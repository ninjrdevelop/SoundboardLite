using SoundTouch;
using SoundboardLite.Core.Abstractions;

namespace SoundboardLite.Audio.Pitch;

/// <summary>
/// Pure-managed (no native dependency) SoundTouch port. Processes the whole buffer in one batch —
/// clips are short, so streaming isn't worth the extra complexity.
/// </summary>
public sealed class SoundTouchPitchShifter : IPitchShifter
{
    public float[] Shift(ReadOnlySpan<float> interleavedSamples, int sampleRate, int channels, double semitones)
    {
        var processor = new SoundTouchProcessor
        {
            SampleRate = sampleRate,
            Channels = channels,
            PitchSemiTones = semitones,
        };

        var totalFrames = interleavedSamples.Length / channels;
        processor.PutSamples(interleavedSamples, totalFrames);
        processor.Flush();

        var output = new List<float>();
        var chunk = new float[4096 * channels];
        int framesReceived;
        while ((framesReceived = processor.ReceiveSamples(chunk, 4096)) > 0)
        {
            output.AddRange(chunk.AsSpan(0, framesReceived * channels));
        }
        return output.ToArray();
    }
}
