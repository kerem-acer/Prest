using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CS8619 // Nullability of TSlot vs Unsafe.NullRef<TSlot>() — expected for unconstrained generic ref returns.

namespace Prest;

/// <summary>
/// Chained hash map with dense entry storage, inspired by Faster.Map's
/// <c>BlitzMap</c> (<c>https://github.com/Wsm2110/Faster.Map/blob/main/src/Core/BlitzMap.cs</c>).
/// Separates hash-bucket metadata (<c>_buckets</c>) from entry storage
/// (<c>_entries</c>) for better cache behaviour, keeps entries densely
/// packed in <c>_entries[0.._count)</c>, and walks a chain via
/// <c>_nextIndex[]</c> pointers rather than probing.
/// </summary>
/// <remarks>
/// <para>
/// Trade-off vs. SwissTable/RobinHood: entries aren't scattered across hash
/// positions, so iteration is O(count) without gaps. Removal does a
/// swap-with-last to keep density — this requires walking the chain of the
/// moved entry to update its predecessor's next pointer.
/// </para>
/// </remarks>
public struct ChainedAlgorithm<TSlot, TKey, THasher, TFinalizer> : IHashAlgorithm<TSlot, TKey>
    where TKey : notnull
    where THasher : struct, IHasher<TKey>
    where TFinalizer : struct, IHashFinalizer
{
    const int EmptyHead = -1;
    const int EndOfChain = -1;

    int[]? _buckets; // per-hash-bucket chain head; length == _bucketCount
    TSlot[]? _entries; // dense; entries[0.._count) are live
    int[]? _nextIndex; // entry -> next-entry-in-chain, -1 = end; length == _bucketCount
    int _capacity;
    int _bucketCount; // power of 2
    int _bucketMask;
    int _count;
    readonly THasher _hasher;
    readonly TFinalizer _finalizer;

    public ChainedAlgorithm(THasher hasher, TFinalizer finalizer)
    {
        _hasher = hasher;
        _finalizer = finalizer;
    }

    public readonly int Count => _count;
    public readonly int Capacity => _capacity;
    public readonly bool IsAllocated => _buckets is not null;

    public readonly TSlot[]? SlotArray => _entries;
    public readonly int SlotScanLimit => _count;

    // All entries in [0.._count) are live — dense layout, no gaps.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsSlotLive(int slotIdx) => _buckets is not null && slotIdx < _count;

    public void Initialize(int capacity)
    {
        _bucketCount = SwissTableHelpers.ComputeBucketCount(capacity);
        _bucketMask = _bucketCount - 1;
        _capacity = _bucketCount * 7 / 8;

        int[]? buckets = null;
        TSlot[]? entries = null;
        int[]? nextIndex = null;
        try
        {
            buckets = ArrayPool<int>.Shared.Rent(_bucketCount);
            buckets.AsSpan(0, _bucketCount).Fill(EmptyHead);
            entries = ArrayPool<TSlot>.Shared.Rent(_bucketCount);
            nextIndex = ArrayPool<int>.Shared.Rent(_bucketCount);
            _buckets = buckets;
            _entries = entries;
            _nextIndex = nextIndex;
        }
        catch
        {
            if (buckets is not null)
            {
                ArrayPool<int>.Shared.Return(buckets);
            }
            if (entries is not null)
            {
                ArrayPool<TSlot>.Shared.Return(entries, clearArray: true);
            }
            if (nextIndex is not null)
            {
                ArrayPool<int>.Shared.Return(nextIndex);
            }
            throw;
        }
    }

    public void Clear()
    {
        if (_buckets is null)
        {
            _count = 0;
            return;
        }

        _buckets.AsSpan(0, _bucketCount).Fill(EmptyHead);
#if NET
        if (RuntimeHelpers.IsReferenceOrContainsReferences<TSlot>())
        {
            Array.Clear(_entries!, 0, _count);
        }
#else
        Array.Clear(_entries, 0, _count);
#endif
        _count = 0;
    }

    public void Dispose()
    {
        var buckets = _buckets;
        var entries = _entries;
        var nextIndex = _nextIndex;
        _buckets = null;
        _entries = null;
        _nextIndex = null;
        _count = 0;
        if (buckets is not null)
        {
            ArrayPool<int>.Shared.Return(buckets);
        }
        if (entries is not null)
        {
            ArrayPool<TSlot>.Shared.Return(entries, clearArray: true);
        }
        if (nextIndex is not null)
        {
            ArrayPool<int>.Shared.Return(nextIndex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref readonly TSlot FindSlot<TExtractor>(TKey key)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
    {
        var buckets = _buckets;
        if (buckets is null)
        {
            return ref Unsafe.NullRef<TSlot>();
        }

        var entries = _entries!;
        var nextIndex = _nextIndex!;
        ref var bucketBase = ref MemoryMarshal.GetArrayDataReference(buckets);
        ref var entryBase = ref MemoryMarshal.GetArrayDataReference(entries);
        ref var nextBase = ref MemoryMarshal.GetArrayDataReference(nextIndex);

        var hash = Unsafe.AsRef(in _finalizer).Finalize(Unsafe.AsRef(in _hasher).ComputeHash(key));
        var bucketIdx = (int)(hash & (uint)_bucketMask);
        var entryIdx = Unsafe.Add(ref bucketBase, (nint)(uint)bucketIdx);

        while (entryIdx != EndOfChain)
        {
            ref var entryRef = ref Unsafe.Add(ref entryBase, (nint)(uint)entryIdx);
            if (Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in entryRef)))
            {
                return ref entryRef;
            }
            entryIdx = Unsafe.Add(ref nextBase, (nint)(uint)entryIdx);
        }
        return ref Unsafe.NullRef<TSlot>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Insert<TExtractor>(TKey key, TSlot newSlot, out int slotIdx)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
    {
        if (_count >= _capacity)
        {
            Grow<TExtractor>();
        }

        var buckets = _buckets!;
        var entries = _entries!;
        var nextIndex = _nextIndex!;

        var hash = Unsafe.AsRef(in _finalizer).Finalize(Unsafe.AsRef(in _hasher).ComputeHash(key));
        var bucketIdx = (int)(hash & (uint)_bucketMask);
        var head = buckets[bucketIdx];

        // Duplicate check — walk chain.
        var cursor = head;
        while (cursor != EndOfChain)
        {
            if (Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in entries[cursor])))
            {
                slotIdx = cursor;
                return false;
            }
            cursor = nextIndex[cursor];
        }

        // Append at _count, link into the chain as new head.
        var newIdx = _count;
        entries[newIdx] = newSlot;
        nextIndex[newIdx] = head;
        buckets[bucketIdx] = newIdx;
        _count++;
        slotIdx = newIdx;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove<TExtractor>(TKey key)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
    {
        var buckets = _buckets;
        if (buckets is null)
        {
            return false;
        }

        var entries = _entries!;
        var nextIndex = _nextIndex!;

        var hash = Unsafe.AsRef(in _finalizer).Finalize(Unsafe.AsRef(in _hasher).ComputeHash(key));
        var bucketIdx = (int)(hash & (uint)_bucketMask);

        // Walk chain at bucketIdx, track predecessor for unlink.
        var cursor = buckets[bucketIdx];
        var prev = EndOfChain;
        while (cursor != EndOfChain)
        {
            if (Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in entries[cursor])))
            {
                break;
            }
            prev = cursor;
            cursor = nextIndex[cursor];
        }
        if (cursor == EndOfChain)
        {
            return false;
        }

        // Unlink target from its chain.
        var next = nextIndex[cursor];
        if (prev == EndOfChain)
        {
            buckets[bucketIdx] = next;
        }
        else
        {
            nextIndex[prev] = next;
        }

        _count--;
        // If the removed entry isn't the last one, swap the last entry into the
        // hole and fix up the chain that referenced the last entry.
        if (cursor != _count)
        {
            var lastIdx = _count;
            entries[cursor] = entries[lastIdx];
            nextIndex[cursor] = nextIndex[lastIdx];

            // Find whoever points to lastIdx (bucket head or some predecessor)
            // and repoint it at cursor.
            var movedKey = default(TExtractor).Extract(in entries[cursor]);
            var movedHash = Unsafe.AsRef(in _finalizer).Finalize(Unsafe.AsRef(in _hasher).ComputeHash(movedKey));
            var movedBucketIdx = (int)(movedHash & (uint)_bucketMask);
            if (buckets[movedBucketIdx] == lastIdx)
            {
                buckets[movedBucketIdx] = cursor;
            }
            else
            {
                var movedCursor = buckets[movedBucketIdx];
                while (movedCursor != EndOfChain && nextIndex[movedCursor] != lastIdx)
                {
                    movedCursor = nextIndex[movedCursor];
                }
                if (movedCursor != EndOfChain)
                {
                    nextIndex[movedCursor] = cursor;
                }
            }

#if NET
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TSlot>())
            {
                entries[lastIdx] = default!;
            }
#else
            entries[lastIdx] = default!;
#endif
        }
        else
        {
#if NET
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TSlot>())
            {
                entries[cursor] = default!;
            }
