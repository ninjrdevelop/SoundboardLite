using NAudio.Wave;

namespace SoundboardLite.Audio.Internal;

/// <summary>
/// Plays a single, already-decoded sample buffer from its own read cursor. A fresh instance is
/// created per trigger so the same clip can be re-triggered concurrently (polyphony), and each
/// instance fires <see cref="Completed"/> exactly once, right after it reports end-of-stream to
/// the mixer, so callers can clean up tracking state (and later, release a push-to-talk hold).
/// </summary>
internal sealed class BufferSampleProvider : ISampleProvider
{
    private readonly float[] _samples;
    private int _position;
    private bool _completedFired;

    public BufferSampleProvider(CachedClipAudio audio)
    {
        _samples = audio.Samples;
        WaveFormat = audio.Format;
    }

    public WaveFormat WaveFormat { get; }

    /// <summary>Invoked once, on the mixer's audio thread, the first time Read reports exhaustion.</summary>
    public Action? Completed { get; set; }

    public int Read(Span<float> buffer)
    {
        var remaining = _samples.Length - _position;
        var toCopy = Math.Min(remaining, buffer.Length);

        if (toCopy > 0)
        {
            _samples.AsSpan(_position, toCopy).CopyTo(buffer);
            _position += toCopy;
        }

        if (toCopy < buffer.Length && !_completedFired)
        {
            _completedFired = true;
            Completed?.Invoke();
        }

        return toCopy;
    }
}
