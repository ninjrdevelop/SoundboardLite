using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SoundboardLite.Core.Services;

namespace SoundboardLite.App.Services;

public sealed record UpdateCheckResult(
    string? AppUpdateMessage,
    string? AppReleaseUrl,
    string? PluginUpdateMessage,
    string? PluginDownloadUrl);

internal sealed class GitHubReleaseAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";
}

internal sealed class GitHubReleaseInfo
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";

    [JsonPropertyName("assets")]
    public List<GitHubReleaseAsset> Assets { get; set; } = [];
}

/// <summary>Checks GitHub's latest release for a newer app build and/or a newer Stream Deck plugin
/// than what's locally installed — notify-only, never auto-downloads or applies anything. Failures
/// (offline, rate-limited, GitHub down) are swallowed; this is a nice-to-have, not worth an error.</summary>
public sealed class UpdateCheckService(HttpClient httpClient)
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/ninjrdevelop/SoundboardLite/releases/latest";

    private static readonly string PluginManifestPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Elgato", "StreamDeck", "Plugins", "com.soundboardlite.streamdeck.sdPlugin", "manifest.json");

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        var release = await TryFetchLatestReleaseAsync(ct);
        if (release is null)
        {
            return new UpdateCheckResult(null, null, null, null);
        }

        string? appMessage = null;
        string? appUrl = null;
        var currentAppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        if (UpdateVersionComparer.IsNewer(release.TagName, currentAppVersion))
        {
            appMessage = $"Soundboard Lite {release.TagName} is available.";
            appUrl = release.HtmlUrl;
        }

        string? pluginMessage = null;
        string? pluginUrl = null;
        var installedPluginVersion = TryReadInstalledPluginVersion();
        if (installedPluginVersion is not null)
        {
            foreach (var asset in release.Assets)
            {
                var assetPluginVersion = UpdateVersionComparer.ExtractPluginVersion(asset.Name);
                if (assetPluginVersion is not null && UpdateVersionComparer.IsNewer(assetPluginVersion, installedPluginVersion))
                {
                    pluginMessage = $"Stream Deck plugin v{assetPluginVersion} is available.";
                    pluginUrl = asset.BrowserDownloadUrl;
                    break;
                }
            }
        }

        return new UpdateCheckResult(appMessage, appUrl, pluginMessage, pluginUrl);
    }

    private async Task<GitHubReleaseInfo?> TryFetchLatestReleaseAsync(CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd("SoundboardLite-UpdateCheck");
            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            return await response.Content.ReadFromJsonAsync<GitHubReleaseInfo>(cancellationToken: ct);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadInstalledPluginVersion()
    {
        try
        {
            if (!File.Exists(PluginManifestPath))
            {
                return null;
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(PluginManifestPath));
            return doc.RootElement.TryGetProperty("Version", out var versionProp) ? versionProp.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
