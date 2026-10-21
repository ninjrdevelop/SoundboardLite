using Shouldly;
using SoundboardLite.Core.Services;

namespace SoundboardLite.Core.Tests;

public sealed class UpdateVersionComparerTests
{
    [Theory]
    [InlineData("v1.1.0", "1.0.0")]
    [InlineData("1.1.0", "v1.0.0")]
    [InlineData("v2.0.0", "v1.9.9")]
    [InlineData("1.0.0.0", "0.9.9")]
    public void IsNewer_WhenLatestIsGreater_ReturnsTrue(string latest, string current)
    {
        UpdateVersionComparer.IsNewer(latest, current).ShouldBeTrue();
    }

    [Theory]
    [InlineData("v1.0.0", "v1.0.0")]
    [InlineData("1.1.0", "1.1.0.0")]
    [InlineData("1.1.0.0", "v1.1.0")]
    [InlineData("1.0.0", "v1.1.0")]
    [InlineData("v1.0.0", "1.2.0")]
    public void IsNewer_WhenLatestIsNotGreater_ReturnsFalse(string latest, string current)
    {
        UpdateVersionComparer.IsNewer(latest, current).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, "1.0.0")]
    [InlineData("1.0.0", null)]
    [InlineData("not-a-version", "1.0.0")]
    [InlineData("1.0.0", "garbage")]
    public void IsNewer_WithUnparsableInput_ReturnsFalse(string? latest, string? current)
    {
        UpdateVersionComparer.IsNewer(latest, current).ShouldBeFalse();
    }

    [Fact]
    public void ExtractPluginVersion_MatchingAssetName_ReturnsVersion()
    {
        var result = UpdateVersionComparer.ExtractPluginVersion("com.soundboardlite.streamdeck-v1.2.3.streamDeckPlugin");

        result.ShouldBe("1.2.3");
    }

    [Theory]
    [InlineData("SoundboardLite-v1.2.3-win-x64.zip")]
    [InlineData("com.soundboardlite.streamdeck.streamDeckPlugin")]
    [InlineData("com.soundboardlite.streamdeck-v1.2.streamDeckPlugin")]
    public void ExtractPluginVersion_NonMatchingAssetName_ReturnsNull(string assetFileName)
    {
        UpdateVersionComparer.ExtractPluginVersion(assetFileName).ShouldBeNull();
    }
}
