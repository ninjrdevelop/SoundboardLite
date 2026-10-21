namespace SoundboardLite.Core.Abstractions;

/// <summary>Resolves the on-disk layout under the app's local data root.</summary>
public interface IAppPaths
{
    string RootDirectory { get; }
    string OriginalsDirectory { get; }
    string CacheDirectory { get; }
    string PeaksDirectory { get; }
    string RuntimeDirectory { get; }
    string DatabaseFilePath { get; }
}
