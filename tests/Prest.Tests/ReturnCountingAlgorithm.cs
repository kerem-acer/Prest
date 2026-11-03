using System.Runtime.CompilerServices;

#pragma warning disable IDE0044 // Readonly not possible — the inner algorithm's methods mutate _inner.

namespace Prest.Tests;

/// <summary>Counts how many times a <see cref="ReturnCountingAlgorithm{TSlot,TKey,TInner}" /> gave its buffers back.</summary>
sealed class BufferReturnLog
{
    public int Returns { get; private set; }

    public void Record() => Returns++;
}

/// <summary>
/// Forwards every call to <typeparamref name="TInner" /> and records in a <see cref="BufferReturnLog" />
/// each <see cref="Dispose" /> that returned rented buffers to the pool. The log is a class, so every copy
/// of this struct reports to the same log: a lost write (a defensive copy) shows up as zero returns, a
/// copy disposed alongside the original as two.
/// </summary>
struct ReturnCountingAlgorithm<TSlot, TKey, TInner> : IHashAlgorithm<TSlot, TKey>
    where TKey : notnull
    where TInner : struct, IHashAlgorithm<TSlot, TKey>
{
    TInner _inner;
    readonly BufferReturnLog _log;

    public ReturnCountingAlgorithm(BufferReturnLog log, TInner inner = default)
    {
        _inner = inner;
        _log = log;
    }

    public int Count => _inner.Count;

    public int Capacity => _inner.Capacity;

    public bool IsAllocated => _inner.IsAllocated;

    public TSlot[]? SlotArray => _inner.SlotArray;

    public int SlotScanLimit => _inner.SlotScanLimit;

    public IEqualityComparer<TKey>? Comparer => _inner.Comparer;

    public void Initialize(int capacity) => _inner.Initialize(capacity);

    public void Clear() => _inner.Clear();

    public bool IsSlotLive(int slotIdx) => _inner.IsSlotLive(slotIdx);

    // The slot lives in the inner algorithm's rented array, not in this struct; Unsafe.AsRef's scoped
    // parameter lets the ref escape the forwarding call.
    public ref readonly TSlot FindSlot<TExtractor>(TKey key)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        => ref Unsafe.AsRef(in _inner.FindSlot<TExtractor>(key));

    public bool Insert<TExtractor>(TKey key, TSlot newSlot, out int slotIdx)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        => _inner.Insert<TExtractor>(key, newSlot, out slotIdx);

    public bool Remove<TExtractor>(TKey key)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        => _inner.Remove<TExtractor>(key);

    public ref readonly TSlot FindSlot<TExtractor, TProbe>(TProbe probe)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        where TProbe : struct, IHashProbe<TSlot>, allows ref struct
        => ref Unsafe.AsRef(in _inner.FindSlot<TExtractor, TProbe>(probe));

    public bool Insert<TExtractor, TProbe>(TProbe probe, out int slotIdx)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        where TProbe : struct, IInsertProbe<TSlot>, allows ref struct
        => _inner.Insert<TExtractor, TProbe>(probe, out slotIdx);

    public bool Remove<TExtractor, TProbe>(TProbe probe, out TSlot removed)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        where TProbe : struct, IHashProbe<TSlot>, allows ref struct
        => _inner.Remove<TExtractor, TProbe>(probe, out removed);

    public void Dispose()
    {
        if (_inner.IsAllocated)
        {
            _log.Record();
        }
        _inner.Dispose();
    }
}
