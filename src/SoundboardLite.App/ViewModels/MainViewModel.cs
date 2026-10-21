using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SoundboardLite.Api;
using SoundboardLite.App.Services;
using SoundboardLite.App.Views;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Models;

namespace SoundboardLite.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IBoardRepository _boardRepository;
    private readonly IClipRepository _clipRepository;
    private readonly IOriginalFileRepository _originalFileRepository;
    private readonly IClipImportService _clipImportService;
    private readonly IPlaybackCoordinator _playbackCoordinator;
    private readonly ClipEditorWindowFactory _clipEditorWindowFactory;
    private readonly SettingsWindowFactory _settingsWindowFactory;
    private readonly ApiHostedService _apiHostedService;
    private readonly IRenderQueue _renderQueue;

    public MainViewModel(
        IBoardRepository boardRepository,
        IClipRepository clipRepository,
        IOriginalFileRepository originalFileRepository,
        IClipImportService clipImportService,
        IPlaybackCoordinator playbackCoordinator,
        ClipEditorWindowFactory clipEditorWindowFactory,
        SettingsWindowFactory settingsWindowFactory,
        ApiHostedService apiHostedService,
        IRenderQueue renderQueue)
    {
        _boardRepository = boardRepository;
        _clipRepository = clipRepository;
        _originalFileRepository = originalFileRepository;
        _clipImportService = clipImportService;
        _playbackCoordinator = playbackCoordinator;
        _clipEditorWindowFactory = clipEditorWindowFactory;
        _settingsWindowFactory = settingsWindowFactory;
        _apiHostedService = apiHostedService;
        _renderQueue = renderQueue;

        // RenderQueue completes on a background task, not the UI thread — this is the one place a
        // render failure (bad/corrupted source, disk full, etc.) becomes visible to the user; without
        // this it would only ever show up in the Serilog file the user never looks at.
        _renderQueue.RenderCompleted += (_, args) =>
        {
            if (args.Success) return;
            Application.Current.Dispatcher.Invoke(() => StatusMessage = $"Failed to render clip: {args.ErrorMessage}");
        };
    }

    public ObservableCollection<BoardItemViewModel> Boards { get; } = [];
    public ObservableCollection<OutputDeviceInfo> OutputDevices { get; } = [];

    [ObservableProperty]
    private BoardItemViewModel? _selectedBoard;

    [ObservableProperty]
    private OutputDeviceInfo? _selectedOutputDevice;

    [ObservableProperty]
    private string _newBoardName = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _appUpdateMessage;

    [ObservableProperty]
    private string? _pluginUpdateMessage;

    private string? _appReleaseUrl;
    private string? _pluginDownloadUrl;

    public void ApplyUpdateCheckResult(UpdateCheckResult result)
    {
        AppUpdateMessage = result.AppUpdateMessage;
        _appReleaseUrl = result.AppReleaseUrl;
        PluginUpdateMessage = result.PluginUpdateMessage;
        _pluginDownloadUrl = result.PluginDownloadUrl;
    }

    [RelayCommand]
    private void OpenAppRelease()
    {
        if (_appReleaseUrl is not null)
        {
            Process.Start(new ProcessStartInfo(_appReleaseUrl) { UseShellExecute = true });
        }
    }

    [RelayCommand]
    private void OpenPluginDownload()
    {
        if (_pluginDownloadUrl is not null)
        {
            Process.Start(new ProcessStartInfo(_pluginDownloadUrl) { UseShellExecute = true });
        }
    }

    public async Task LoadAsync()
    {
        var boards = await _boardRepository.ListAsync();
        Boards.Clear();
        foreach (var board in boards)
        {
            var boardVm = new BoardItemViewModel(board, DeleteBoardAsync, RenameBoardAsync);
            await LoadClipsIntoAsync(boardVm);
            Boards.Add(boardVm);
        }
        SelectedBoard = Boards.FirstOrDefault();
    }

    public void RefreshOutputDevices()
    {
        OutputDevices.Clear();
        foreach (var device in _playbackCoordinator.GetOutputDevices())
        {
            OutputDevices.Add(device);
        }
        SelectedOutputDevice =
            OutputDevices.FirstOrDefault(d => d.Id == _playbackCoordinator.CurrentOutputDeviceId)
            ?? OutputDevices.FirstOrDefault(d => d.IsDefault);
    }

    private async Task LoadClipsIntoAsync(BoardItemViewModel boardVm)
    {
        var clips = await _clipRepository.ListByBoardAsync(boardVm.Id);
        boardVm.Clips.Clear();
        foreach (var clip in clips)
        {
            boardVm.Clips.Add(new ClipItemViewModel(clip, TriggerClipAsync, StopClip, OpenClipEditor));
        }
    }

    [RelayCommand]
    private async Task CreateBoardAsync()
    {
        var name = NewBoardName.Trim();
        if (name.Length == 0) return;

        var board = await _boardRepository.CreateAsync(name);
        var boardVm = new BoardItemViewModel(board, DeleteBoardAsync, RenameBoardAsync);
        Boards.Add(boardVm);
        SelectedBoard = boardVm;
        NewBoardName = string.Empty;

        _ = _apiHostedService.BroadcastBoardChangedAsync(board.Id);
    }

    private async Task DeleteBoardAsync(BoardItemViewModel board)
    {
        var result = MessageBox.Show(
            $"Delete the soundboard \"{board.Name}\" and all its clips? This cannot be undone.",
            "Delete Soundboard",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        await _boardRepository.DeleteAsync(board.Id);
        Boards.Remove(board);
        if (SelectedBoard == board)
        {
            SelectedBoard = Boards.FirstOrDefault();
        }

        _ = _apiHostedService.BroadcastBoardChangedAsync(board.Id);
    }

    private async Task RenameBoardAsync(BoardItemViewModel board, string newName)
    {
        await _boardRepository.RenameAsync(board.Id, newName);
        board.Name = newName;
        _ = _apiHostedService.BroadcastBoardChangedAsync(board.Id);
    }

    [RelayCommand]
    private async Task ImportClipAsync()
    {
        if (SelectedBoard is null)
        {
            StatusMessage = "Create or select a board before importing a clip.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Import sound clip",
            Filter = "Audio files|*.wav;*.mp3;*.m4a;*.aac;*.wma;*.flac|All files|*.*",
            Multiselect = true,
        };
        if (dialog.ShowDialog() != true) return;

        var board = SelectedBoard;
        foreach (var path in dialog.FileNames)
        {
            try
            {
                var clip = await _clipImportService.ImportAsync(path, board.Id);
                board.Clips.Add(new ClipItemViewModel(clip, TriggerClipAsync, StopClip, OpenClipEditor));
                StatusMessage = null;
                _ = _apiHostedService.BroadcastBoardChangedAsync(board.Id);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to import '{Path.GetFileName(path)}': {ex.Message}";
            }
        }
    }

    private void OpenClipEditor(ClipItemViewModel clip)
    {
        var window = _clipEditorWindowFactory.Create(clip.Clip, Application.Current.MainWindow);
        window.ShowDialog();
        clip.Name = clip.Clip.Name;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var window = _settingsWindowFactory.Create(Application.Current.MainWindow);
        window.ShowDialog();
    }

    [RelayCommand]
    private async Task DeleteClipAsync(ClipItemViewModel? clip)
    {
        if (clip is null || SelectedBoard is null) return;

        var hash = await _clipRepository.DeleteAsync(clip.Id);
        var original = await _originalFileRepository.GetAsync(hash);
        var shouldDeleteFile = await _originalFileRepository.ReleaseReferenceAsync(hash);

        if (shouldDeleteFile && original is not null && File.Exists(original.FilePath))
        {
            File.Delete(original.FilePath);
        }

        SelectedBoard.Clips.Remove(clip);
        _ = _apiHostedService.BroadcastBoardChangedAsync(SelectedBoard.Id);
    }

    partial void OnSelectedOutputDeviceChanged(OutputDeviceInfo? value)
    {
        if (value is not null)
        {
            _playbackCoordinator.SetOutputDevice(value.Id);
        }
    }

    private async Task TriggerClipAsync(Guid clipId)
    {
        try
        {
            await _playbackCoordinator.TriggerClipAsync(clipId);
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't play clip: {ex.Message}";
        }
    }

    private void StopClip(Guid clipId) => _playbackCoordinator.StopClip(clipId);
}
