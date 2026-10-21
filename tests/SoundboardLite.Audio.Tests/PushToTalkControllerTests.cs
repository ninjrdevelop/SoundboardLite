using Shouldly;
using SoundboardLite.Audio.Ptt;

namespace SoundboardLite.Audio.Tests;

public sealed class PushToTalkControllerTests
{
    private sealed record KeyEvent(int VirtualKeyCode, bool KeyUp);

    private readonly List<KeyEvent> _events = [];
    private readonly PushToTalkController _sut;

    public PushToTalkControllerTests()
    {
        _sut = new PushToTalkController((vk, keyUp) => _events.Add(new KeyEvent(vk, keyUp)));
    }

    [Fact]
    public void Acquire_FirstCallForAKey_SendsKeyDown()
    {
        _sut.Acquire(65);

        _events.ShouldBe([new KeyEvent(65, false)]);
    }

    [Fact]
    public void Acquire_SecondOverlappingCallForSameKey_DoesNotSendAnotherKeyDown()
    {
        _sut.Acquire(65);
        _sut.Acquire(65);

        _events.ShouldBe([new KeyEvent(65, false)]);
    }

    [Fact]
    public void Release_LastReferenceForAKey_SendsKeyUp()
    {
        _sut.Acquire(65);
        _sut.Release(65);

        _events.ShouldBe([new KeyEvent(65, false), new KeyEvent(65, true)]);
    }

    [Fact]
    public void Release_WhileAnotherReferenceStillHeld_DoesNotSendKeyUp()
    {
        // Two overlapping PTT-enabled clips sharing the same key: the key must stay down until both finish.
        _sut.Acquire(65);
        _sut.Acquire(65);
        _sut.Release(65);

        _events.ShouldBe([new KeyEvent(65, false)]);
    }

    [Fact]
    public void Release_AfterBothOverlappingReferencesRelease_SendsExactlyOneKeyUp()
    {
        _sut.Acquire(65);
        _sut.Acquire(65);
        _sut.Release(65);
        _sut.Release(65);

        _events.ShouldBe([new KeyEvent(65, false), new KeyEvent(65, true)]);
    }

    [Fact]
    public void Release_WithoutAMatchingAcquire_IsANoOp()
    {
        _sut.Release(65);

        _events.ShouldBeEmpty();
    }

    [Fact]
    public void DifferentKeys_AreTrackedIndependently()
    {
        _sut.Acquire(65);
        _sut.Acquire(66);
        _sut.Release(65);

        _events.ShouldBe([new KeyEvent(65, false), new KeyEvent(66, false), new KeyEvent(65, true)]);
    }

    [Fact]
    public void AcquireThenReleaseThenAcquireAgain_SendsKeyDownEachCycle()
    {
        _sut.Acquire(65);
        _sut.Release(65);
        _sut.Acquire(65);

        _events.ShouldBe([new KeyEvent(65, false), new KeyEvent(65, true), new KeyEvent(65, false)]);
    }
}
