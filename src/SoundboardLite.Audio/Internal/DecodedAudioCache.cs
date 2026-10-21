namespace SoundboardLite.Audio.Internal;

/// <summary>
/// Memory-budgeted LRU for fully-decoded clip PCM, keyed by render key (or "orig:{hash}" for clips
/// that have no render yet). Never evicts an entry with an active playback refcount above zero —
/// budget overrun from in-use entries alone is accepted as the safe failure mode over cutting off
/// audio that's currently playing.
/// </summary>
internal sealed class DecodedAudioCache
{
    private sealed class Entry
    {
        public required CachedClipAudio Audio;
        public int RefCount;
    }

    private readonly long _budgetBytes;
    private readonly object _lock = new();
    private readonly Dictionary<string, Entry> _entries = new();
    private readonly LinkedList<string> _lruOrder = new(); // front = most recently used

    public DecodedAudioCache(long budgetBytes = 175 * 1024 * 1024) => _budgetBytes = budgetBytes;

    /// <summary>If present, bumps it to most-recently-used and increments its refcount. Caller must
    /// call <see cref="Release"/> exactly once when done (e.g. when playback of that instance ends).</summary>
    public bool TryAcquire(string key, out CachedClipAudio audio)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                entry.RefCount++;
                Touch(key);
                audio = entry.Audio;
                return true;
            }
        }
        audio = null!;
        return false;
    }

    /// <summary>Inserts a freshly-decoded entry (or, if another caller raced and already inserted it,
    /// just acquires the existing one) with its refcount pre-incremented for the caller.</summary>
    public void InsertAndAcquire(string key, CachedClipAudio audio)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                existing.RefCount++;
                Touch(key);
                return;
            }

            _entries[key] = new Entry { Audio = audio, RefCount = 1 };
            _lruOrder.AddFirst(key);
            EvictIfOverBudget();
        }
    }

    public void Release(string key)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var entry) && entry.RefCount > 0)
            {
                entry.RefCount--;
                EvictIfOverBudget();
            }
        }
    }

    private void Touch(string key)
    {
        _lruOrder.Remove(key);
        _lruOrder.AddFirst(key);
    }

    private void EvictIfOverBudget()
    {
        var total = _entries.Values.Sum(e => e.Audio.ApproximateBytes);
        if (total <= _budgetBytes) return;

        for (var node = _lruOrder.Last; node is not null && total > _budgetBytes;)
        {
            var previous = node.Previous;
            if (_entries.TryGetValue(node.Value, out var entry) && entry.RefCount == 0)
            {
                total -= entry.Audio.ApproximateBytes;
                _entries.Remove(node.Value);
                _lruOrder.Remove(node);
            }
            node = previous;
        }
    }
}
