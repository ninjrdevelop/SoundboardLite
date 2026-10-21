using System.Text.Json;
using SoundboardLite.Core.Abstractions;

namespace SoundboardLite.Api;

/// <summary>
/// Reads/writes <c>runtime/api-session.json</c> — the file the Stream Deck plugin polls to discover
/// the embedded API's port/token/pid. Written atomically (temp file + move) so the plugin never reads
/// a half-written file; deleted on graceful shutdown so a stale file can't be mistaken for a running app.
/// </summary>
public static class ApiSessionFile
{
    public static void Write(IAppPaths appPaths, ApiSession session)
    {
        var path = GetPath(appPaths);
        var tempPath = path + ".writing";
        var json = JsonSerializer.SerializeToUtf8Bytes(session, ApiJsonContext.Default.ApiSession);
        File.WriteAllBytes(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    public static void Delete(IAppPaths appPaths)
    {
        var path = GetPath(appPaths);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string GetPath(IAppPaths appPaths) => Path.Combine(appPaths.RuntimeDirectory, "api-session.json");
}
