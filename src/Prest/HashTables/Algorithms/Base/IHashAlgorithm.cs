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

    bool Insert<TExtractor>(TKey key, TSlot newSlot, out int slotIdx)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>;

    bool Remove<TExtractor>(TKey key)
        where TExtractor : struct, ISlotKeyExtractor<TSlot, TKey>;

    // Enumeration primitives — wrapper enumerators call these per-slot. Dense
    // algorithms (Blitz) set SlotScanLimit == Count and IsSlotLive trivially
    // true; sparse algorithms (SwissTable, RobinHood, Linear) walk to the
    // bucket count and test the control byte / displacement marker.
    TSlot[]? SlotArray { get; }
    int SlotScanLimit { get; }
    bool IsSlotLive(int slotIdx);
}
