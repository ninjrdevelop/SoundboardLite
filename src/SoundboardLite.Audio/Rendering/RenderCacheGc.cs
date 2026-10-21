using SoundboardLite.Core.Abstractions;

namespace SoundboardLite.Audio.Rendering;

public sealed class RenderCacheGc : IRenderCacheGc
{
    private readonly IClipRepository _clipRepository;
    private readonly IAppPaths _appPaths;

    public RenderCacheGc(IClipRepository clipRepository, IAppPaths appPaths)
    {
        _clipRepository = clipRepository;
        _appPaths = appPaths;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var referenced = await _clipRepository.GetAllRenderKeysAsync(ct);
        var referencedFileNames = referenced
            .Select(key => key + ".wav")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(_appPaths.CacheDirectory)) return;

        foreach (var file in Directory.EnumerateFiles(_appPaths.CacheDirectory, "*.wav"))
        {
            ct.ThrowIfCancellationRequested();
            if (referencedFileNames.Contains(Path.GetFileName(file))) continue;

            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // In use or transiently locked — picked up on the next GC pass.
            }
        }
    }
}
