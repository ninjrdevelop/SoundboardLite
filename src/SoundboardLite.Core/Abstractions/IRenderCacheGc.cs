namespace SoundboardLite.Core.Abstractions;

/// <summary>Deletes cache/*.wav files no longer referenced by any clip's CurrentRenderKey.</summary>
public interface IRenderCacheGc
{
    Task RunAsync(CancellationToken ct = default);
}
