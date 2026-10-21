using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Models;

namespace SoundboardLite.Core.Services;

/// <summary>Resolves a clip's effective push-to-talk key/delays: per-clip override wins, falling
/// back to the global AppSettings default. Returns null if PTT isn't enabled for the clip, or if no
/// key is configured anywhere (neither a clip override nor a global default) — PTT silently no-ops
/// rather than failing the trigger.</summary>
public static class PttContextResolver
{
    public static async Task<PttContext?> ResolveAsync(
        ClipEditSettings settings, IAppSettingsRepository appSettingsRepository, CancellationToken ct = default)
    {
        if (!settings.PttEnabled) return null;

        var virtualKeyCode = settings.PttKeyOverride;
        if (virtualKeyCode is null)
        {
            var defaultKeyRaw = await appSettingsRepository.GetAsync(AppSettingsKeys.PttDefaultKey, ct);
            if (defaultKeyRaw is null || !int.TryParse(defaultKeyRaw, out var parsedKey)) return null;
            virtualKeyCode = parsedKey;
        }

        var beginDelayMs = settings.PttBeginDelayMsOverride
            ?? await GetIntSettingAsync(appSettingsRepository, AppSettingsKeys.PttDefaultBeginDelayMs, ct);
        var endDelayMs = settings.PttEndDelayMsOverride
            ?? await GetIntSettingAsync(appSettingsRepository, AppSettingsKeys.PttDefaultEndDelayMs, ct);

        return new PttContext(virtualKeyCode.Value, beginDelayMs, endDelayMs);
    }

    private static async Task<int> GetIntSettingAsync(IAppSettingsRepository repository, string key, CancellationToken ct)
    {
        var raw = await repository.GetAsync(key, ct);
        return raw is not null && int.TryParse(raw, out var value) ? value : 0;
    }
}