#else
            entries[cursor] = default!;
#endif
        }
        return true;
    }

    public void Grow<TExtractor>()
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
    {
        var oldEntries = _entries!;
        var oldBuckets = _buckets!;
        var oldNextIndex = _nextIndex!;
        var oldCount = _count;

        var newBucketCount = _bucketCount * 2;
        var newBuckets = ArrayPool<int>.Shared.Rent(newBucketCount);
        newBuckets.AsSpan(0, newBucketCount).Fill(EmptyHead);
        var newEntries = ArrayPool<TSlot>.Shared.Rent(newBucketCount);
        var newNextIndex = ArrayPool<int>.Shared.Rent(newBucketCount);

        _bucketCount = newBucketCount;
        _bucketMask = newBucketCount - 1;
        _capacity = newBucketCount * 7 / 8;
        _buckets = newBuckets;
        _entries = newEntries;
        _nextIndex = newNextIndex;
        _count = 0;

        // Reinsert each old entry into the new table.
        for (var i = 0; i < oldCount; i++)
        {
            Insert<TExtractor>(default(TExtractor).Extract(in oldEntries[i]), oldEntries[i], out _);
        }

        ArrayPool<int>.Shared.Return(oldBuckets);
        ArrayPool<TSlot>.Shared.Return(oldEntries, clearArray: true);
        ArrayPool<int>.Shared.Return(oldNextIndex);
    }
}
