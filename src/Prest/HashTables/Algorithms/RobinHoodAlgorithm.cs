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
        => ref FindCore<KeyMode, TExtractor, NoProbe<TSlot>>(key, default);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Insert<TExtractor>(TKey key, TSlot newSlot, out int slotIdx)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        => InsertCore<KeyMode, TExtractor, NoProbe<TSlot>>(key, newSlot, default, out slotIdx);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove<TExtractor>(TKey key)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        => RemoveCore<KeyMode, TExtractor, NoProbe<TSlot>>(key, default, ref Unsafe.NullRef<TSlot>());

#if NET9_0_OR_GREATER
    public readonly IEqualityComparer<TKey>? Comparer => HasherComparer.Get<TKey, THasher>(in _hasher);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref readonly TSlot FindSlot<TExtractor, TProbe>(TProbe probe)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        where TProbe : struct, IHashProbe<TSlot>, allows ref struct
        => ref FindCore<ProbeMode, TExtractor, TProbe>(default!, probe);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Insert<TExtractor, TProbe>(TProbe probe, out int slotIdx)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        where TProbe : struct, IInsertProbe<TSlot>, allows ref struct
        => InsertCore<ProbeMode, TExtractor, TProbe>(default!, default!, probe, out slotIdx);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove<TExtractor, TProbe>(TProbe probe, out TSlot removed)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        where TProbe : struct, IHashProbe<TSlot>, allows ref struct
    {
        removed = default!;
        return RemoveCore<ProbeMode, TExtractor, TProbe>(default!, probe, ref removed);
    }
#endif

    // The loops below are shared by the key-based members above and the
    // alternate-key (probe-based) members. TMode is KeyMode or ProbeMode, a
    // JIT-time constant: KeyMode hashes and compares `key` with this algorithm's
    // own hasher, exactly as before probes existed; ProbeMode asks `probe`. (A
    // wrapper struct around the key would cost a real call per hash and per
    // comparison in shared generic code, i.e. for reference-type keys.)
    // RemoveCore copies the removed slot into `removed` only in ProbeMode; the
    // key-based Remove has no use for it and passes a null ref.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly ref readonly TSlot FindCore<TMode, TExtractor, TProbe>(TKey key, TProbe probe)
        where TMode : struct
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        where TProbe : struct, IHashProbe<TSlot>
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
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

        var hash = Unsafe.AsRef(in _finalizer).Finalize(
            KeyMode.Is<TMode>() ? Unsafe.AsRef(in _hasher).ComputeHash(key) : probe.ComputeHash());
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
            if (KeyMode.Is<TMode>()
                ? Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in slotRef))
                : probe.Matches(in slotRef))
            {
                return ref slotRef;
            }
            pos = (pos + 1) & bucketMask;
            probeDistance++;
        }
        return ref Unsafe.NullRef<TSlot>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool InsertCore<TMode, TExtractor, TProbe>(TKey key, TSlot newSlot, TProbe probe, out int slotIdx)
        where TMode : struct
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        where TProbe : struct, IInsertProbe<TSlot>
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
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

        var hash = Unsafe.AsRef(in _finalizer).Finalize(
            KeyMode.Is<TMode>() ? Unsafe.AsRef(in _hasher).ComputeHash(key) : probe.ComputeHash());
        var pos = SwissTableHelpers.InitialPos(hash, bucketMask);
        byte probeDistance = 0;

        // Phase 1: duplicate check. Walk until an empty slot or an entry that sits
        // closer to its home than we would; by the RH invariant the key can't be
        // any further along the chain.
        while (true)
        {
            if (probeDistance > MaxDistance)
            {
                // Distance cap reached without finding a place — grow and retry.
                Grow<TExtractor>();
                return InsertCore<TMode, TExtractor, TProbe>(key, newSlot, probe, out slotIdx);
            }

            var slotDistance = Unsafe.Add(ref distBase, (nint)(uint)pos);
            if (slotDistance == EmptyDistance || slotDistance < probeDistance)
            {
                break;
            }

            if (slotDistance == probeDistance
                && (KeyMode.Is<TMode>()
                    ? Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in Unsafe.Add(ref slotBase, (nint)(uint)pos)))
                    : probe.Matches(in Unsafe.Add(ref slotBase, (nint)(uint)pos))))
            {
                // Duplicate — only valid at the FIRST occurrence (where the
                // original insert would have lived). Later occurrences can't
                // be the same key because RH shifts left.
                slotIdx = pos;
                return false;
            }

            pos = (pos + 1) & bucketMask;
            probeDistance++;
        }

        // Phase 2: the key is absent, so build its slot now and place it here,
        // robbing poorer slots and carrying each displaced entry forward. No more
        // key comparisons are needed.
        var inFlightSlot = KeyMode.Is<TMode>() ? newSlot : probe.CreateSlot();
        slotIdx = pos;

        while (probeDistance <= MaxDistance)
        {
            var slotDistance = Unsafe.Add(ref distBase, (nint)(uint)pos);

            if (slotDistance == EmptyDistance)
            {
                // Empty slot — drop the in-flight entry here, done.
                Unsafe.Add(ref distBase, (nint)(uint)pos) = probeDistance;
                Unsafe.Add(ref slotBase, (nint)(uint)pos) = inFlightSlot;
                _count++;
                return true;
            }

            if (slotDistance < probeDistance)
            {
                // Rob: swap our entry with the poorer slot's entry, continue
                // probing with the displaced entry and its new distance.
                var temp = Unsafe.Add(ref slotBase, (nint)(uint)pos);
                Unsafe.Add(ref slotBase, (nint)(uint)pos) = inFlightSlot;
                Unsafe.Add(ref distBase, (nint)(uint)pos) = probeDistance;
                inFlightSlot = temp;
                probeDistance = slotDistance;
            }

            pos = (pos + 1) & bucketMask;
            probeDistance++;
        }

        // A displaced entry hit the distance cap. The new entry is already in the
        // table, so grow (which rehashes it with everything else) and re-place the
        // displaced one.
        Grow<TExtractor>();
        return Insert<TExtractor>(default(TExtractor).Extract(in inFlightSlot), inFlightSlot, out slotIdx);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool RemoveCore<TMode, TExtractor, TProbe>(TKey key, TProbe probe, ref TSlot removed)
        where TMode : struct
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        where TProbe : struct, IHashProbe<TSlot>
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
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

        var hash = Unsafe.AsRef(in _finalizer).Finalize(
            KeyMode.Is<TMode>() ? Unsafe.AsRef(in _hasher).ComputeHash(key) : probe.ComputeHash());
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
            if (KeyMode.Is<TMode>()
                ? Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in Unsafe.Add(ref slotBase, (nint)(uint)pos)))
                : probe.Matches(in Unsafe.Add(ref slotBase, (nint)(uint)pos)))
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

        if (!KeyMode.Is<TMode>())
        {
            removed = Unsafe.Add(ref slotBase, (nint)(uint)pos);
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
