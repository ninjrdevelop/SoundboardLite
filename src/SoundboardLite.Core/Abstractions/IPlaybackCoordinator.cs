using SoundboardLite.Core.Models;

namespace SoundboardLite.Core.Abstractions;

/// <summary>
/// The single entry point for triggering clip playback. Both the WPF click-to-trigger UI and the
/// embedded local API (Stream Deck plugin) call the same methods here, so behaviour (including
/// push-to-talk wrapping, once added) never diverges by trigger source.
/// </summary>
public interface IPlaybackCoordinator
{
    /// <summary>Restores the previously-selected output device (or the system default) and starts the mixer.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    IReadOnlyList<OutputDeviceInfo> GetOutputDevices();
    void SetOutputDevice(string deviceId);
    string? CurrentOutputDeviceId { get; }

    /// <summary>
    /// Switches the WASAPI shared-mode output latency between the robust default (~100ms) and a
    /// low-latency mode (~45ms), persists the choice, and — if a device is already active — rebuilds
    /// its player immediately so the change takes effect without restarting the app.
    /// </summary>
    void SetLowLatencyMode(bool enabled);

    Task TriggerClipAsync(Guid clipId, CancellationToken ct = default);
    void StopClip(Guid clipId);
    void StopAll();

    /// <summary>
    /// Fired once per triggered instance (so a polyphonically-retriggered clip fires once per
    /// overlapping play), used by the embedded API's WebSocket hub to push clipTriggered/clipStopped
    /// events for Stream Deck key-state feedback. <see cref="StopPreview"/> uses the same internal
    /// stop path as a real clip but is keyed by an internal sentinel id, never a real clip id, so
    /// consumers can safely ignore any clip id they don't recognise.
    /// </summary>
    event EventHandler<Guid>? ClipStarted;
    event EventHandler<Guid>? ClipStopped;

    /// <summary>
    /// Plays an arbitrary file once, outside the clip-keyed decode cache — used by the clip editor's
    /// live preview, which renders a short throwaway file per preview rather than a cached clip.
    /// </summary>
    Task PlayPreviewFileAsync(string filePath, CancellationToken ct = default);
    void StopPreview();
}
