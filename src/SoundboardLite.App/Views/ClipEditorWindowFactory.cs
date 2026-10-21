using System.Windows;
using SoundboardLite.App.ViewModels;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Models;

namespace SoundboardLite.App.Views;

/// <summary>
/// Builds a <see cref="ClipEditorWindow"/> for a given clip. A factory rather than DI-registering
/// the window/view-model directly, since <see cref="ClipEditorViewModel"/> needs the specific
/// <see cref="Clip"/> being edited as a constructor argument, which the container can't supply.
/// </summary>
public sealed class ClipEditorWindowFactory
{
    private readonly IClipRepository _clipRepository;
    private readonly IOriginalFileRepository _originalFileRepository;
    private readonly IWaveformPeakService _peakService;
    private readonly IClipRenderer _clipRenderer;
    private readonly IRenderQueue _renderQueue;
    private readonly IPlaybackCoordinator _playbackCoordinator;
    private readonly IAppPaths _appPaths;

    public ClipEditorWindowFactory(
        IClipRepository clipRepository,
        IOriginalFileRepository originalFileRepository,
        IWaveformPeakService peakService,
        IClipRenderer clipRenderer,
        IRenderQueue renderQueue,
        IPlaybackCoordinator playbackCoordinator,
        IAppPaths appPaths)
    {
        _clipRepository = clipRepository;
        _originalFileRepository = originalFileRepository;
        _peakService = peakService;
        _clipRenderer = clipRenderer;
        _renderQueue = renderQueue;
        _playbackCoordinator = playbackCoordinator;
        _appPaths = appPaths;
    }

    public ClipEditorWindow Create(Clip clip, Window? owner)
    {
        var viewModel = new ClipEditorViewModel(
            clip,
            _clipRepository,
            _originalFileRepository,
            _peakService,
            _clipRenderer,
            _renderQueue,
            _playbackCoordinator,
            _appPaths);

        return new ClipEditorWindow(viewModel) { Owner = owner };
    }
}
