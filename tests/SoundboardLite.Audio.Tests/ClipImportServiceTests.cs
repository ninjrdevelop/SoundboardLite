using NAudio.Wave;
using NSubstitute;
using Shouldly;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Models;
using SoundboardLite.Core.Services;

namespace SoundboardLite.Audio.Tests;

public sealed class ClipImportServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly AppPaths _appPaths;
    private readonly IBoardRepository _boardRepository = Substitute.For<IBoardRepository>();
    private readonly IClipRepository _clipRepository = Substitute.For<IClipRepository>();
    private readonly IOriginalFileRepository _originalFileRepository = Substitute.For<IOriginalFileRepository>();
    private readonly IWaveformPeakService _peakService = Substitute.For<IWaveformPeakService>();
    private readonly ClipImportService _sut;
    private readonly Guid _boardId = Guid.NewGuid();

    public ClipImportServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "SoundboardLiteAudioTests_" + Guid.NewGuid());
        _appPaths = new AppPaths(_tempRoot);

        _boardRepository.GetAsync(_boardId, Arg.Any<CancellationToken>())
            .Returns(new Board { Id = _boardId, Name = "Board", SortOrder = 0, CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow });
        _clipRepository.ListByBoardAsync(_boardId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Clip>());

        _sut = new ClipImportService(_appPaths, _originalFileRepository, _clipRepository, _boardRepository, _peakService);
    }

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    private static string WriteSilentWavFixture(string path, int milliseconds = 100)
    {
        var format = new WaveFormat(8000, 16, 1);
        using var writer = new WaveFileWriter(path, format);
        var silence = new byte[format.AverageBytesPerSecond * milliseconds / 1000];
        writer.Write(silence, 0, silence.Length);
        return path;
    }

    [Fact]
    public async Task ImportAsync_CopiesFileToOriginalsByHash_AndProbesDuration()
    {
        var sourcePath = WriteSilentWavFixture(Path.Combine(_tempRoot, "source.wav"));
        var expectedHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(sourcePath)));

        var clip = await _sut.ImportAsync(sourcePath, _boardId);

        clip.OriginalFileHash.ShouldBe(expectedHash);
        clip.OriginalFileExtension.ShouldBe(".wav");
        clip.OriginalDurationMs.ShouldBeInRange(90, 110);

        var expectedTargetPath = Path.Combine(_appPaths.OriginalsDirectory, expectedHash + ".wav");
        File.Exists(expectedTargetPath).ShouldBeTrue();

        await _originalFileRepository.Received(1).AddReferenceAsync(expectedHash, expectedTargetPath, Arg.Any<long>(), Arg.Any<CancellationToken>());
        await _clipRepository.Received(1).CreateAsync(Arg.Is<Clip>(c => c.Id == clip.Id), Arg.Any<ClipEditSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportAsync_SameContentTwice_DedupesInsteadOfDuplicatingTheOriginalFile()
    {
        var sourcePath1 = WriteSilentWavFixture(Path.Combine(_tempRoot, "a.wav"));
        var sourcePath2 = WriteSilentWavFixture(Path.Combine(_tempRoot, "b.wav"));

        var clip1 = await _sut.ImportAsync(sourcePath1, _boardId);
        var clip2 = await _sut.ImportAsync(sourcePath2, _boardId);

        clip1.OriginalFileHash.ShouldBe(clip2.OriginalFileHash);

        Directory.GetFiles(_appPaths.OriginalsDirectory).Length.ShouldBe(1);
        await _originalFileRepository.Received(2).AddReferenceAsync(
            clip1.OriginalFileHash, Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportAsync_UnknownBoard_Throws()
    {
        var sourcePath = WriteSilentWavFixture(Path.Combine(_tempRoot, "source.wav"));
        _boardRepository.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Board?)null);

        await Should.ThrowAsync<InvalidOperationException>(() => _sut.ImportAsync(sourcePath, Guid.NewGuid()));
    }
}
