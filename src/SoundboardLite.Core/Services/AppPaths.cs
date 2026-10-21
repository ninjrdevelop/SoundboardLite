using SoundboardLite.Core.Abstractions;

namespace SoundboardLite.Core.Services;

public sealed class AppPaths : IAppPaths
{
    public string RootDirectory { get; }
    public string OriginalsDirectory { get; }
    public string CacheDirectory { get; }
    public string PeaksDirectory { get; }
    public string RuntimeDirectory { get; }
    public string DatabaseFilePath { get; }

    /// <param name="rootDirectory">
    /// Defaults to %LOCALAPPDATA%\SoundboardLite. Overridable so tests can point at a temp directory.
    /// </param>
    public AppPaths(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundboardLite");
        OriginalsDirectory = Path.Combine(RootDirectory, "originals");
        CacheDirectory = Path.Combine(RootDirectory, "cache");
        PeaksDirectory = Path.Combine(RootDirectory, "peaks");
        RuntimeDirectory = Path.Combine(RootDirectory, "runtime");
        DatabaseFilePath = Path.Combine(RootDirectory, "library.db");

        foreach (var dir in new[] { RootDirectory, OriginalsDirectory, CacheDirectory, PeaksDirectory, RuntimeDirectory })
        {
            Directory.CreateDirectory(dir);
        }
    }
}
