namespace SoundboardLite.Core.Models;

/// <summary>
/// A downsampled min/max envelope of an original audio file's (mono-downmixed) waveform, used to
/// draw the editor's waveform control without decoding the full file.
/// </summary>
public sealed record PeakData(float[] Min, float[] Max, long TotalSourceFrames, int SourceSampleRate)
{
    public int PeakCount => Min.Length;
}
