using NSubstitute;
using Shouldly;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Models;
using SoundboardLite.Core.Services;

namespace SoundboardLite.Core.Tests;

public sealed class PttContextResolverTests
{
    private readonly IAppSettingsRepository _appSettingsRepository = Substitute.For<IAppSettingsRepository>();

    private static ClipEditSettings Defaults(bool pttEnabled = true) => new()
    {
        ClipId = Guid.NewGuid(),
        TrimStartMs = 0,
        TrimEndMs = null,
        PitchSemitones = 0,
        GainDb = 0,
        FadeInMs = 0,
        FadeOutMs = 0,
        PttEnabled = pttEnabled,
        UpdatedUtc = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task ResolveAsync_PttDisabled_ReturnsNull()
    {
        var settings = Defaults(pttEnabled: false);

        var result = await PttContextResolver.ResolveAsync(settings, _appSettingsRepository);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_NoOverrideAndNoGlobalDefault_ReturnsNull()
    {
        var settings = Defaults();
        _appSettingsRepository.GetAsync(AppSettingsKeys.PttDefaultKey, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var result = await PttContextResolver.ResolveAsync(settings, _appSettingsRepository);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_ClipKeyOverride_TakesPrecedenceOverGlobalDefault()
    {
        var settings = Defaults();
        settings.PttKeyOverride = 65; // 'A'
        _appSettingsRepository.GetAsync(AppSettingsKeys.PttDefaultKey, Arg.Any<CancellationToken>())
            .Returns("66"); // 'B' — should never be read since the override wins

        var result = await PttContextResolver.ResolveAsync(settings, _appSettingsRepository);

        result.ShouldNotBeNull();
        result.VirtualKeyCode.ShouldBe(65);
    }

    [Fact]
    public async Task ResolveAsync_NoKeyOverride_FallsBackToGlobalDefault()
    {
        var settings = Defaults();
        _appSettingsRepository.GetAsync(AppSettingsKeys.PttDefaultKey, Arg.Any<CancellationToken>())
            .Returns("66");

        var result = await PttContextResolver.ResolveAsync(settings, _appSettingsRepository);

        result.ShouldNotBeNull();
        result.VirtualKeyCode.ShouldBe(66);
    }

    [Fact]
    public async Task ResolveAsync_DelayOverrides_TakePrecedenceOverGlobalDefaults()
    {
        var settings = Defaults();
        settings.PttKeyOverride = 65;
        settings.PttBeginDelayMsOverride = 123;
        settings.PttEndDelayMsOverride = 456;
        _appSettingsRepository.GetAsync(AppSettingsKeys.PttDefaultBeginDelayMs, Arg.Any<CancellationToken>())
            .Returns("999");
        _appSettingsRepository.GetAsync(AppSettingsKeys.PttDefaultEndDelayMs, Arg.Any<CancellationToken>())
            .Returns("999");

        var result = await PttContextResolver.ResolveAsync(settings, _appSettingsRepository);

        result.ShouldNotBeNull();
        result.BeginDelayMs.ShouldBe(123);
        result.EndDelayMs.ShouldBe(456);
    }

    [Fact]
    public async Task ResolveAsync_NoDelayOverrides_FallsBackToGlobalDefaults()
    {
        var settings = Defaults();
        settings.PttKeyOverride = 65;
        _appSettingsRepository.GetAsync(AppSettingsKeys.PttDefaultBeginDelayMs, Arg.Any<CancellationToken>())
            .Returns("100");
        _appSettingsRepository.GetAsync(AppSettingsKeys.PttDefaultEndDelayMs, Arg.Any<CancellationToken>())
            .Returns("200");

        var result = await PttContextResolver.ResolveAsync(settings, _appSettingsRepository);

        result.ShouldNotBeNull();
        result.BeginDelayMs.ShouldBe(100);
        result.EndDelayMs.ShouldBe(200);
    }

    [Fact]
    public async Task ResolveAsync_MissingOrUnparsableGlobalDelayDefaults_FallBackToZero()
    {
        var settings = Defaults();
        settings.PttKeyOverride = 65;
        _appSettingsRepository.GetAsync(AppSettingsKeys.PttDefaultBeginDelayMs, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _appSettingsRepository.GetAsync(AppSettingsKeys.PttDefaultEndDelayMs, Arg.Any<CancellationToken>())
            .Returns("not-a-number");

        var result = await PttContextResolver.ResolveAsync(settings, _appSettingsRepository);

        result.ShouldNotBeNull();
        result.BeginDelayMs.ShouldBe(0);
        result.EndDelayMs.ShouldBe(0);
    }
}
