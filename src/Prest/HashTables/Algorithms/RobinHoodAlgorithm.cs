using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CS8619 // Nullability of TSlot vs Unsafe.NullRef<TSlot>() — expected for unconstrained generic ref returns.

namespace Prest;

/// <summary>
/// Robin Hood hashing: linear probing with displacement tracking. On insert,
/// a new entry with a larger displacement than the currently-occupied slot's
/// entry "steals" the slot and the displaced entry continues probing.
/// On removal, backward-shift keeps displacements monotonic and eliminates
/// tombstones entirely — big win on churning workloads.
/// </summary>
/// <remarks>
/// <para>
/// Slot layout: parallel <c>byte[] _distances</c> encodes displacement from
/// ideal position. <c>0xFF</c> is the empty marker; <c>0..0xFE</c> is the
/// actual distance. No tombstones; no h2 tag — we compare keys directly
/// when distance encoding says the slot may hold our key.
/// </para>
/// </remarks>
public struct RobinHoodAlgorithm<TSlot, TKey, THasher, TFinalizer> : IHashAlgorithm<TSlot, TKey>
    where TKey : notnull
    where THasher : struct, IHasher<TKey>
    where TFinalizer : struct, IHashFinalizer
{
    const byte EmptyDistance = 0xFF;
    const byte MaxDistance = 0xFE;

    byte[]? _distances;
    TSlot[]? _slots;
    int _capacity;
    int _bucketCount; // power of 2
    int _bucketMask;
    int _count;
    readonly THasher _hasher;
    readonly TFinalizer _finalizer;

    public RobinHoodAlgorithm(THasher hasher, TFinalizer finalizer)
    {
        _hasher = hasher;
        _finalizer = finalizer;
    }

    public readonly int Count => _count;
    public readonly int Capacity => _capacity;
    public readonly bool IsAllocated => _distances is not null;

    public readonly TSlot[]? SlotArray => _slots;
    public readonly int SlotScanLimit => _bucketCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsSlotLive(int slotIdx) =>
        _distances is not null && _distances[slotIdx] != EmptyDistance;

    public void Initialize(int capacity)
    {
        _bucketCount = SwissTableHelpers.ComputeBucketCount(capacity);
        _bucketMask = _bucketCount - 1;
        _capacity = _bucketCount * 7 / 8;

        byte[]? distances = null;
        TSlot[]? slots = null;
        try
        {
            distances = ArrayPool<byte>.Shared.Rent(_bucketCount);
            distances.AsSpan(0, _bucketCount).Fill(EmptyDistance);
            slots = ArrayPool<TSlot>.Shared.Rent(_bucketCount);
            _distances = distances;
            _slots = slots;
        }
        catch
        {
            if (distances is not null)
            {
                ArrayPool<byte>.Shared.Return(distances);
            }
            if (slots is not null)
            {
                ArrayPool<TSlot>.Shared.Return(slots, clearArray: true);
            }
            throw;
        }
    }

    public void Clear()
    {
        if (_distances is null)
        {
            _count = 0;
            return;
        }

        _distances.AsSpan(0, _bucketCount).Fill(EmptyDistance);
#if NET
        if (RuntimeHelpers.IsReferenceOrContainsReferences<TSlot>())
        {
            Array.Clear(_slots!, 0, _bucketCount);
        }
#else
        Array.Clear(_slots, 0, _bucketCount);
#endif
        _count = 0;
    }

    public void Dispose()
    {
        var distances = _distances;
        var slots = _slots;
        _distances = null;
        _slots = null;
        _count = 0;
        if (distances is not null)
        {
            ArrayPool<byte>.Shared.Return(distances);
        }
        if (slots is not null)
        {
            ArrayPool<TSlot>.Shared.Return(slots, clearArray: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref readonly TSlot FindSlot<TExtractor>(TKey key)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
    {
        var distances = _distances;
        if (distances is null)
        {
            return ref Unsafe.NullRef<TSlot>();
        }

        var slots = _slots!;
        ref var distBase = ref MemoryMarshal.GetArrayDataReference(distances);
        ref var slotBase = ref MemoryMarshal.GetArrayDataReference(slots);
        var bucketMask = _bucketMask;

        var hash = Unsafe.AsRef(in _finalizer).Finalize(Unsafe.AsRef(in _hasher).ComputeHash(key));
        var pos = SwissTableHelpers.InitialPos(hash, bucketMask);
        byte probeDistance = 0;

        while (probeDistance <= MaxDistance)
        {
            var slotDistance = Unsafe.Add(ref distBase, (nint)(uint)pos);
            if (slotDistance == EmptyDistance || slotDistance < probeDistance)
            {
                // RH invariant: if we find a slot with smaller distance than our
                // probe distance, the target can't be later in the chain — it
                // would have been "robbed" to an earlier position.
                return ref Unsafe.NullRef<TSlot>();
            }
            ref var slotRef = ref Unsafe.Add(ref slotBase, (nint)(uint)pos);
            if (Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in slotRef)))
            {
                return ref slotRef;
            }
            pos = (pos + 1) & bucketMask;
            probeDistance++;
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

        var distances = _distances!;
        var slots = _slots!;
        ref var distBase = ref MemoryMarshal.GetArrayDataReference(distances);
        ref var slotBase = ref MemoryMarshal.GetArrayDataReference(slots);
        var bucketMask = _bucketMask;

        var hash = Unsafe.AsRef(in _finalizer).Finalize(Unsafe.AsRef(in _hasher).ComputeHash(key));
        var pos = SwissTableHelpers.InitialPos(hash, bucketMask);
        byte probeDistance = 0;
        var inFlightSlot = newSlot;
        var firstPos = -1;

        while (probeDistance <= MaxDistance)
        {
            var slotDistance = Unsafe.Add(ref distBase, (nint)(uint)pos);

            if (slotDistance == EmptyDistance)
            {
                // Empty slot — drop the in-flight entry here, done.
                Unsafe.Add(ref distBase, (nint)(uint)pos) = probeDistance;
                Unsafe.Add(ref slotBase, (nint)(uint)pos) = inFlightSlot;
                _count++;
                slotIdx = firstPos >= 0 ? firstPos : pos;
                return true;
            }

            if (slotDistance == probeDistance
                && Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in Unsafe.Add(ref slotBase, (nint)(uint)pos))))
            {
                // Duplicate — only valid at the FIRST occurrence (where the
                // original insert would have lived). Later occurrences can't
                // be the same key because RH shifts left.
                slotIdx = pos;
                return false;
            }

            if (slotDistance < probeDistance)
            {
                // Rob: swap our entry with the poorer slot's entry, continue
                // probing with the displaced entry and its new distance.
                if (firstPos < 0)
                {
                    firstPos = pos;
                }
                var temp = Unsafe.Add(ref slotBase, (nint)(uint)pos);
                Unsafe.Add(ref slotBase, (nint)(uint)pos) = inFlightSlot;
                Unsafe.Add(ref distBase, (nint)(uint)pos) = probeDistance;
                inFlightSlot = temp;
                probeDistance = slotDistance;
            }

            pos = (pos + 1) & bucketMask;
            probeDistance++;
        }

        // Hit distance cap without placing — grow and retry.
        Grow<TExtractor>();
        return Insert<TExtractor>(default(TExtractor).Extract(in inFlightSlot), inFlightSlot, out slotIdx);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove<TExtractor>(TKey key)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
    {
        var distances = _distances;
        if (distances is null)
        {
            return false;
        }

        var slots = _slots!;
        ref var distBase = ref MemoryMarshal.GetArrayDataReference(distances);
        ref var slotBase = ref MemoryMarshal.GetArrayDataReference(slots);
        var bucketMask = _bucketMask;

        var hash = Unsafe.AsRef(in _finalizer).Finalize(Unsafe.AsRef(in _hasher).ComputeHash(key));
        var pos = SwissTableHelpers.InitialPos(hash, bucketMask);
        byte probeDistance = 0;

        // Locate the target slot.
        while (probeDistance <= MaxDistance)
        {
            var slotDistance = Unsafe.Add(ref distBase, (nint)(uint)pos);
            if (slotDistance == EmptyDistance || slotDistance < probeDistance)
            {
                return false;
            }
            if (Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in Unsafe.Add(ref slotBase, (nint)(uint)pos))))
            {
                break;
            }
            pos = (pos + 1) & bucketMask;
            probeDistance++;
        }
        if (probeDistance > MaxDistance)
        {
            return false;
        }

        // Backward-shift: move trailing entries one slot back while they have distance > 0.
        while (true)
        {
            var nextPos = (pos + 1) & bucketMask;
            var nextDistance = Unsafe.Add(ref distBase, (nint)(uint)nextPos);
            if (nextDistance == EmptyDistance || nextDistance == 0)
            {
                // Can't shift (empty, or at its ideal position). Mark current slot empty.
                Unsafe.Add(ref distBase, (nint)(uint)pos) = EmptyDistance;
#if NET
                if (RuntimeHelpers.IsReferenceOrContainsReferences<TSlot>())
                {
                    Unsafe.Add(ref slotBase, (nint)(uint)pos) = default!;
                }
#else
                Unsafe.Add(ref slotBase, (nint)(uint)pos) = default!;
#endif
                break;
            }
            Unsafe.Add(ref slotBase, (nint)(uint)pos) = Unsafe.Add(ref slotBase, (nint)(uint)nextPos);
            Unsafe.Add(ref distBase, (nint)(uint)pos) = (byte)(nextDistance - 1);
            pos = nextPos;
        }

        _count--;
        return true;
    }

    public void Grow<TExtractor>()
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
    {
        var oldDistances = _distances!;
        var oldSlots = _slots!;
        var oldBucketCount = _bucketCount;

        var newBucketCount = _bucketCount * 2;
        var newDistances = ArrayPool<byte>.Shared.Rent(newBucketCount);
        newDistances.AsSpan(0, newBucketCount).Fill(EmptyDistance);
        var newSlots = ArrayPool<TSlot>.Shared.Rent(newBucketCount);

        _bucketCount = newBucketCount;
        _bucketMask = newBucketCount - 1;
        _capacity = newBucketCount * 7 / 8;
        _distances = newDistances;
        _slots = newSlots;
        _count = 0;

        for (var i = 0; i < oldBucketCount; i++)
        {
            if (oldDistances[i] != EmptyDistance)
            {
                Insert<TExtractor>(default(TExtractor).Extract(in oldSlots[i]), oldSlots[i], out _);
            }
        }

        ArrayPool<byte>.Shared.Return(oldDistances);
        ArrayPool<TSlot>.Shared.Return(oldSlots, clearArray: true);
    }
}
