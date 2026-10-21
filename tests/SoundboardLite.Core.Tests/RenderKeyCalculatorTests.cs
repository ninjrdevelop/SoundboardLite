using Shouldly;
using SoundboardLite.Core.Models;
using SoundboardLite.Core.Services;

namespace SoundboardLite.Core.Tests;

public sealed class RenderKeyCalculatorTests
{
    private static ClipEditSettings Defaults(Guid? clipId = null) => new()
    {
        ClipId = clipId ?? Guid.NewGuid(),
        TrimStartMs = 0,
        TrimEndMs = null,
        PitchSemitones = 0,
        GainDb = 0,
        FadeInMs = 0,
        FadeOutMs = 0,
        PttEnabled = false,
        UpdatedUtc = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void Compute_SameOriginalHashAndSettings_IsDeterministic()
    {
        var settings = Defaults();

        var key1 = RenderKeyCalculator.Compute("abc123", settings);
        var key2 = RenderKeyCalculator.Compute("abc123", settings);

        key1.ShouldBe(key2);
    }

    [Fact]
    public void Compute_DifferentOriginalHash_ProducesDifferentKey()
    {
        var settings = Defaults();

        var key1 = RenderKeyCalculator.Compute("abc123", settings);
        var key2 = RenderKeyCalculator.Compute("def456", settings);

        key1.ShouldNotBe(key2);
    }

    [Theory]
    [MemberData(nameof(SettingMutations))]
    public void Compute_AnyRenderAffectingSettingChange_ProducesDifferentKey(Action<ClipEditSettings> mutate)
    {
        var baseline = Defaults();
        var mutated = Defaults(baseline.ClipId);
        mutate(mutated);

        var key1 = RenderKeyCalculator.Compute("abc123", baseline);
        var key2 = RenderKeyCalculator.Compute("abc123", mutated);

        key1.ShouldNotBe(key2);
    }

    public static IEnumerable<object[]> SettingMutations()
    {
        yield return [new Action<ClipEditSettings>(s => s.TrimStartMs = 500)];
        yield return [new Action<ClipEditSettings>(s => s.TrimEndMs = 1000)];
        yield return [new Action<ClipEditSettings>(s => s.PitchSemitones = 3.5)];
        yield return [new Action<ClipEditSettings>(s => s.GainDb = -6)];
        yield return [new Action<ClipEditSettings>(s => s.FadeInMs = 200)];
        yield return [new Action<ClipEditSettings>(s => s.FadeOutMs = 200)];
    }

    [Fact]
    public void Compute_PttFieldsDoNotAffectKey()
    {
        var settings = Defaults();
        var withPtt = Defaults(settings.ClipId);
        withPtt.PttEnabled = true;
        withPtt.PttKeyOverride = 65;
        withPtt.PttBeginDelayMsOverride = 100;
        withPtt.PttEndDelayMsOverride = 100;

        var key1 = RenderKeyCalculator.Compute("abc123", settings);
        var key2 = RenderKeyCalculator.Compute("abc123", withPtt);

        key1.ShouldBe(key2);
    }
}
