using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Numerics;

#pragma warning disable CS8619 // Nullability of TSlot vs Unsafe.NullRef<TSlot>() — expected for unconstrained generic ref returns.

namespace Prest;

/// <summary>
/// SwissTable algorithm: 16-byte SIMD group scan, triangular probing, 7/8 load
/// factor, tombstone + erase-to-empty. Port of the original inline implementation
/// from <c>SwissTable.cs</c> exposed through <see cref="IHashAlgorithm{TSlot,TKey}" />
/// so consumers can swap the algorithm via generic parameter without paying any
/// interface-dispatch cost.
/// </summary>
/// <typeparam name="TSlot">Payload type stored in each slot.</typeparam>
/// <typeparam name="TKey">Key type used for hashing and equality.</typeparam>
/// <typeparam name="THasher">Struct hasher; passed by value but zero-sized or
/// holds only a comparer field, so effectively free.</typeparam>
/// <typeparam name="TFinalizer">Struct hash finalizer applied to the raw hash
/// before position/tag derivation. Use <see cref="NoOpHashFinalizer" /> to skip.</typeparam>
public struct SwissTableAlgorithm<TSlot, TKey, THasher, TFinalizer> : IHashAlgorithm<TSlot, TKey>
    where TKey : notnull
    where THasher : struct, IHasher<TKey>
    where TFinalizer : struct, IHashFinalizer
{
    byte[]? _controls; // length == _bucketCount + GroupWidth; last GroupWidth mirrors first
    TSlot[]? _slots;
    int _capacity;
    int _bucketCount; // power of 2
    int _bucketMask; // _bucketCount - 1; cached to skip the sub on the hot path
    int _count;
    int _tombstoneCount;

    readonly THasher _hasher;
    readonly TFinalizer _finalizer;

    public SwissTableAlgorithm(THasher hasher, TFinalizer finalizer)
    {
        _hasher = hasher;
        _finalizer = finalizer;
    }

    public readonly int Count => _count;
    public readonly int Capacity => _capacity;
    public readonly bool IsAllocated => _controls is not null;

    // IHashAlgorithm enumeration interface.
    public readonly TSlot[]? SlotArray => _slots;
    public readonly int SlotScanLimit => _bucketCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsSlotLive(int slotIdx) =>
        _controls is not null && (_controls[slotIdx] & SwissTableHelpers.CtrlLiveBit) == 0;

    public void Initialize(int capacity)
    {
        _bucketCount = SwissTableHelpers.ComputeBucketCount(capacity);
        _bucketMask = _bucketCount - 1;
        // Capacity is the load-factor limit — entries we can hold before a Grow.
        _capacity = _bucketCount * 7 / 8;

        byte[]? controls = null;
        TSlot[]? slots = null;
        try
        {
            controls = ArrayPool<byte>.Shared.Rent(_bucketCount + SwissTableHelpers.GroupWidth);
            controls.AsSpan(0, _bucketCount + SwissTableHelpers.GroupWidth).Fill(SwissTableHelpers.CtrlEmpty);
            slots = ArrayPool<TSlot>.Shared.Rent(_bucketCount);
            _controls = controls;
            _slots = slots;
        }
        catch
        {
            if (controls is not null)
            {
                ArrayPool<byte>.Shared.Return(controls);
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
        if (_controls is null)
        {
            _count = 0;
            _tombstoneCount = 0;
            return;
        }

        _controls.AsSpan(0, _bucketCount + SwissTableHelpers.GroupWidth).Fill(SwissTableHelpers.CtrlEmpty);

#if NET
        if (RuntimeHelpers.IsReferenceOrContainsReferences<TSlot>())
        {
            Array.Clear(_slots!, 0, _bucketCount);
        }
#else
        Array.Clear(_slots, 0, _bucketCount);
#endif
        _count = 0;
        _tombstoneCount = 0;
    }

    public void Dispose()
    {
        var controls = _controls;
        var slots = _slots;
        _controls = null;
        _slots = null;
        _count = 0;
        _tombstoneCount = 0;
        if (controls is not null)
        {
            ArrayPool<byte>.Shared.Return(controls);
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
        var controls = _controls;
        if (controls is null)
        {
            return ref Unsafe.NullRef<TSlot>();
        }

        var slots = _slots!;
        ref var controlBase = ref MemoryMarshal.GetArrayDataReference(controls);
        ref var slotBase = ref MemoryMarshal.GetArrayDataReference(slots);
        var bucketMask = _bucketMask;

        var hash = Unsafe.AsRef(in _finalizer).Finalize(Unsafe.AsRef(in _hasher).ComputeHash(key));
        var h2 = SwissTableHelpers.H2(hash);
        var pos = SwissTableHelpers.InitialPos(hash, bucketMask);
        var jumpDistance = 0;

        // No probe-limit counter: the SwissTable invariant guarantees an empty
        // group exists at ≤7/8 load, so the empty-check always terminates.
        while (true)
        {
            var group = SwissTableHelpers.LoadGroup(ref controlBase, pos);

            var matchBits = SwissTableHelpers.MatchByte(group, h2);
            while (matchBits != 0)
            {
                var bit = BitOperations.TrailingZeroCount(matchBits);
                var slotIdx = (pos + bit) & bucketMask;
                ref var slotRef = ref Unsafe.Add(ref slotBase, (nint)(uint)slotIdx);
                if (Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in slotRef)))
                {
                    return ref slotRef;
                }

                matchBits &= matchBits - 1;
            }

            if (SwissTableHelpers.MatchVector(group, SwissTableHelpers.EmptyVector) != 0)
            {
                return ref Unsafe.NullRef<TSlot>();
            }

            jumpDistance += SwissTableHelpers.GroupWidth;
            pos = (pos + jumpDistance) & bucketMask;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Insert<TExtractor>(TKey key, TSlot newSlot, out int slotIdx)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
    {
        if (_count + _tombstoneCount >= _capacity)
        {
            Grow<TExtractor>();
        }

        return TryInsertCore<TExtractor>(key, newSlot, out slotIdx) == InsertAttempt.Inserted;
    }

    enum InsertAttempt
    {
        Inserted,
        Duplicate,
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    InsertAttempt TryInsertCore<TExtractor>(TKey key, TSlot newSlot, out int slotIdx)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
    {
        var controls = _controls!;
        var slots = _slots!;
        ref var controlBase = ref MemoryMarshal.GetArrayDataReference(controls);
        ref var slotBase = ref MemoryMarshal.GetArrayDataReference(slots);
        var bucketCount = _bucketCount;
        var bucketMask = _bucketMask;

        var hash = Unsafe.AsRef(in _finalizer).Finalize(Unsafe.AsRef(in _hasher).ComputeHash(key));
        var h2 = SwissTableHelpers.H2(hash);
        var pos = SwissTableHelpers.InitialPos(hash, bucketMask);
        var jumpDistance = 0;
        var insertionFromTombstone = false;
        var insertionSlot = -1;

        // No probe-limit counter: caller guarantees _count + _tombstones < _capacity
        // (≤7/8 of bucketCount), so an empty group always exists — loop terminates.
        while (true)
        {
            var group = SwissTableHelpers.LoadGroup(ref controlBase, pos);

            var matchBits = SwissTableHelpers.MatchByte(group, h2);
            while (matchBits != 0)
            {
                var bit = BitOperations.TrailingZeroCount(matchBits);
                var idx = (pos + bit) & bucketMask;
                if (Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in Unsafe.Add(ref slotBase, (nint)(uint)idx))))
                {
                    slotIdx = idx;
                    return InsertAttempt.Duplicate;
                }

                matchBits &= matchBits - 1;
            }

            if (insertionSlot < 0)
            {
                var deletedBits = SwissTableHelpers.MatchVector(group, SwissTableHelpers.DeletedVector);
                if (deletedBits != 0)
                {
                    var bit = BitOperations.TrailingZeroCount(deletedBits);
                    insertionSlot = (pos + bit) & bucketMask;
                    insertionFromTombstone = true;
                }
            }

            var emptyBits = SwissTableHelpers.MatchVector(group, SwissTableHelpers.EmptyVector);
            if (emptyBits != 0)
            {
                int target;
                if (insertionSlot >= 0)
                {
                    target = insertionSlot;
                }
                else
                {
                    var bit = BitOperations.TrailingZeroCount(emptyBits);
                    target = (pos + bit) & bucketMask;
                }

                SwissTableHelpers.WriteControl(
                    ref controlBase,
                    bucketCount,
                    target,
                    h2);

                Unsafe.Add(ref slotBase, (nint)(uint)target) = newSlot;
                _count++;
                if (insertionFromTombstone)
                {
                    _tombstoneCount--;
                }

                slotIdx = target;
                return InsertAttempt.Inserted;
            }

            jumpDistance += SwissTableHelpers.GroupWidth;
            pos = (pos + jumpDistance) & bucketMask;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove<TExtractor>(TKey key)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
    {
        var controls = _controls;
        if (controls is null)
        {
            return false;
        }

        var slots = _slots!;
        ref var controlBase = ref MemoryMarshal.GetArrayDataReference(controls);
        ref var slotBase = ref MemoryMarshal.GetArrayDataReference(slots);
        var bucketCount = _bucketCount;
        var bucketMask = _bucketMask;

        var hash = Unsafe.AsRef(in _finalizer).Finalize(Unsafe.AsRef(in _hasher).ComputeHash(key));
        var h2 = SwissTableHelpers.H2(hash);
        var pos = SwissTableHelpers.InitialPos(hash, bucketMask);
        var jumpDistance = 0;

        // Probe until we see an empty group — SwissTable invariant guarantees one exists.
        while (true)
        {
            var group = SwissTableHelpers.LoadGroup(ref controlBase, pos);

            var matchBits = SwissTableHelpers.MatchByte(group, h2);
            while (matchBits != 0)
            {
                var bit = BitOperations.TrailingZeroCount(matchBits);
                var slotIdx = (pos + bit) & bucketMask;
                if (Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in Unsafe.Add(ref slotBase, (nint)(uint)slotIdx))))
                {
                    var marker = SwissTableHelpers.MatchVector(group, SwissTableHelpers.EmptyVector) != 0 ? SwissTableHelpers.CtrlEmpty : SwissTableHelpers.CtrlDeleted;
                    SwissTableHelpers.WriteControl(
                        ref controlBase,
                        bucketCount,
                        slotIdx,
                        marker);

                    if (marker == SwissTableHelpers.CtrlDeleted)
                    {
                        _tombstoneCount++;
                    }

#if NET
                    if (RuntimeHelpers.IsReferenceOrContainsReferences<TSlot>())
                    {
                        Unsafe.Add(ref slotBase, (nint)(uint)slotIdx) = default!;
                    }
#else
                    Unsafe.Add(ref slotBase, (nint)(uint)slotIdx) = default!;
#endif
                    _count--;
                    return true;
                }

                matchBits &= matchBits - 1;
            }

            if (SwissTableHelpers.MatchVector(group, SwissTableHelpers.EmptyVector) != 0)
            {
                return false;
            }

            jumpDistance += SwissTableHelpers.GroupWidth;
            pos = (pos + jumpDistance) & bucketMask;
        }
    }

    public void Grow<TExtractor>()
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
    {
        var oldControls = _controls!;
        var oldSlots = _slots!;
        var oldBucketCount = _bucketCount;

        var newBucketCount = _bucketCount * 2;
        var newControls = ArrayPool<byte>.Shared.Rent(newBucketCount + SwissTableHelpers.GroupWidth);
        newControls.AsSpan(0, newBucketCount + SwissTableHelpers.GroupWidth).Fill(SwissTableHelpers.CtrlEmpty);
        var newSlots = ArrayPool<TSlot>.Shared.Rent(newBucketCount);

        _bucketCount = newBucketCount;
        _bucketMask = newBucketCount - 1;
        _capacity = newBucketCount * 7 / 8;
        _controls = newControls;
        _slots = newSlots;
        _count = 0;
        _tombstoneCount = 0;

        for (var i = 0; i < oldBucketCount; i++)
        {
            if ((oldControls[i] & SwissTableHelpers.CtrlLiveBit) == 0)
            {
                ReinsertNoCheck<TExtractor>(oldSlots[i]);
            }
        }

        ArrayPool<byte>.Shared.Return(oldControls);
        ArrayPool<TSlot>.Shared.Return(oldSlots, clearArray: true);
    }

    void ReinsertNoCheck<TExtractor>(TSlot slot)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
    {
        var controls = _controls!;
        var slots = _slots!;
        ref var controlBase = ref MemoryMarshal.GetArrayDataReference(controls);
        ref var slotBase = ref MemoryMarshal.GetArrayDataReference(slots);
        var bucketCount = _bucketCount;
        var bucketMask = _bucketMask;

        var key = default(TExtractor).Extract(in slot);
        var hash = Unsafe.AsRef(in _finalizer).Finalize(Unsafe.AsRef(in _hasher).ComputeHash(key));
        var h2 = SwissTableHelpers.H2(hash);
        var pos = SwissTableHelpers.InitialPos(hash, bucketMask);
        var jumpDistance = 0;

        while (true)
        {
            var group = SwissTableHelpers.LoadGroup(ref controlBase, pos);
            var emptyBits = SwissTableHelpers.MatchVector(group, SwissTableHelpers.EmptyVector);
            if (emptyBits != 0)
            {
                var bit = BitOperations.TrailingZeroCount(emptyBits);
                var slotIdx = (pos + bit) & bucketMask;
                SwissTableHelpers.WriteControl(
                    ref controlBase,
                    bucketCount,
                    slotIdx,
                    h2);

                Unsafe.Add(ref slotBase, (nint)(uint)slotIdx) = slot;
                _count++;
                return;
            }

            jumpDistance += SwissTableHelpers.GroupWidth;
            pos = (pos + jumpDistance) & bucketMask;
        }
    }
}
