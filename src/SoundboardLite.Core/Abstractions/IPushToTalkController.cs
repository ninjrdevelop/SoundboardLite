namespace SoundboardLite.Core.Abstractions;

/// <summary>
/// Simulates holding a push-to-talk key via synthetic keyboard input. Ref-counted per virtual-key
/// code so multiple concurrently-playing clips sharing the same key don't release it until all of
/// them have finished — only the 0→1 transition presses the key down, only the matching 1→0
/// transition releases it.
/// </summary>
public interface IPushToTalkController
{
    void Acquire(int virtualKeyCode);
    void Release(int virtualKeyCode);
}
