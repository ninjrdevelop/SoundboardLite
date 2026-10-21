using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Models;

namespace SoundboardLite.App.ViewModels;

/// <summary>
/// Backs the non-destructive clip editor: loads the clip's current trim/pitch/gain/fade settings
/// and its waveform peaks, lets the user preview the edit against a throwaway render, and on Save
/// persists the settings and hands off to the background render queue (the original file is never
/// touched by any of this).
/// </summary>
public sealed partial class ClipEditorViewModel : ObservableObject
{
    private readonly Clip _clip;
    private readonly IClipRepository _clipRepository;
    private readonly IOriginalFileRepository _originalFileRepository;
    private readonly IWaveformPeakService _peakService;
    private readonly IClipRenderer _clipRenderer;
    private readonly IRenderQueue _renderQueue;
    private readonly IPlaybackCoordinator _playbackCoordinator;
    private readonly IAppPaths _appPaths;

    private string? _originalFilePath;

    public ClipEditorViewModel(
        Clip clip,
        IClipRepository clipRepository,
        IOriginalFileRepository originalFileRepository,
        IWaveformPeakService peakService,
        IClipRenderer clipRenderer,
        IRenderQueue renderQueue,
        IPlaybackCoordinator playbackCoordinator,
        IAppPaths appPaths)
    {
        _clip = clip;
        _clipRepository = clipRepository;
        _originalFileRepository = originalFileRepository;
        _peakService = peakService;
        _clipRenderer = clipRenderer;
        _renderQueue = renderQueue;
        _playbackCoordinator = playbackCoordinator;
        _appPaths = appPaths;

        _trimEndMs = clip.OriginalDurationMs;
        _name = clip.Name;
    }

    public double OriginalDurationMs => _clip.OriginalDurationMs;

    public bool? DialogResult { get; private set; }
    public event EventHandler? RequestClose;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private PeakData? _peaks;

    [ObservableProperty]
    private double _trimStartMs;

    [ObservableProperty]
    private double _trimEndMs;

    [ObservableProperty]
    private double _pitchSemitones;

    [ObservableProperty]
    private double _gainDb;

    [ObservableProperty]
    private double _fadeInMs;

    [ObservableProperty]
    private double _fadeOutMs;

    [ObservableProperty]
    private bool _pttEnabled;

    [ObservableProperty]
    private int? _pttKeyOverride;

    [ObservableProperty]
    private int? _pttBeginDelayMsOverride;

    [ObservableProperty]
    private int? _pttEndDelayMsOverride;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    public async Task LoadAsync()
    {
        var settings = await _clipRepository.GetEditSettingsAsync(_clip.Id)
            ?? ClipEditSettings.CreateDefault(_clip.Id, DateTimeOffset.UtcNow);

        TrimStartMs = settings.TrimStartMs;
        TrimEndMs = settings.TrimEndMs ?? _clip.OriginalDurationMs;
        PitchSemitones = settings.PitchSemitones;
        GainDb = settings.GainDb;
        FadeInMs = settings.FadeInMs;
        FadeOutMs = settings.FadeOutMs;
        PttEnabled = settings.PttEnabled;
        PttKeyOverride = settings.PttKeyOverride;
        PttBeginDelayMsOverride = settings.PttBeginDelayMsOverride;
        PttEndDelayMsOverride = settings.PttEndDelayMsOverride;

        var original = await _originalFileRepository.GetAsync(_clip.OriginalFileHash);
        _originalFilePath = original?.FilePath;

        if (_originalFilePath is not null)
        {
            await _peakService.EnsurePeaksAsync(_clip.OriginalFileHash, _originalFilePath);
        }
        Peaks = _peakService.LoadPeaks(_clip.OriginalFileHash);
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (_originalFilePath is null)
        {
            StatusMessage = "Original file is missing.";
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var request = new ClipRenderRequest(
                _originalFilePath,
                (long)TrimStartMs,
                (long)TrimEndMs,
                PitchSemitones,
                GainDb,
                (long)FadeInMs,
                (long)FadeOutMs);

            var previewPath = Path.Combine(_appPaths.RuntimeDirectory, $"preview-{_clip.Id}.wav");
            await _clipRenderer.RenderAsync(request, previewPath);
            await _playbackCoordinator.PlayPreviewFileAsync(previewPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Preview failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var trimmedName = Name.Trim();
        if (trimmedName.Length > 0 && trimmedName != _clip.Name)
        {
            _clip.Name = trimmedName;
            _clip.UpdatedUtc = DateTimeOffset.UtcNow;
            await _clipRepository.UpdateAsync(_clip);
        }

        var settings = await _clipRepository.GetEditSettingsAsync(_clip.Id)
            ?? ClipEditSettings.CreateDefault(_clip.Id, DateTimeOffset.UtcNow);

        settings.TrimStartMs = (long)TrimStartMs;
        settings.TrimEndMs = TrimEndMs >= _clip.OriginalDurationMs ? null : (long)TrimEndMs;
        settings.PitchSemitones = PitchSemitones;
        settings.GainDb = GainDb;
        settings.FadeInMs = (long)FadeInMs;
        settings.FadeOutMs = (long)FadeOutMs;
        settings.PttEnabled = PttEnabled;
        settings.PttKeyOverride = PttKeyOverride;
        settings.PttBeginDelayMsOverride = PttBeginDelayMsOverride;
        settings.PttEndDelayMsOverride = PttEndDelayMsOverride;
        settings.UpdatedUtc = DateTimeOffset.UtcNow;

        await _clipRepository.UpdateEditSettingsAsync(settings);
        _renderQueue.RequestRender(_clip.Id);

        DialogResult = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        _playbackCoordinator.StopPreview();
        DialogResult = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
