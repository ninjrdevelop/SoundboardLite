using Shouldly;
using SoundboardLite.Core.Models;
using SoundboardLite.Core.Services;
using SoundboardLite.Data;
using SoundboardLite.Data.Repositories;

namespace SoundboardLite.Data.Tests;

public sealed class BoardAndClipRepositoryTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly SqliteConnectionFactory _connectionFactory;

    public BoardAndClipRepositoryTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "SoundboardLiteTests_" + Guid.NewGuid());
        var appPaths = new AppPaths(_tempRoot);
        _connectionFactory = new SqliteConnectionFactory(appPaths);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public async Task CreateBoard_RoundTripsThroughSqlite()
    {
        var repo = new BoardRepository(_connectionFactory);

        var created = await repo.CreateAsync("My Board");
        var fetched = await repo.GetAsync(created.Id);

        fetched.ShouldNotBeNull();
        fetched.Id.ShouldBe(created.Id);
        fetched.Name.ShouldBe("My Board");
        fetched.CreatedUtc.ShouldBe(created.CreatedUtc);
    }

    [Fact]
    public async Task CreateClip_PersistsEditSettingsIncludingBoolAndNullableFields()
    {
        var boardRepo = new BoardRepository(_connectionFactory);
        var clipRepo = new ClipRepository(_connectionFactory);

        var board = await boardRepo.CreateAsync("Board");
        var originalFileRepo = new OriginalFileRepository(_connectionFactory);
        var now = DateTimeOffset.UtcNow;
        await originalFileRepo.AddReferenceAsync("abc123", @"C:\originals\abc123.wav", 1000);

        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "Clip 1",
            OriginalFileHash = "abc123",
            OriginalFileExtension = ".wav",
            OriginalDurationMs = 5000,
            SortOrder = 0,
            ColorTag = null,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        var settings = ClipEditSettings.CreateDefault(clip.Id, now);
        settings.PttEnabled = true;
        settings.PttKeyOverride = 0x20;

        await clipRepo.CreateAsync(clip, settings);

        var fetchedClip = await clipRepo.GetAsync(clip.Id);
        var fetchedSettings = await clipRepo.GetEditSettingsAsync(clip.Id);

        fetchedClip.ShouldNotBeNull();
        fetchedClip.BoardId.ShouldBe(board.Id);

        fetchedSettings.ShouldNotBeNull();
        fetchedSettings.PttEnabled.ShouldBeTrue();
        fetchedSettings.PttKeyOverride.ShouldBe(0x20);
        fetchedSettings.TrimEndMs.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteClip_ReturnsOriginalFileHashForRefCounting()
    {
        var boardRepo = new BoardRepository(_connectionFactory);
        var clipRepo = new ClipRepository(_connectionFactory);
        var board = await boardRepo.CreateAsync("Board");
        var originalFileRepo = new OriginalFileRepository(_connectionFactory);
        var now = DateTimeOffset.UtcNow;
        await originalFileRepo.AddReferenceAsync("hash-xyz", @"C:\originals\hash-xyz.mp3", 2000);
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "Clip",
            OriginalFileHash = "hash-xyz",
            OriginalFileExtension = ".mp3",
            OriginalDurationMs = 1234,
            SortOrder = 0,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        await clipRepo.CreateAsync(clip, ClipEditSettings.CreateDefault(clip.Id, now));

        var returnedHash = await clipRepo.DeleteAsync(clip.Id);

        returnedHash.ShouldBe("hash-xyz");
        (await clipRepo.GetAsync(clip.Id)).ShouldBeNull();
    }
}
