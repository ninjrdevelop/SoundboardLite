using NAudio.Wave;

namespace SoundboardLite.Audio.Internal;

/// <summary>Fully-decoded PCM for a short clip, already resampled to the mixer's canonical format.</summary>
internal sealed record CachedClipAudio(float[] Samples, WaveFormat Format)
{
    public long ApproximateBytes => Samples.Length * sizeof(float);
}
