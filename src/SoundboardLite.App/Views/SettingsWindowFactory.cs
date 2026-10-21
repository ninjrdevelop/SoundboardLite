using System.Windows;
using SoundboardLite.App.ViewModels;
using SoundboardLite.Core.Abstractions;

namespace SoundboardLite.App.Views;

/// <summary>
/// Builds a <see cref="SettingsWindow"/>. A factory (mirroring <see cref="ClipEditorWindowFactory"/>)
/// rather than DI-registering the window directly, since a WPF <c>Window</c> can only be shown once —
/// a singleton-registered window instance would break on the second "Settings" click.
/// </summary>
public sealed class SettingsWindowFactory
{
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IPlaybackCoordinator _playbackCoordinator;

    public SettingsWindowFactory(IAppSettingsRepository appSettingsRepository, IPlaybackCoordinator playbackCoordinator)
    {
        _appSettingsRepository = appSettingsRepository;
        _playbackCoordinator = playbackCoordinator;
    }

    public SettingsWindow Create(Window? owner)
    {
        var viewModel = new SettingsViewModel(_appSettingsRepository, _playbackCoordinator);
        return new SettingsWindow(viewModel) { Owner = owner };
    }
}
