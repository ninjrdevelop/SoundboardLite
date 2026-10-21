using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundboardLite.Audio.Internal;
using SoundboardLite.Core.Abstractions;

namespace SoundboardLite.Audio.Rendering;

/// <summary>
/// Offline render pipeline: decode → trim → pitch-shift → gain → fades → resample to the canonical
/// 48kHz/stereo/float32 mixer format → write a WAV file. Entirely batch/in-memory (not streaming) —
/// clips are short, so this is simpler to reason about and test than chaining streaming
/// <see cref="ISampleProvider"/>s end to end.
/// </summary>
public sealed class ClipRenderer : IClipRenderer
{
    public static readonly WaveFormat CanonicalFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

    private readonly IPitchShifter _pitchShifter;

    public ClipRenderer(IPitchShifter pitchShifter) => _pitchShifter = pitchShifter;

    public Task RenderAsync(ClipRenderRequest request, string outputFilePath, CancellationToken ct = default) =>
        Task.Run(() => Render(request, outputFilePath), ct);

    private void Render(ClipRenderRequest request, string outputFilePath)
    {
        using var reader = new MediaFoundationReader(request.SourceFilePath);
        ISampleProvider sp = reader.ToSampleProvider();

        if (sp.WaveFormat.Channels == 1)
        {
            sp = new MonoToStereoSampleProvider(sp);
        }
        else if (sp.WaveFormat.Channels > 2)
        {
            throw new NotSupportedException(
                $"'{request.SourceFilePath}' has {sp.WaveFormat.Channels} channels; only mono and stereo sources are supported.");
        }

        var sourceFormat = sp.WaveFormat;
        var samples = ReadTrimmed(sp, sourceFormat, request.TrimStartMs, request.TrimEndMs);

        if (request.PitchSemitones != 0 && samples.Length > 0)
        {
            samples = _pitchShifter.Shift(samples, sourceFormat.SampleRate, sourceFormat.Channels, request.PitchSemitones);
        }

        ApplyGain(samples, request.GainDb);
        ApplyFades(samples, sourceFormat.Channels, sourceFormat.SampleRate, request.FadeInMs, request.FadeOutMs);

        if (sourceFormat.SampleRate != CanonicalFormat.SampleRate)
        {
            samples = Resample(samples, sourceFormat, CanonicalFormat);
        }

        WriteWav(samples, CanonicalFormat, outputFilePath);
    }

    private static float[] ReadTrimmed(ISampleProvider source, WaveFormat format, long trimStartMs, long? trimEndMs)
    {
        var offset = new OffsetSampleProvider(source) { SkipOver = TimeSpan.FromMilliseconds(trimStartMs) };
        if (trimEndMs is { } endMs)
        {
            offset.Take = TimeSpan.FromMilliseconds(Math.Max(0, endMs - trimStartMs));
        }

        return ReadAll(offset, format);
    }

    private static float[] Resample(float[] samples, WaveFormat sourceFormat, WaveFormat targetFormat)
    {
        ISampleProvider sp = new BufferSampleProvider(new CachedClipAudio(samples, sourceFormat));
        sp = new WdlResamplingSampleProvider(sp, targetFormat.SampleRate);
        return ReadAll(sp, targetFormat);
    }

    private static float[] ReadAll(ISampleProvider source, WaveFormat format)
    {
        var buffer = new List<float>();
        var chunk = new float[Math.Max(format.SampleRate, 1) * format.Channels];
        int read;
        while ((read = source.Read(chunk.AsSpan())) > 0)
        {
            buffer.AddRange(read == chunk.Length ? chunk : chunk[..read]);
        }
        return buffer.ToArray();
    }

    private static void ApplyGain(float[] samples, double gainDb)
    {
        if (gainDb == 0) return;

        var linear = (float)Math.Pow(10, gainDb / 20.0);
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= linear;
        }
    }

    private static void ApplyFades(float[] samples, int channels, int sampleRate, long fadeInMs, long fadeOutMs)
    {
        if (fadeInMs <= 0 && fadeOutMs <= 0) return;

        var totalFrames = samples.Length / channels;
        var fadeInFrames = fadeInMs * sampleRate / 1000;
        var fadeOutFrames = fadeOutMs * sampleRate / 1000;
        var fadeOutStartFrame = Math.Max(0, totalFrames - fadeOutFrames);

        for (var frame = 0; frame < totalFrames; frame++)
        {
            var gain = 1f;
            if (fadeInFrames > 0 && frame < fadeInFrames)
            {
                gain *= (float)frame / fadeInFrames;
            }
            if (fadeOutFrames > 0 && frame >= fadeOutStartFrame)
            {
                var intoFadeOut = frame - fadeOutStartFrame;
                gain *= Math.Max(0f, 1f - (float)intoFadeOut / fadeOutFrames);
            }

            if (gain == 1f) continue;
            for (var ch = 0; ch < channels; ch++)
            {
                samples[(frame * channels) + ch] *= gain;
            }
        }
    }

    private static void WriteWav(float[] samples, WaveFormat format, string outputFilePath)
    {
        // Write to a temp name then move, so a crash/exception mid-write never leaves a partially
        // written file sitting at the renderKey's canonical cache path.
        var tempPath = outputFilePath + ".rendering";
        using (var writer = new WaveFileWriter(tempPath, format))
        {
            writer.WriteSamples(samples, 0, samples.Length);
        }
        File.Move(tempPath, outputFilePath, overwrite: true);
    }
}
