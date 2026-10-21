using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Models;

namespace SoundboardLite.App.ViewModels;

/// <summary>Global app settings: the push-to-talk defaults every PTT-enabled clip falls back to when
/// it doesn't set its own key/delay overrides, and output latency.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IPlaybackCoordinator _playbackCoordinator;

    public SettingsViewModel(IAppSettingsRepository appSettingsRepository, IPlaybackCoordinator playbackCoordinator)
    {
        _appSettingsRepository = appSettingsRepository;
        _playbackCoordinator = playbackCoordinator;
    }

    [ObservableProperty]
    private int? _defaultPttKey;

    [ObservableProperty]
    private int _defaultBeginDelayMs;

    [ObservableProperty]
    private int _defaultEndDelayMs;

    [ObservableProperty]
    private bool _lowLatencyOutput;

    public bool? DialogResult { get; private set; }
    public event EventHandler? RequestClose;

    public async Task LoadAsync()
    {
        var keyRaw = await _appSettingsRepository.GetAsync(AppSettingsKeys.PttDefaultKey);
        DefaultPttKey = int.TryParse(keyRaw, out var key) ? key : null;
        DefaultBeginDelayMs = await GetIntAsync(AppSettingsKeys.PttDefaultBeginDelayMs);
        DefaultEndDelayMs = await GetIntAsync(AppSettingsKeys.PttDefaultEndDelayMs);

        var lowLatencyRaw = await _appSettingsRepository.GetAsync(AppSettingsKeys.LowLatencyOutput);
        LowLatencyOutput = lowLatencyRaw == bool.TrueString;
    }

    private async Task<int> GetIntAsync(string key)
    {
        var raw = await _appSettingsRepository.GetAsync(key);
        return raw is not null && int.TryParse(raw, out var value) ? value : 0;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (DefaultPttKey is { } key)
        {
            await _appSettingsRepository.SetAsync(AppSettingsKeys.PttDefaultKey, key.ToString());
        }
        await _appSettingsRepository.SetAsync(AppSettingsKeys.PttDefaultBeginDelayMs, DefaultBeginDelayMs.ToString());
        await _appSettingsRepository.SetAsync(AppSettingsKeys.PttDefaultEndDelayMs, DefaultEndDelayMs.ToString());

        _playbackCoordinator.SetLowLatencyMode(LowLatencyOutput);

        DialogResult = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
