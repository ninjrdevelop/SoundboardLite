using NAudio.Wave;
using Shouldly;
using SoundboardLite.Audio.Pitch;
using SoundboardLite.Audio.Rendering;
using SoundboardLite.Core.Abstractions;

namespace SoundboardLite.Audio.Tests;

public sealed class ClipRendererTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly ClipRenderer _sut = new(new SoundTouchPitchShifter());

    public ClipRendererTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "SoundboardLiteClipRendererTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    private string WriteSineWavFixture(
        string name, int durationMs = 1000, int sampleRate = 48000, int channels = 1,
        float amplitude = 0.3f, double frequencyHz = 440)
    {
        var path = Path.Combine(_tempRoot, name);
        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        using var writer = new WaveFileWriter(path, format);

        var totalFrames = sampleRate * durationMs / 1000;
        var samples = new float[totalFrames * channels];
        for (var frame = 0; frame < totalFrames; frame++)
        {
            var value = amplitude * (float)Math.Sin(2 * Math.PI * frequencyHz * frame / sampleRate);
            for (var ch = 0; ch < channels; ch++)
            {
                samples[(frame * channels) + ch] = value;
            }
        }
        writer.WriteSamples(samples, 0, samples.Length);
        return path;
    }

    private static float[] ReadAllSamples(string path, out WaveFormat format)
    {
        using var reader = new MediaFoundationReader(path);
        var sp = reader.ToSampleProvider();
        format = sp.WaveFormat;

        var buffer = new List<float>();
        var chunk = new float[format.SampleRate * format.Channels];
        int read;
        while ((read = sp.Read(chunk.AsSpan())) > 0)
        {
            buffer.AddRange(read == chunk.Length ? chunk : chunk[..read]);
        }
        return buffer.ToArray();
    }

    [Fact]
    public async Task RenderAsync_Trim_ProducesExpectedDuration()
    {
        var source = WriteSineWavFixture("source.wav", durationMs: 1000);
        var output = Path.Combine(_tempRoot, "out.wav");

        var request = new ClipRenderRequest(source, TrimStartMs: 200, TrimEndMs: 700, PitchSemitones: 0, GainDb: 0, FadeInMs: 0, FadeOutMs: 0);
        await _sut.RenderAsync(request, output);

        var samples = ReadAllSamples(output, out var format);
        var actualDurationMs = samples.Length / (double)format.Channels / format.SampleRate * 1000;

        actualDurationMs.ShouldBeInRange(490, 510);
    }

    [Fact]
    public async Task RenderAsync_NoEdits_PreservesOriginalDuration()
    {
        var source = WriteSineWavFixture("source.wav", durationMs: 800);
        var output = Path.Combine(_tempRoot, "out.wav");

        var request = new ClipRenderRequest(source, TrimStartMs: 0, TrimEndMs: null, PitchSemitones: 0, GainDb: 0, FadeInMs: 0, FadeOutMs: 0);
        await _sut.RenderAsync(request, output);

        var samples = ReadAllSamples(output, out var format);
        var actualDurationMs = samples.Length / (double)format.Channels / format.SampleRate * 1000;

        actualDurationMs.ShouldBeInRange(790, 810);
    }

    [Fact]
    public async Task RenderAsync_GainChange_ScalesAmplitude()
    {
        var source = WriteSineWavFixture("source.wav", durationMs: 200, amplitude: 0.3f);

        var unityOutput = Path.Combine(_tempRoot, "unity.wav");
        await _sut.RenderAsync(new ClipRenderRequest(source, 0, null, 0, 0, 0, 0), unityOutput);

        var boostedOutput = Path.Combine(_tempRoot, "boosted.wav");
        await _sut.RenderAsync(new ClipRenderRequest(source, 0, null, 0, GainDb: 6, 0, 0), boostedOutput);

        var unitySamples = ReadAllSamples(unityOutput, out _);
        var boostedSamples = ReadAllSamples(boostedOutput, out _);

        double unityPeak = unitySamples.Max(Math.Abs);
        double boostedPeak = boostedSamples.Max(Math.Abs);

        (boostedPeak / unityPeak).ShouldBeInRange(1.9, 2.1); // +6dB ≈ x2 linear gain
    }

    [Fact]
    public async Task RenderAsync_FadeIn_RampsFromSilence()
    {
        var source = WriteSineWavFixture("source.wav", durationMs: 500, amplitude: 0.5f);
        var output = Path.Combine(_tempRoot, "out.wav");

        var request = new ClipRenderRequest(source, 0, null, 0, 0, FadeInMs: 100, FadeOutMs: 0);
        await _sut.RenderAsync(request, output);

        var samples = ReadAllSamples(output, out var format);

        // First sample of the fade-in should be at (or extremely close to) silence.
        Math.Abs(samples[0]).ShouldBeLessThan(0.01f);

        // Well after the 100ms fade-in window, amplitude should be back to full.
        var framesPerMs = format.SampleRate / 1000.0;
        var pastFadeFrame = (int)(150 * framesPerMs);
        var pastFadeWindow = samples.Skip(pastFadeFrame * format.Channels).Take(1000).ToArray();
        pastFadeWindow.Max(Math.Abs).ShouldBeGreaterThan(0.4f);
    }

    [Fact]
    public async Task RenderAsync_PitchShift_PreservesApproximateDuration()
    {
        var source = WriteSineWavFixture("source.wav", durationMs: 1000);
        var output = Path.Combine(_tempRoot, "out.wav");

        var request = new ClipRenderRequest(source, 0, null, PitchSemitones: 5, 0, 0, 0);
        await _sut.RenderAsync(request, output);

        var samples = ReadAllSamples(output, out var format);
        var actualDurationMs = samples.Length / (double)format.Channels / format.SampleRate * 1000;

        // SoundTouch changes pitch independent of tempo — duration should be essentially unchanged.
        actualDurationMs.ShouldBeInRange(950, 1050);
    }

    [Fact]
    public async Task RenderAsync_RejectsMoreThanStereoSource()
    {
        var path = Path.Combine(_tempRoot, "quad.wav");
        var format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 4);
        using (var writer = new WaveFileWriter(path, format))
        {
            writer.WriteSamples(new float[format.SampleRate * format.Channels], 0, format.SampleRate * format.Channels);
        }

        var output = Path.Combine(_tempRoot, "out.wav");
        var request = new ClipRenderRequest(path, 0, null, 0, 0, 0, 0);

        await Should.ThrowAsync<NotSupportedException>(() => _sut.RenderAsync(request, output));
    }
}
