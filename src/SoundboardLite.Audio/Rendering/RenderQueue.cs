using System.Collections.Concurrent;
using System.Threading.Channels;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Services;

namespace SoundboardLite.Audio.Rendering;

/// <summary>
/// Single-consumer background render queue (see <see cref="IRenderQueue"/>). Correctness doesn't
/// depend on actually cancelling a superseded in-flight render: each job always reads the clip's
/// *current* settings from the repository right before rendering and short-circuits if the cache
/// already matches, so a stale/duplicate job just becomes a cheap no-op once a newer one has landed.
/// </summary>
public sealed class RenderQueue : IRenderQueue, IDisposable
{
    private readonly IClipRepository _clipRepository;
    private readonly IOriginalFileRepository _originalFileRepository;
    private readonly IClipRenderer _clipRenderer;
    private readonly IAppPaths _appPaths;

    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _inFlight = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _consumerLoop;

    public event EventHandler<RenderCompletedEventArgs>? RenderCompleted;

    public RenderQueue(
        IClipRepository clipRepository,
        IOriginalFileRepository originalFileRepository,
        IClipRenderer clipRenderer,
        IAppPaths appPaths)
    {
        _clipRepository = clipRepository;
        _originalFileRepository = originalFileRepository;
        _clipRenderer = clipRenderer;
        _appPaths = appPaths;
        _consumerLoop = Task.Run(ConsumeAsync);
    }

    public void RequestRender(Guid clipId)
    {
        if (_inFlight.TryRemove(clipId, out var previousCts))
        {
            previousCts.Cancel();
            previousCts.Dispose();
        }
        _channel.Writer.TryWrite(clipId);
    }

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (var clipId in _channel.Reader.ReadAllAsync(_shutdown.Token))
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                if (!_inFlight.TryAdd(clipId, cts))
                {
                    cts.Dispose();
                    continue;
                }

                try
                {
                    await RenderOneAsync(clipId, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Superseded by a newer request for the same clip; the newer enqueue will render it.
                }
                catch (Exception ex)
                {
                    RenderCompleted?.Invoke(this, new RenderCompletedEventArgs(clipId, false, ex.Message));
                }
                finally
                {
                    _inFlight.TryRemove(clipId, out _);
                    cts.Dispose();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task RenderOneAsync(Guid clipId, CancellationToken ct)
    {
        var clip = await _clipRepository.GetAsync(clipId, ct);
        var settings = clip is null ? null : await _clipRepository.GetEditSettingsAsync(clipId, ct);
        if (clip is null || settings is null) return;

        var renderKey = RenderKeyCalculator.Compute(clip.OriginalFileHash, settings);
        var outputPath = Path.Combine(_appPaths.CacheDirectory, renderKey + ".wav");

        if (settings.CurrentRenderKey == renderKey && File.Exists(outputPath))
        {
            RenderCompleted?.Invoke(this, new RenderCompletedEventArgs(clipId, true, null));
            return;
        }

        var original = await _originalFileRepository.GetAsync(clip.OriginalFileHash, ct)
            ?? throw new InvalidOperationException($"Original file for clip {clipId} is missing.");

        var request = ClipRenderRequest.FromSettings(original.FilePath, settings);
        await _clipRenderer.RenderAsync(request, outputPath, ct);

        ct.ThrowIfCancellationRequested();

        settings.CurrentRenderKey = renderKey;
        settings.UpdatedUtc = DateTimeOffset.UtcNow;
        await _clipRepository.UpdateEditSettingsAsync(settings, ct);

        RenderCompleted?.Invoke(this, new RenderCompletedEventArgs(clipId, true, null));
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _channel.Writer.TryComplete();
        try
        {
            _consumerLoop.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Best-effort drain on shutdown.
        }
        _shutdown.Dispose();
    }
}
