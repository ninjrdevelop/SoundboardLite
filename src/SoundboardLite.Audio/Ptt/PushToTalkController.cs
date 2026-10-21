using System.Runtime.InteropServices;
using SoundboardLite.Core.Abstractions;

namespace SoundboardLite.Audio.Ptt;

/// <summary>
/// Holds a key down via <c>user32!SendInput</c> while ref-counted by virtual-key code. A small,
/// well-documented P/Invoke pattern — not worth pulling in a dependency for.
///
/// Note: synthetic input can be ignored by games with kernel-level anti-cheat. Acceptable, documented
/// limitation — Discord and the vast majority of apps/games respond to it normally.
/// </summary>
public sealed class PushToTalkController : IPushToTalkController
{
    private readonly object _lock = new();
    private readonly Dictionary<int, int> _refCounts = new();
    private readonly Action<int, bool> _sendKeyEvent;

    public PushToTalkController() : this(SendKeyEventNative)
    {
    }

    /// <summary>Test-only seam: lets unit tests substitute a fake sender instead of emitting real
    /// synthetic keyboard input into whatever window happens to have focus on the test machine.</summary>
    internal PushToTalkController(Action<int, bool> sendKeyEvent) => _sendKeyEvent = sendKeyEvent;

    public void Acquire(int virtualKeyCode)
    {
        lock (_lock)
        {
            var count = _refCounts.GetValueOrDefault(virtualKeyCode);
            _refCounts[virtualKeyCode] = count + 1;
            if (count == 0)
            {
                _sendKeyEvent(virtualKeyCode, false);
            }
        }
    }

    public void Release(int virtualKeyCode)
    {
        lock (_lock)
        {
            if (!_refCounts.TryGetValue(virtualKeyCode, out var count) || count <= 0) return;

            count--;
            if (count == 0)
            {
                _refCounts.Remove(virtualKeyCode);
                _sendKeyEvent(virtualKeyCode, true);
            }
            else
            {
                _refCounts[virtualKeyCode] = count;
            }
        }
    }

    private const int InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;

    private static void SendKeyEventNative(int virtualKeyCode, bool keyUp)
    {
        var input = new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Ki = new KeybdInput
                {
                    VirtualKeyCode = (ushort)virtualKeyCode,
                    ScanCode = 0,
                    Flags = keyUp ? KeyEventFKeyUp : 0,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero,
                },
            },
        };
        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInputStructure);

    // SendInput validates cbSize against the real native INPUT struct's size, which is a union sized
    // to fit its largest variant (MOUSEINPUT) — not just KEYBDINPUT. A struct with only the keyboard
    // fields (no union, no MOUSEINPUT/HARDWAREINPUT members) is the wrong size and SendInput silently
    // rejects every call with ERROR_INVALID_PARAMETER. Caught by actually running this and observing
    // zero key events via a system-wide low-level keyboard hook — not by unit tests, which mock the
    // send call and can't catch a wrong struct layout.
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mi;
        [FieldOffset(0)] public KeybdInput Ki;
        [FieldOffset(0)] public HardwareInput Hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort VirtualKeyCode;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Msg;
        public ushort ParamL;
        public ushort ParamH;
    }
}
