using NSubstitute;
using Shouldly;
using SoundboardLite.Audio.Rendering;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Services;

namespace SoundboardLite.Audio.Tests;

public sealed class RenderCacheGcTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly AppPaths _appPaths;
    private readonly IClipRepository _clipRepository = Substitute.For<IClipRepository>();
    private readonly RenderCacheGc _sut;

    public RenderCacheGcTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "SoundboardLiteGcTests_" + Guid.NewGuid());
        _appPaths = new AppPaths(_tempRoot);
        _sut = new RenderCacheGc(_clipRepository, _appPaths);
    }

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    [Fact]
    public async Task RunAsync_DeletesUnreferencedCacheFiles_KeepsReferencedOnes()
    {
        var referencedPath = Path.Combine(_appPaths.CacheDirectory, "referenced.wav");
        var orphanPath = Path.Combine(_appPaths.CacheDirectory, "orphan.wav");
        File.WriteAllBytes(referencedPath, [1, 2, 3]);
        File.WriteAllBytes(orphanPath, [1, 2, 3]);

        _clipRepository.GetAllRenderKeysAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string> { "referenced" });

        await _sut.RunAsync();

        File.Exists(referencedPath).ShouldBeTrue();
        File.Exists(orphanPath).ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_EmptyCacheDirectory_DoesNothing()
    {
        _clipRepository.GetAllRenderKeysAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        await Should.NotThrowAsync(() => _sut.RunAsync());
    }
}
