namespace Prest;

/// <summary>
/// Pluggable hashtable algorithm. An algorithm struct owns its backing arrays
/// plus any algorithm-specific metadata (control bytes, displacement bytes,
/// bucket chains, etc.) and provides the CRUD API. Implementations must be
/// <see langword="struct" />s so the JIT specializes each closed generic
/// end-to-end with zero interface-dispatch cost.
/// </summary>
/// <typeparam name="TSlot">Payload type per slot (e.g. key-only for sets, key+value for maps).</typeparam>
/// <typeparam name="TKey">Key type hashed/compared by the algorithm's internal hasher.</typeparam>
public interface IHashAlgorithm<TSlot, in TKey> : IDisposable
    where TKey : notnull
{
    // Lifecycle
    void Initialize(int capacity);
    void Clear();

    // State
    int Count { get; }
    int Capacity { get; }
    bool IsAllocated { get; }

    // CRUD — TExtractor is per-call so the same algorithm works for set
    // (identity extractor) and map (slot.Key extractor) with zero duplication.
    // The extractor is a zero-sized struct used via default(TExtractor) inside
    // the algorithm — no value needs to be passed in.
    //
    // FindSlot returns a ref directly to the matching slot so callers can read
    // fields off it (e.g. .Value) without a second index lookup. On miss,
    // returns <c>Unsafe.NullRef&lt;TSlot&gt;()</c> — check with <c>Unsafe.IsNullRef</c>.
    ref readonly TSlot FindSlot<TExtractor>(TKey key)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>;

    // Insert returns false on a duplicate and leaves the table unchanged. Either
    // way, slotIdx is the SlotArray index of the entry for the key: the existing
    // one on a duplicate, the new one otherwise, after any grow the insert did.
    // PooledCollectionsMarshal.GetValueRefOrAddDefault hands out a ref to it.
    bool Insert<TExtractor>(TKey key, TSlot newSlot, out int slotIdx)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>;

    bool Remove<TExtractor>(TKey key)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>;

#if NET9_0_OR_GREATER
    // Alternate-key support (.NET 9+). The probe-based members do the same work
    // as the key-based ones above, but the probe supplies the raw hash and the
    // equality test, so the caller can search with a key of another type (a
    // ReadOnlySpan<char> for string keys, say) without creating a TKey. The
    // built-in algorithms run both through one shared loop per operation.
    // TExtractor is the same slot-key extractor the key-based members take; an
    // algorithm needs it where it re-hashes entries it already stores (growth,
    // Robin Hood displacement, Chained swap-with-last).

    /// <summary>
    /// The <see cref="IEqualityComparer{T}" /> behind this algorithm's hasher:
    /// <see cref="EqualityComparer{T}.Default" /> for <see cref="EqualityDefaultHasher{T}" />, the
    /// wrapped comparer for <see cref="ComparerHasher{T}" />, or <see langword="null" /> for a hasher
    /// that does not wrap one. <c>GetAlternateLookup&lt;TAlternate&gt;()</c> checks it for
    /// <see cref="IAlternateEqualityComparer{TAlternate,T}" />.
    /// </summary>
    IEqualityComparer<TKey>? Comparer { get; }

    /// <summary>
    /// Finds the slot that <paramref name="probe" /> matches. Returns a null ref on a miss; check with
    /// <c>Unsafe.IsNullRef</c>.
    /// </summary>
    ref readonly TSlot FindSlot<TExtractor, TProbe>(TProbe probe)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        where TProbe : struct, IHashProbe<TSlot>, allows ref struct;

    /// <summary>
    /// Inserts the slot built by <see cref="IInsertProbe{TSlot}.CreateSlot" /> unless
    /// <paramref name="probe" /> matches an existing slot. Returns <see langword="false" /> on a
    /// duplicate; <see cref="IInsertProbe{TSlot}.CreateSlot" /> is then never called. Either way,
    /// <paramref name="slotIdx" /> is the <see cref="SlotArray" /> index of the matching entry: the
    /// existing one on a duplicate, the new one otherwise.
    /// </summary>
    bool Insert<TExtractor, TProbe>(TProbe probe, out int slotIdx)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        where TProbe : struct, IInsertProbe<TSlot>, allows ref struct;

    /// <summary>
    /// Removes the slot that <paramref name="probe" /> matches and returns a copy of it in
    /// <paramref name="removed" />.
    /// </summary>
    bool Remove<TExtractor, TProbe>(TProbe probe, out TSlot removed)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>
        where TProbe : struct, IHashProbe<TSlot>, allows ref struct;
#endif

    // Enumeration primitives — wrapper enumerators call these per-slot. Dense
    // algorithms (Blitz) set SlotScanLimit == Count and IsSlotLive trivially
    // true; sparse algorithms (SwissTable, RobinHood, Linear) walk to the
    // bucket count and test the control byte / displacement marker.
    TSlot[]? SlotArray { get; }
    int SlotScanLimit { get; }
    bool IsSlotLive(int slotIdx);
}
