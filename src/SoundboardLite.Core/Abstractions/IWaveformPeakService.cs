using SoundboardLite.Core.Models;

namespace SoundboardLite.Core.Abstractions;

/// <summary>
/// Generates and reads the downsampled peak envelope used by the waveform editor control. Peaks
/// are keyed by the original file's content hash (not by renderKey) since trim/pitch/gain edits
/// never change the underlying waveform being displayed, only which region/processing applies to it.
/// </summary>
public interface IWaveformPeakService
{
    /// <summary>No-ops if peaks/{originalFileHash}.peaks already exists. Safe to call repeatedly.</summary>
    Task EnsurePeaksAsync(string originalFileHash, string originalFilePath, CancellationToken ct = default);

    /// <summary>Null if peaks haven't been generated yet (e.g. the background job hasn't finished).</summary>
    PeakData? LoadPeaks(string originalFileHash);
}
