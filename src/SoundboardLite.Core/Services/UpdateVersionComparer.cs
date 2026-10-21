using System.Text.RegularExpressions;

namespace SoundboardLite.Core.Services;

/// <summary>Pure version-string parsing/comparison for the update-check feature — no network or file I/O.</summary>
public static partial class UpdateVersionComparer
{
    public static bool IsNewer(string? latest, string? current)
    {
        if (!TryParse(latest, out var latestVersion) || !TryParse(current, out var currentVersion))
        {
            return false;
        }
        return latestVersion > currentVersion;
    }

    /// <summary>Parses a version string into exactly major.minor.patch, normalizing away any 4th
    /// ("build") segment or missing segments — the plugin manifest's version is always 4-part
    /// (`{major}.{minor}.{patch}.{build}`, required by Elgato's schema) while release tags and the
    /// app's own assembly version are 3-part; comparing unnormalized `System.Version` values treats a
    /// missing segment as -1, which would make "1.1.0.0" spuriously compare greater than "1.1.0".</summary>
    public static bool TryParse(string? value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.TrimStart('v', 'V');
        if (!Version.TryParse(trimmed, out var parsed))
        {
            return false;
        }

        version = new Version(Math.Max(parsed.Major, 0), Math.Max(parsed.Minor, 0), Math.Max(parsed.Build, 0));
        return true;
    }

    /// <summary>Extracts the embedded version from a release asset named like
    /// `com.soundboardlite.streamdeck-v1.2.3.streamDeckPlugin` (see release.yml) — the plugin's own
    /// version is tracked independently of the app's/the release tag's, since a given release may
    /// touch only the app and ship the plugin unchanged.</summary>
    public static string? ExtractPluginVersion(string assetFileName)
    {
        var match = PluginAssetPattern().Match(assetFileName);
        return match.Success ? match.Groups["version"].Value : null;
    }

    [GeneratedRegex(@"^com\.soundboardlite\.streamdeck-v(?<version>\d+\.\d+\.\d+)\.streamDeckPlugin$")]
    private static partial Regex PluginAssetPattern();
}
