using System.Collections.Concurrent;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundboardLite.Audio.Internal;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Models;
using SoundboardLite.Core.Services;

namespace SoundboardLite.Audio;

/// <summary>
/// The single entry point for clip playback (see <see cref="IPlaybackCoordinator"/>). Owns one
/// long-lived <see cref="MixingSampleProvider"/> feeding one <see cref="WasapiOut"/> on the
/// selected device; triggering a clip just adds a mixer input, which is how polyphony works.
///
/// Decoded PCM is cached in a memory-budgeted LRU (<see cref="DecodedAudioCache"/>) keyed by the
/// clip's current render key — or by "orig:{hash}" for a clip that has no render yet, in which case
/// playback falls back to the untouched original file (M1 behaviour) until the render queue
/// produces its first cache/{renderKey}.wav.
/// </summary>
public sealed class PlaybackCoordinator : IPlaybackCoordinator, IDisposable
{
    private readonly WaveFormat _mixerFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
    private readonly MixingSampleProvider _mixer;
    private readonly IClipRepository _clipRepository;
    private readonly IOriginalFileRepository _originalFileRepository;
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IAppPaths _appPaths;
    private readonly IPushToTalkController _pushToTalkController;

    private readonly object _outputLock = new();
    private WasapiPlayer? _output;
    private MMDevice? _outputDevice;

    private sealed class ActiveInstance
    {
        public required BufferSampleProvider Provider;
        public required string CacheKey;
        public PttContext? Ptt;
        private int _cacheReleased;
        private int _pttReleaseStarted;

        /// <summary>Idempotent: natural end-of-stream and an explicit Stop can both race to release
        /// the same instance's cache refcount — only the first one should count.</summary>
        public void ReleaseCacheOnce(DecodedAudioCache cache)
        {
            if (Interlocked.Exchange(ref _cacheReleased, 1) == 0)
            {
                cache.Release(CacheKey);
            }
        }

        /// <summary>Idempotent for the same reason as <see cref="ReleaseCacheOnce"/>. Fires the
        /// end-delay-then-key-up sequence at most once per instance; no-ops if PTT wasn't engaged.</summary>
        public async Task ReleasePttOnceAsync(IPushToTalkController controller)
        {
            if (Ptt is not { } ptt) return;
            if (Interlocked.Exchange(ref _pttReleaseStarted, 1) != 0) return;

            if (ptt.EndDelayMs > 0)
            {
                await Task.Delay(ptt.EndDelayMs);
            }
            controller.Release(ptt.VirtualKeyCode);
        }
    }

    private readonly object _activeLock = new();
    private readonly Dictionary<Guid, List<ActiveInstance>> _activeByClip = new();

    private readonly DecodedAudioCache _decodedCache = new();
    private readonly ConcurrentDictionary<string, Task<CachedClipAudio>> _inFlightDecodes = new();

    public PlaybackCoordinator(
        IClipRepository clipRepository,
        IOriginalFileRepository originalFileRepository,
        IAppSettingsRepository appSettingsRepository,
        IAppPaths appPaths,
        IPushToTalkController pushToTalkController)
    {
        _clipRepository = clipRepository;
        _originalFileRepository = originalFileRepository;
        _appSettingsRepository = appSettingsRepository;
        _appPaths = appPaths;
        _pushToTalkController = pushToTalkController;
        _mixer = new MixingSampleProvider(_mixerFormat) { ReadFully = true };
    }

    public string? CurrentOutputDeviceId { get; private set; }

    public event EventHandler<Guid>? ClipStarted;
    public event EventHandler<Guid>? ClipStopped;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var lowLatencyRaw = await _appSettingsRepository.GetAsync(AppSettingsKeys.LowLatencyOutput, ct);
        _lowLatencyMode = lowLatencyRaw == bool.TrueString;

        var savedId = await _appSettingsRepository.GetAsync(AppSettingsKeys.SelectedOutputDeviceId, ct);
        var devices = GetOutputDevices();

        var target = (savedId is not null ? devices.FirstOrDefault(d => d.Id == savedId) : null)
            ?? devices.FirstOrDefault(d => d.IsDefault)
            ?? devices.FirstOrDefault();

