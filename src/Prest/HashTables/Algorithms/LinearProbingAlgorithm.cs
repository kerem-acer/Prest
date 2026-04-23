using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CS8619 // Nullability of TSlot vs Unsafe.NullRef<TSlot>() — expected for unconstrained generic ref returns.

namespace Prest;

/// <summary>
/// Open-addressing hash table with linear probing (no SIMD, no group scans).
/// Same three-state control byte scheme as SwissTable (<c>0xFF</c> empty,
/// <c>0x80</c> deleted, <c>0x00–0x7F</c> tag); probe walks one slot at a time
/// instead of 16. Exists mainly as a baseline / abstraction sanity-check — if
/// this algorithm hits the same perf as SwissTable minus the SIMD win, the
/// <see cref="IHashAlgorithm{TSlot,TKey}" /> abstraction is zero-cost.
/// </summary>
public struct LinearProbingAlgorithm<TSlot, TKey, THasher, TFinalizer> : IHashAlgorithm<TSlot, TKey>
    where TKey : notnull
    where THasher : struct, IHasher<TKey>
    where TFinalizer : struct, IHashFinalizer
{
    byte[]? _controls;
    TSlot[]? _slots;
    int _capacity;
    int _bucketCount; // power of 2
    int _bucketMask;
    int _count;
    int _tombstoneCount;
    readonly THasher _hasher;
    readonly TFinalizer _finalizer;

    public LinearProbingAlgorithm(THasher hasher, TFinalizer finalizer)
    {
        _hasher = hasher;
        _finalizer = finalizer;
    }

    public readonly int Count => _count;
    public readonly int Capacity => _capacity;
    public readonly bool IsAllocated => _controls is not null;

    public readonly TSlot[]? SlotArray => _slots;
    public readonly int SlotScanLimit => _bucketCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsSlotLive(int slotIdx) =>
        _controls is not null && (_controls[slotIdx] & SwissTableHelpers.CtrlLiveBit) == 0;

    public void Initialize(int capacity)
    {
        _bucketCount = SwissTableHelpers.ComputeBucketCount(capacity);
        _bucketMask = _bucketCount - 1;
        _capacity = _bucketCount * 7 / 8;

        byte[]? controls = null;
        TSlot[]? slots = null;
        try
        {
            controls = ArrayPool<byte>.Shared.Rent(_bucketCount);
            controls.AsSpan(0, _bucketCount).Fill(SwissTableHelpers.CtrlEmpty);
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

        _controls.AsSpan(0, _bucketCount).Fill(SwissTableHelpers.CtrlEmpty);
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

        // At ≤7/8 load, an empty slot exists, so the empty-check always terminates the probe.
        while (true)
        {
            var ctrl = Unsafe.Add(ref controlBase, (nint)(uint)pos);
            if (ctrl == SwissTableHelpers.CtrlEmpty)
            {
                return ref Unsafe.NullRef<TSlot>();
            }
            if (ctrl == h2)
            {
                ref var slotRef = ref Unsafe.Add(ref slotBase, (nint)(uint)pos);
                if (Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in slotRef)))
                {
                    return ref slotRef;
                }
            }
            pos = (pos + 1) & bucketMask;
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

        var controls = _controls!;
        var slots = _slots!;
        ref var controlBase = ref MemoryMarshal.GetArrayDataReference(controls);
        ref var slotBase = ref MemoryMarshal.GetArrayDataReference(slots);
        var bucketMask = _bucketMask;

        var hash = Unsafe.AsRef(in _finalizer).Finalize(Unsafe.AsRef(in _hasher).ComputeHash(key));
        var h2 = SwissTableHelpers.H2(hash);
        var pos = SwissTableHelpers.InitialPos(hash, bucketMask);
        var insertionSlot = -1;

        for (var probe = 0; probe < _bucketCount; probe++)
        {
            var ctrl = Unsafe.Add(ref controlBase, (nint)(uint)pos);
            if (ctrl == SwissTableHelpers.CtrlEmpty)
            {
                var target = insertionSlot >= 0 ? insertionSlot : pos;
                Unsafe.Add(ref controlBase, (nint)(uint)target) = h2;
                Unsafe.Add(ref slotBase, (nint)(uint)target) = newSlot;
                _count++;
                if (insertionSlot >= 0)
                {
                    _tombstoneCount--;
                }
                slotIdx = target;
                return true;
            }
            if (ctrl == SwissTableHelpers.CtrlDeleted)
            {
                if (insertionSlot < 0)
                {
                    insertionSlot = pos;
                }
            }
            else if (ctrl == h2 && Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in Unsafe.Add(ref slotBase, (nint)(uint)pos))))
            {
                slotIdx = pos;
                return false;
            }
            pos = (pos + 1) & bucketMask;
        }

        // Probe exhausted without finding empty — reuse tombstone if we saw one.
        if (insertionSlot >= 0)
        {
            Unsafe.Add(ref controlBase, (nint)(uint)insertionSlot) = h2;
            Unsafe.Add(ref slotBase, (nint)(uint)insertionSlot) = newSlot;
            _count++;
            _tombstoneCount--;
            slotIdx = insertionSlot;
            return true;
        }

        Grow<TExtractor>();
        return Insert<TExtractor>(key, newSlot, out slotIdx);
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
        var bucketMask = _bucketMask;

        var hash = Unsafe.AsRef(in _finalizer).Finalize(Unsafe.AsRef(in _hasher).ComputeHash(key));
        var h2 = SwissTableHelpers.H2(hash);
        var pos = SwissTableHelpers.InitialPos(hash, bucketMask);

        for (var probe = 0; probe < _bucketCount; probe++)
        {
            var ctrl = Unsafe.Add(ref controlBase, (nint)(uint)pos);
            if (ctrl == SwissTableHelpers.CtrlEmpty)
            {
                return false;
            }
            if (ctrl == h2 && Unsafe.AsRef(in _hasher).Equals(key, default(TExtractor).Extract(in Unsafe.Add(ref slotBase, (nint)(uint)pos))))
            {
                // Look at the next slot: if it's empty, we can mark empty
                // (no probe chain depends on this slot); else mark deleted.
                var nextCtrl = Unsafe.Add(ref controlBase, (nint)(uint)((pos + 1) & bucketMask));
                var marker = nextCtrl == SwissTableHelpers.CtrlEmpty ? SwissTableHelpers.CtrlEmpty : SwissTableHelpers.CtrlDeleted;
                Unsafe.Add(ref controlBase, (nint)(uint)pos) = marker;
                if (marker == SwissTableHelpers.CtrlDeleted)
                {
                    _tombstoneCount++;
                }
#if NET
                if (RuntimeHelpers.IsReferenceOrContainsReferences<TSlot>())
                {
                    Unsafe.Add(ref slotBase, (nint)(uint)pos) = default!;
                }
#else
                Unsafe.Add(ref slotBase, (nint)(uint)pos) = default!;
#endif
                _count--;
                return true;
            }
            pos = (pos + 1) & bucketMask;
        }
        return false;
    }

    public void Grow<TExtractor>()
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
    {
        var oldControls = _controls!;
        var oldSlots = _slots!;
        var oldBucketCount = _bucketCount;

        var newBucketCount = _bucketCount * 2;
        var newControls = ArrayPool<byte>.Shared.Rent(newBucketCount);
        newControls.AsSpan(0, newBucketCount).Fill(SwissTableHelpers.CtrlEmpty);
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
                Insert<TExtractor>(default(TExtractor).Extract(in oldSlots[i]), oldSlots[i], out _);
            }
        }

        ArrayPool<byte>.Shared.Return(oldControls);
        ArrayPool<TSlot>.Shared.Return(oldSlots, clearArray: true);
    }
}
