using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SoundboardLite.Core.Models;

namespace SoundboardLite.Core.Services;

/// <summary>Computes the hash of (OriginalFileHash + render-affecting settings) that keys a clip's cache/{key}.wav.</summary>
public static class RenderKeyCalculator
{
    public static string Compute(string originalFileHash, ClipEditSettings settings)
    {
        var parts = new[]
        {
            originalFileHash,
            settings.TrimStartMs.ToString(CultureInfo.InvariantCulture),
            settings.TrimEndMs?.ToString(CultureInfo.InvariantCulture) ?? "null",
            settings.PitchSemitones.ToString("R", CultureInfo.InvariantCulture),
            settings.GainDb.ToString("R", CultureInfo.InvariantCulture),
            settings.FadeInMs.ToString(CultureInfo.InvariantCulture),
            settings.FadeOutMs.ToString(CultureInfo.InvariantCulture),
        };
        var bytes = Encoding.UTF8.GetBytes(string.Join('|', parts));
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
