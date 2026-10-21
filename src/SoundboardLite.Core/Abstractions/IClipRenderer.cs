using SoundboardLite.Core.Models;

namespace SoundboardLite.Core.Abstractions;

/// <summary>The subset of a clip's edit settings needed to render it, plus the original file to render from.</summary>
public sealed record ClipRenderRequest(
    string SourceFilePath,
    long TrimStartMs,
    long? TrimEndMs,
    double PitchSemitones,
    double GainDb,
    long FadeInMs,
    long FadeOutMs)
{
    public static ClipRenderRequest FromSettings(string sourceFilePath, ClipEditSettings settings) => new(
        sourceFilePath,
        settings.TrimStartMs,
        settings.TrimEndMs,
        settings.PitchSemitones,
        settings.GainDb,
        settings.FadeInMs,
        settings.FadeOutMs);
}

/// <summary>
/// Offline render pipeline: trim → pitch-shift → gain/fade → resample to the canonical mixer format,
/// writing a self-contained WAV file. The original source file is never modified.
/// </summary>
public interface IClipRenderer
{
    Task RenderAsync(ClipRenderRequest request, string outputFilePath, CancellationToken ct = default);
}
