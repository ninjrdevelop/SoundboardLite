namespace SoundboardLite.Core.Abstractions;

/// <summary>
/// Shifts the pitch of interleaved PCM without changing its duration/tempo. Hidden behind an
/// interface so the SoundTouch-backed implementation can be swapped (e.g. for NWaves) without
/// touching the render pipeline.
/// </summary>
public interface IPitchShifter
{
    /// <summary>
    /// Returns the pitch-shifted interleaved samples. The output sample count is not guaranteed to
    /// exactly match the input (processing latency/flush behaviour of the underlying algorithm).
    /// </summary>
    float[] Shift(ReadOnlySpan<float> interleavedSamples, int sampleRate, int channels, double semitones);
}
