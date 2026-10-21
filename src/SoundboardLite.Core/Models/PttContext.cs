namespace SoundboardLite.Core.Models;

/// <summary>The resolved (clip-override-or-global-default) push-to-talk parameters for one trigger.</summary>
public sealed record PttContext(int VirtualKeyCode, int BeginDelayMs, int EndDelayMs);
