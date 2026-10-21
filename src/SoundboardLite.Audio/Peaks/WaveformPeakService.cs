using NAudio.Wave;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Models;

namespace SoundboardLite.Audio.Peaks;

/// <summary>
/// Generates a downsampled min/max peak envelope (mono-downmixed) for the waveform editor control,
/// persisted as a small binary file at peaks/{originalFileHash}.peaks so the editor never has to
/// decode the full original just to draw the waveform.
/// </summary>
public sealed class WaveformPeakService : IWaveformPeakService
{
    private const int PeaksPerSecond = 100;
    private const uint FileMagic = 0x4B504253; // "SBPK" little-endian
    private const int FileVersion = 1;

    private readonly IAppPaths _appPaths;

    public WaveformPeakService(IAppPaths appPaths) => _appPaths = appPaths;

    public async Task EnsurePeaksAsync(string originalFileHash, string originalFilePath, CancellationToken ct = default)
    {
        var peaksPath = GetPeaksPath(originalFileHash);
        if (File.Exists(peaksPath)) return;

        var peaks = await Task.Run(() => Generate(originalFilePath), ct);

        var tempPath = peaksPath + ".generating";
        WriteToFile(peaks, tempPath);
        File.Move(tempPath, peaksPath, overwrite: true);
    }

    public PeakData? LoadPeaks(string originalFileHash)
    {
        var peaksPath = GetPeaksPath(originalFileHash);
        return File.Exists(peaksPath) ? ReadFromFile(peaksPath) : null;
    }

    private string GetPeaksPath(string originalFileHash) =>
        Path.Combine(_appPaths.PeaksDirectory, originalFileHash + ".peaks");

    private static PeakData Generate(string filePath)
    {
        using var reader = new MediaFoundationReader(filePath);
        var sp = reader.ToSampleProvider();
        var sampleRate = sp.WaveFormat.SampleRate;
        var channels = sp.WaveFormat.Channels;

        var framesPerBucket = Math.Max(1, sampleRate / PeaksPerSecond);
        var mins = new List<float>();
        var maxes = new List<float>();

        var bucketMin = float.MaxValue;
        var bucketMax = float.MinValue;
        var framesInBucket = 0;
        long totalFrames = 0;

        var chunk = new float[framesPerBucket * channels];
        int read;
        while ((read = sp.Read(chunk.AsSpan())) > 0)
        {
            var framesRead = read / channels;
            for (var frame = 0; frame < framesRead; frame++)
            {
                var sum = 0f;
                for (var ch = 0; ch < channels; ch++)
                {
                    sum += chunk[(frame * channels) + ch];
                }
                var mono = sum / channels;

                if (mono < bucketMin) bucketMin = mono;
                if (mono > bucketMax) bucketMax = mono;
                framesInBucket++;
                totalFrames++;

                if (framesInBucket == framesPerBucket)
                {
                    mins.Add(bucketMin);
                    maxes.Add(bucketMax);
                    bucketMin = float.MaxValue;
                    bucketMax = float.MinValue;
                    framesInBucket = 0;
                }
            }
        }

        if (framesInBucket > 0)
        {
            mins.Add(bucketMin);
            maxes.Add(bucketMax);
        }

        return new PeakData(mins.ToArray(), maxes.ToArray(), totalFrames, sampleRate);
    }

    private static void WriteToFile(PeakData peaks, string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write(FileMagic);
        writer.Write(FileVersion);
        writer.Write(peaks.SourceSampleRate);
        writer.Write(peaks.TotalSourceFrames);
        writer.Write(peaks.PeakCount);
        foreach (var v in peaks.Min) writer.Write(v);
        foreach (var v in peaks.Max) writer.Write(v);
    }

    private static PeakData ReadFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        var magic = reader.ReadUInt32();
        if (magic != FileMagic)
        {
            throw new InvalidDataException($"'{path}' is not a valid peaks file.");
        }
        _ = reader.ReadInt32(); // version — only version 1 exists so far
        var sampleRate = reader.ReadInt32();
        var totalFrames = reader.ReadInt64();
        var count = reader.ReadInt32();

        var min = new float[count];
        for (var i = 0; i < count; i++) min[i] = reader.ReadSingle();
        var max = new float[count];
        for (var i = 0; i < count; i++) max[i] = reader.ReadSingle();

        return new PeakData(min, max, totalFrames, sampleRate);
    }
}
