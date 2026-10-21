namespace SoundboardLite.Core.Abstractions;

public sealed record RenderCompletedEventArgs(Guid ClipId, bool Success, string? ErrorMessage);

/// <summary>
/// Background single-consumer queue that (re-)renders a clip's cache file when its edit settings
/// change. Requesting a render for a clip that already has one in flight supersedes it rather than
/// queuing both (last-write-wins) — the queue always renders against the clip's current settings at
/// the time it's dequeued, so correctness doesn't depend on actually cancelling the superseded job.
/// </summary>
public interface IRenderQueue
{
    void RequestRender(Guid clipId);

    event EventHandler<RenderCompletedEventArgs>? RenderCompleted;
}
