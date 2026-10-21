using NAudio.Wave;
using Shouldly;
using SoundboardLite.Audio.Internal;

namespace SoundboardLite.Audio.Tests;

public sealed class DecodedAudioCacheTests
{
    private static CachedClipAudio MakeAudio(int floatCount) =>
        new(new float[floatCount], WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));

    [Fact]
    public void TryAcquire_UnknownKey_ReturnsFalse()
    {
        var sut = new DecodedAudioCache(budgetBytes: 1_000_000);
        sut.TryAcquire("missing", out _).ShouldBeFalse();
    }

    [Fact]
    public void InsertAndAcquire_ThenTryAcquire_ReturnsSameInstance()
    {
        var sut = new DecodedAudioCache(budgetBytes: 1_000_000);
        var audio = MakeAudio(100);

        sut.InsertAndAcquire("key", audio);
        sut.TryAcquire("key", out var found).ShouldBeTrue();

        found.ShouldBeSameAs(audio);
    }

    [Fact]
    public void EvictsLeastRecentlyUsed_WhenOverBudget_AndNotReferenced()
    {
        // Each entry is 100 floats * 4 bytes = 400 bytes; budget only fits one at a time.
        var sut = new DecodedAudioCache(budgetBytes: 500);

        sut.InsertAndAcquire("a", MakeAudio(100));
        sut.Release("a"); // refcount back to 0, now evictable

        sut.InsertAndAcquire("b", MakeAudio(100));
        sut.Release("b");

        // "a" should have been evicted to stay under budget once "b" pushed it over.
        sut.TryAcquire("a", out _).ShouldBeFalse();
        sut.TryAcquire("b", out _).ShouldBeTrue();
    }

    [Fact]
    public void NeverEvictsAnEntryWithActiveRefCount()
    {
        var sut = new DecodedAudioCache(budgetBytes: 500);

        sut.InsertAndAcquire("a", MakeAudio(100)); // refcount 1, stays active, never released

        sut.InsertAndAcquire("b", MakeAudio(100));
        sut.Release("b");
        sut.InsertAndAcquire("c", MakeAudio(100));
        sut.Release("c");

        // "a" is still in use (refcount 1) so it must survive eviction pressure from b/c.
        sut.TryAcquire("a", out _).ShouldBeTrue();
    }

    [Fact]
    public void InsertAndAcquire_SameKeyTwice_IncrementsRefCountInsteadOfDuplicating()
    {
        var sut = new DecodedAudioCache(budgetBytes: 1_000_000);
        var audio = MakeAudio(100);

        sut.InsertAndAcquire("key", audio);
        sut.InsertAndAcquire("key", MakeAudio(100)); // a racing decode of the same key

        sut.TryAcquire("key", out var found).ShouldBeTrue();
        found.ShouldBeSameAs(audio); // first writer wins; second caller just acquired the existing entry
    }
}