        if (target is not null)
        {
            SetOutputDevice(target.Id);
        }
    }

    public IReadOnlyList<OutputDeviceInfo> GetOutputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();

        string? defaultId = null;
        try
        {
            using var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            defaultId = defaultDevice.ID;
        }
        catch (Exception)
        {
            // No default render device (e.g. no speakers attached) — leave defaultId null, still list what exists.
        }

        var result = new List<OutputDeviceInfo>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
            {
                result.Add(new OutputDeviceInfo(device.ID, device.FriendlyName, device.ID == defaultId));
            }
        }
        return result;
    }

    private const int DefaultLatencyMs = 100;
    private const int LowLatencyMs = 45;
    private volatile bool _lowLatencyMode;

    public void SetLowLatencyMode(bool enabled)
    {
        _lowLatencyMode = enabled;
        _ = _appSettingsRepository.SetAsync(AppSettingsKeys.LowLatencyOutput, enabled.ToString());

        if (CurrentOutputDeviceId is { } deviceId)
        {
            SetOutputDevice(deviceId);
        }
    }

    public void SetOutputDevice(string deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDevice(deviceId);

        var player = new WasapiPlayerBuilder()
            .WithDevice(device)
            .WithSharedMode()
            .WithLatency(_lowLatencyMode ? LowLatencyMs : DefaultLatencyMs)
            .Build();
        player.Init(_mixer.ToWaveProvider());
        player.Play();

        lock (_outputLock)
        {
            var previousPlayer = _output;
            var previousDevice = _outputDevice;

            _output = player;
            _outputDevice = device;
            CurrentOutputDeviceId = deviceId;

            previousPlayer?.Stop();
            previousPlayer?.Dispose();
            previousDevice?.Dispose();
        }

        _ = _appSettingsRepository.SetAsync(AppSettingsKeys.SelectedOutputDeviceId, deviceId);
    }

    public async Task TriggerClipAsync(Guid clipId, CancellationToken ct = default)
    {
        var clip = await _clipRepository.GetAsync(clipId, ct)
            ?? throw new InvalidOperationException($"Clip {clipId} not found.");
        var editSettings = await _clipRepository.GetEditSettingsAsync(clipId, ct);

        PttContext? ptt = null;
        if (editSettings is not null)
        {
            ptt = await PttContextResolver.ResolveAsync(editSettings, _appSettingsRepository, ct);
            if (ptt is not null)
            {
                // Key-down only on the 0->1 transition (ref-counted inside the controller), so two
                // overlapping PTT clips sharing a key don't double-press it.
                _pushToTalkController.Acquire(ptt.VirtualKeyCode);
                if (ptt.BeginDelayMs > 0)
                {
                    await Task.Delay(ptt.BeginDelayMs, ct);
                }
            }
        }

        var (cacheKey, cached) = await GetOrAcquireAsync(clip, editSettings, ct);
        var provider = new BufferSampleProvider(cached);
        var instance = new ActiveInstance { Provider = provider, CacheKey = cacheKey, Ptt = ptt };

        lock (_activeLock)
        {
            provider.Completed = () =>
            {
                RemoveActive(clipId, instance);
                instance.ReleaseCacheOnce(_decodedCache);
                _ = instance.ReleasePttOnceAsync(_pushToTalkController);
                ClipStopped?.Invoke(this, clipId);
            };
            if (!_activeByClip.TryGetValue(clipId, out var list))
            {
                list = new List<ActiveInstance>();
                _activeByClip[clipId] = list;
            }
            list.Add(instance);
        }

        _mixer.AddMixerInput(provider);
        ClipStarted?.Invoke(this, clipId);
    }

    public void StopClip(Guid clipId)
    {
        List<ActiveInstance>? toRemove = null;
        lock (_activeLock)
        {
            if (_activeByClip.TryGetValue(clipId, out var list))
            {
                toRemove = [..list];
                _activeByClip.Remove(clipId);
            }
        }

        if (toRemove is null) return;
        foreach (var instance in toRemove)
        {
            _mixer.RemoveMixerInput(instance.Provider);
            instance.ReleaseCacheOnce(_decodedCache);
            _ = instance.ReleasePttOnceAsync(_pushToTalkController);
            ClipStopped?.Invoke(this, clipId);
        }
    }

    public void StopAll()
    {
        List<(Guid ClipId, ActiveInstance Instance)> all;
        lock (_activeLock)
        {
            all = _activeByClip.SelectMany(kvp => kvp.Value.Select(instance => (kvp.Key, instance))).ToList();
            _activeByClip.Clear();
        }
        _mixer.RemoveAllMixerInputs();
        foreach (var (clipId, instance) in all)
        {
            instance.ReleaseCacheOnce(_decodedCache);
            _ = instance.ReleasePttOnceAsync(_pushToTalkController);
            ClipStopped?.Invoke(this, clipId);
        }
    }

    private static readonly Guid PreviewSlotId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task PlayPreviewFileAsync(string filePath, CancellationToken ct = default)
    {
        StopPreview();

        var decoded = await Task.Run(() => DecodeToCanonicalFormat(filePath, _mixerFormat), ct);
        var provider = new BufferSampleProvider(decoded);
        // No cache entry backs a preview (it's a throwaway render), so there's nothing to release —
        // CacheKey is left empty, and DecodedAudioCache.Release no-ops on an unknown key.
        var instance = new ActiveInstance { Provider = provider, CacheKey = string.Empty };

        lock (_activeLock)
        {
            provider.Completed = () => RemoveActive(PreviewSlotId, instance);
            if (!_activeByClip.TryGetValue(PreviewSlotId, out var list))
            {
                list = new List<ActiveInstance>();
                _activeByClip[PreviewSlotId] = list;
            }
            list.Add(instance);
        }

        _mixer.AddMixerInput(provider);
    }

    public void StopPreview() => StopClip(PreviewSlotId);

    private void RemoveActive(Guid clipId, ActiveInstance instance)
    {
        lock (_activeLock)
        {
            if (_activeByClip.TryGetValue(clipId, out var list))
            {
                list.Remove(instance);
                if (list.Count == 0) _activeByClip.Remove(clipId);
            }
        }
    }

    /// <summary>
    /// Resolves which file backs the clip's current playback (the render cache if one exists and
    /// matches, else the untouched original) and returns a cache-acquired decode of it. The caller
    /// must release the returned key exactly once (see <see cref="ActiveInstance.ReleaseCacheOnce"/>).
    /// Takes the already-loaded clip/settings (rather than re-querying by id) since
    /// <see cref="TriggerClipAsync"/> already needs them for PTT resolution before this is called.
    /// </summary>
    private async Task<(string CacheKey, CachedClipAudio Audio)> GetOrAcquireAsync(
        Clip clip, ClipEditSettings? editSettings, CancellationToken ct)
    {
        string cacheKey;
        string sourceFilePath;

        var renderPath = editSettings?.CurrentRenderKey is { } renderKey
            ? Path.Combine(_appPaths.CacheDirectory, renderKey + ".wav")
            : null;

        if (renderPath is not null && File.Exists(renderPath))
        {
            cacheKey = "render:" + editSettings!.CurrentRenderKey;
            sourceFilePath = renderPath;
        }
        else
        {
            var original = await _originalFileRepository.GetAsync(clip.OriginalFileHash, ct)
                ?? throw new InvalidOperationException($"'{clip.Name}' has no original file on record.");
            if (!File.Exists(original.FilePath))
            {
                throw new InvalidOperationException($"'{clip.Name}' is missing its source file on disk.");
            }
            cacheKey = "orig:" + clip.OriginalFileHash;
            sourceFilePath = original.FilePath;
        }

        if (_decodedCache.TryAcquire(cacheKey, out var cached))
        {
            return (cacheKey, cached);
        }

        var decodeTask = _inFlightDecodes.GetOrAdd(
            cacheKey,
            _ => Task.Run(() => DecodeToCanonicalFormat(sourceFilePath, _mixerFormat), ct));
        try
        {
            var decoded = await decodeTask.WaitAsync(ct);
            _decodedCache.InsertAndAcquire(cacheKey, decoded);
            return (cacheKey, decoded);
        }
        finally
        {
            _inFlightDecodes.TryRemove(new KeyValuePair<string, Task<CachedClipAudio>>(cacheKey, decodeTask));
        }
    }

    private static CachedClipAudio DecodeToCanonicalFormat(string filePath, WaveFormat target)
    {
        using var reader = new MediaFoundationReader(filePath);
        ISampleProvider sp = reader.ToSampleProvider();

        if (sp.WaveFormat.Channels == 1)
        {
            sp = new MonoToStereoSampleProvider(sp);
        }
        else if (sp.WaveFormat.Channels > 2)
        {
            throw new NotSupportedException(
                $"'{filePath}' has {sp.WaveFormat.Channels} channels; only mono and stereo sources are supported.");
        }

        if (sp.WaveFormat.SampleRate != target.SampleRate)
        {
            sp = new WdlResamplingSampleProvider(sp, target.SampleRate);
        }

        var buffer = new List<float>();
        var chunk = new float[target.SampleRate * target.Channels];
        int read;
        while ((read = sp.Read(chunk.AsSpan())) > 0)
        {
            buffer.AddRange(read == chunk.Length ? chunk : chunk[..read]);
        }

        return new CachedClipAudio(buffer.ToArray(), target);
    }

    public void Dispose()
    {
        lock (_outputLock)
        {
            _output?.Stop();
            _output?.Dispose();
            _output = null;
            _outputDevice?.Dispose();
            _outputDevice = null;
        }
    }
}
