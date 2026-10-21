using Dapper;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Models;

namespace SoundboardLite.Data.Repositories;

public sealed class ClipRepository : IClipRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ClipRepository(SqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<Clip>> ListByBoardAsync(Guid boardId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        var clips = await connection.QueryAsync<Clip>(
            """
            SELECT Id, BoardId, Name, OriginalFileHash, OriginalFileExtension, OriginalDurationMs,
                   SortOrder, ColorTag, CreatedUtc, UpdatedUtc
            FROM Clips WHERE BoardId = @boardId ORDER BY SortOrder;
            """,
            new { boardId = boardId.ToString() });
        return clips.ToList();
    }

    public async Task<Clip?> GetAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        return await connection.QuerySingleOrDefaultAsync<Clip>(
            """
            SELECT Id, BoardId, Name, OriginalFileHash, OriginalFileExtension, OriginalDurationMs,
                   SortOrder, ColorTag, CreatedUtc, UpdatedUtc
            FROM Clips WHERE Id = @id;
            """,
            new { id = id.ToString() });
    }

    public async Task<ClipEditSettings?> GetEditSettingsAsync(Guid clipId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        return await connection.QuerySingleOrDefaultAsync<ClipEditSettings>(
            """
            SELECT ClipId, TrimStartMs, TrimEndMs, PitchSemitones, GainDb, FadeInMs, FadeOutMs,
                   CurrentRenderKey, PttEnabled, PttKeyOverride, PttBeginDelayMsOverride,
                   PttEndDelayMsOverride, UpdatedUtc
            FROM ClipEditSettings WHERE ClipId = @clipId;
            """,
            new { clipId = clipId.ToString() });
    }

    public async Task<IReadOnlyCollection<string>> GetAllRenderKeysAsync(CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        var keys = await connection.QueryAsync<string>(
            "SELECT CurrentRenderKey FROM ClipEditSettings WHERE CurrentRenderKey IS NOT NULL;");
        return keys.ToList();
    }

    public async Task CreateAsync(Clip clip, ClipEditSettings editSettings, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            """
            INSERT INTO Clips (Id, BoardId, Name, OriginalFileHash, OriginalFileExtension,
                                OriginalDurationMs, SortOrder, ColorTag, CreatedUtc, UpdatedUtc)
            VALUES (@Id, @BoardId, @Name, @OriginalFileHash, @OriginalFileExtension,
                    @OriginalDurationMs, @SortOrder, @ColorTag, @CreatedUtc, @UpdatedUtc);
            """,
            new
            {
                Id = clip.Id.ToString(),
                BoardId = clip.BoardId.ToString(),
                clip.Name,
                clip.OriginalFileHash,
                clip.OriginalFileExtension,
                clip.OriginalDurationMs,
                clip.SortOrder,
                clip.ColorTag,
                clip.CreatedUtc,
                clip.UpdatedUtc,
            },
            transaction);

        await connection.ExecuteAsync(
            """
            INSERT INTO ClipEditSettings (ClipId, TrimStartMs, TrimEndMs, PitchSemitones, GainDb,
                                           FadeInMs, FadeOutMs, CurrentRenderKey, PttEnabled,
                                           PttKeyOverride, PttBeginDelayMsOverride, PttEndDelayMsOverride, UpdatedUtc)
            VALUES (@ClipId, @TrimStartMs, @TrimEndMs, @PitchSemitones, @GainDb,
                    @FadeInMs, @FadeOutMs, @CurrentRenderKey, @PttEnabled,
                    @PttKeyOverride, @PttBeginDelayMsOverride, @PttEndDelayMsOverride, @UpdatedUtc);
            """,
            new
            {
                ClipId = editSettings.ClipId.ToString(),
                editSettings.TrimStartMs,
                editSettings.TrimEndMs,
                editSettings.PitchSemitones,
                editSettings.GainDb,
                editSettings.FadeInMs,
                editSettings.FadeOutMs,
                editSettings.CurrentRenderKey,
                editSettings.PttEnabled,
                editSettings.PttKeyOverride,
                editSettings.PttBeginDelayMsOverride,
                editSettings.PttEndDelayMsOverride,
                editSettings.UpdatedUtc,
            },
            transaction);

        transaction.Commit();
    }

    public async Task UpdateAsync(Clip clip, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        await connection.ExecuteAsync(
            """
            UPDATE Clips SET Name = @Name, ColorTag = @ColorTag, UpdatedUtc = @UpdatedUtc
            WHERE Id = @Id;
            """,
            new { Id = clip.Id.ToString(), clip.Name, clip.ColorTag, clip.UpdatedUtc });
    }

    public async Task UpdateEditSettingsAsync(ClipEditSettings editSettings, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        await connection.ExecuteAsync(
            """
            UPDATE ClipEditSettings
            SET TrimStartMs = @TrimStartMs, TrimEndMs = @TrimEndMs, PitchSemitones = @PitchSemitones,
                GainDb = @GainDb, FadeInMs = @FadeInMs, FadeOutMs = @FadeOutMs,
                CurrentRenderKey = @CurrentRenderKey, PttEnabled = @PttEnabled,
                PttKeyOverride = @PttKeyOverride, PttBeginDelayMsOverride = @PttBeginDelayMsOverride,
                PttEndDelayMsOverride = @PttEndDelayMsOverride, UpdatedUtc = @UpdatedUtc
            WHERE ClipId = @ClipId;
            """,
            new
            {
                ClipId = editSettings.ClipId.ToString(),
                editSettings.TrimStartMs,
                editSettings.TrimEndMs,
                editSettings.PitchSemitones,
                editSettings.GainDb,
                editSettings.FadeInMs,
                editSettings.FadeOutMs,
                editSettings.CurrentRenderKey,
                editSettings.PttEnabled,
                editSettings.PttKeyOverride,
                editSettings.PttBeginDelayMsOverride,
                editSettings.PttEndDelayMsOverride,
                editSettings.UpdatedUtc,
            });
    }

    public async Task ReorderAsync(Guid id, int sortOrder, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        await connection.ExecuteAsync(
            "UPDATE Clips SET SortOrder = @sortOrder, UpdatedUtc = @now WHERE Id = @id;",
            new { id = id.ToString(), sortOrder, now = DateTimeOffset.UtcNow });
    }

    public async Task<string> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.Open();
        var hash = await connection.ExecuteScalarAsync<string>(
            "SELECT OriginalFileHash FROM Clips WHERE Id = @id;", new { id = id.ToString() });
        await connection.ExecuteAsync("DELETE FROM Clips WHERE Id = @id;", new { id = id.ToString() });
        return hash ?? throw new InvalidOperationException($"Clip {id} not found.");
    }
}
