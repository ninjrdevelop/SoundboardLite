using NAudio.Wave;
using Shouldly;
using SoundboardLite.Audio.Peaks;
using SoundboardLite.Core.Services;

namespace SoundboardLite.Audio.Tests;

public sealed class WaveformPeakServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly AppPaths _appPaths;
    private readonly WaveformPeakService _sut;

    public WaveformPeakServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "SoundboardLitePeakTests_" + Guid.NewGuid());
        _appPaths = new AppPaths(_tempRoot);
        _sut = new WaveformPeakService(_appPaths);
    }

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    private string WriteSineWavFixture(string name, int durationMs = 1000, int sampleRate = 44100, float amplitude = 0.6f)
    {
        var path = Path.Combine(_tempRoot, name);
        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        using var writer = new WaveFileWriter(path, format);

        var totalFrames = sampleRate * durationMs / 1000;
        var samples = new float[totalFrames];
        for (var i = 0; i < totalFrames; i++)
        {
            samples[i] = amplitude * (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate);
        }
        writer.WriteSamples(samples, 0, samples.Length);
        return path;
    }

    [Fact]
    public async Task EnsurePeaksAsync_GeneratesFile_ThatLoadsBackWithMatchingEnvelope()
    {
        var wavPath = WriteSineWavFixture("clip.wav", durationMs: 1000, amplitude: 0.6f);

        await _sut.EnsurePeaksAsync("hash1", wavPath);
        var peaks = _sut.LoadPeaks("hash1");

        peaks.ShouldNotBeNull();
        peaks.PeakCount.ShouldBeGreaterThan(0);
        peaks.Max.Max().ShouldBeInRange(0.4f, 0.7f);
        peaks.Min.Min().ShouldBeInRange(-0.7f, -0.4f);
        peaks.SourceSampleRate.ShouldBe(44100);
    }

    [Fact]
    public async Task EnsurePeaksAsync_CalledTwice_DoesNotRegenerate()
    {
        var wavPath = WriteSineWavFixture("clip.wav");
        await _sut.EnsurePeaksAsync("hash1", wavPath);

        var peaksPath = Path.Combine(_appPaths.PeaksDirectory, "hash1.peaks");
        var firstWriteTime = File.GetLastWriteTimeUtc(peaksPath);

        await Task.Delay(50);
        await _sut.EnsurePeaksAsync("hash1", wavPath);

        File.GetLastWriteTimeUtc(peaksPath).ShouldBe(firstWriteTime);
    }

    [Fact]
    public void LoadPeaks_UnknownHash_ReturnsNull()
    {
        _sut.LoadPeaks("does-not-exist").ShouldBeNull();
    }
}
