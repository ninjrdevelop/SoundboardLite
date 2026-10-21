using System.Security.Cryptography;
using NAudio.Wave;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Models;

namespace SoundboardLite.Audio;

public sealed class ClipImportService : IClipImportService
{
    private readonly IAppPaths _appPaths;
    private readonly IOriginalFileRepository _originalFileRepository;
    private readonly IClipRepository _clipRepository;
    private readonly IBoardRepository _boardRepository;
    private readonly IWaveformPeakService _peakService;

    public ClipImportService(
        IAppPaths appPaths,
        IOriginalFileRepository originalFileRepository,
        IClipRepository clipRepository,
        IBoardRepository boardRepository,
        IWaveformPeakService peakService)
    {
        _appPaths = appPaths;
        _originalFileRepository = originalFileRepository;
        _clipRepository = clipRepository;
        _boardRepository = boardRepository;
        _peakService = peakService;
    }

    public async Task<Clip> ImportAsync(string sourceFilePath, Guid boardId, CancellationToken ct = default)
    {
        if (await _boardRepository.GetAsync(boardId, ct) is null)
            throw new InvalidOperationException($"Board {boardId} does not exist.");

        var extension = Path.GetExtension(sourceFilePath);
        var hash = await ComputeSha256Async(sourceFilePath, ct);
        var targetPath = Path.Combine(_appPaths.OriginalsDirectory, hash + extension);

        if (!File.Exists(targetPath))
        {
            // Copy to a temp name first then move, so a crash mid-copy never leaves a half-written
            // file sitting at the hash's canonical path for a future import to "dedupe" onto.
            var tempPath = targetPath + ".importing";
            await using (var source = File.OpenRead(sourceFilePath))
            await using (var dest = File.Create(tempPath))
            {
                await source.CopyToAsync(dest, ct);
            }
            File.Move(tempPath, targetPath, overwrite: true);
        }

        var sizeBytes = new FileInfo(targetPath).Length;
        await _originalFileRepository.AddReferenceAsync(hash, targetPath, sizeBytes, ct);

        var durationMs = ProbeDurationMs(targetPath);

        var now = DateTimeOffset.UtcNow;
        var nextSortOrder = (await _clipRepository.ListByBoardAsync(boardId, ct)).Count;

        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            Name = Path.GetFileNameWithoutExtension(sourceFilePath),
            OriginalFileHash = hash,
            OriginalFileExtension = extension,
            OriginalDurationMs = durationMs,
            SortOrder = nextSortOrder,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        var editSettings = ClipEditSettings.CreateDefault(clip.Id, now);

        await _clipRepository.CreateAsync(clip, editSettings, ct);

        // Fire-and-forget: the waveform editor just shows "no peaks yet" until this lands, it never
        // blocks import. Swallow failures — a missing waveform isn't fatal to anything else.
        _ = Task.Run(async () =>
        {
            try
            {
                await _peakService.EnsurePeaksAsync(hash, targetPath, CancellationToken.None);
            }
            catch
            {
                // best-effort background job
            }
        });

        return clip;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexStringLower(hashBytes);
    }

    private static long ProbeDurationMs(string filePath)
    {
        using var reader = new MediaFoundationReader(filePath);
        return (long)(reader.Length / (double)reader.WaveFormat.AverageBytesPerSecond * 1000);
    }
}
