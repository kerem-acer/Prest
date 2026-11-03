using System.Runtime.CompilerServices;

namespace Prest;

public ref partial struct StackOnlyPooledHashMap<TKey, TValue, TAlgo>
{
    // PooledCollectionsMarshal takes the map by `in`: a `using var` local cannot be passed by ref
    // (CS1657) but can be passed by `in`, which passes the variable's address, not a copy.
    // Unsafe.AsRef turns that address back into a writable reference to the algorithm struct, so
    // the insert (and any grow) in GetValueRefOrAddDefault changes the caller's map. Calling the
    // algorithm through the read-only reference instead would run on a hidden copy and lose the add.

    /// <summary>Backs <see cref="PooledCollectionsMarshal.GetValueRefOrNullRef{TKey,TValue,TAlgo}(in StackOnlyPooledHashMap{TKey,TValue,TAlgo}, TKey)" />.</summary>
    internal static ref TValue GetValueRefOrNullRef(in StackOnlyPooledHashMap<TKey, TValue, TAlgo> map, TKey key)
    {
        ref var algo = ref Unsafe.AsRef(in map._algo);
        ref var slot = ref Unsafe.AsRef(in algo.FindSlot<PooledHashMap<TKey, TValue, TAlgo>.Extractor>(key));
        if (Unsafe.IsNullRef(ref slot))
        {
            return ref Unsafe.NullRef<TValue>();
        }
        return ref slot.Value;
    }

    /// <summary>Backs <see cref="PooledCollectionsMarshal.GetValueRefOrAddDefault{TKey,TValue,TAlgo}(in StackOnlyPooledHashMap{TKey,TValue,TAlgo}, TKey, out bool)" />.</summary>
    internal static ref TValue? GetValueRefOrAddDefault(in StackOnlyPooledHashMap<TKey, TValue, TAlgo> map, TKey key, out bool exists)
    {
        ref var algo = ref Unsafe.AsRef(in map._algo);
        if (!algo.IsAllocated)
        {
            EnsureInitialized(ref algo);
        }
        exists = !algo.Insert<PooledHashMap<TKey, TValue, TAlgo>.Extractor>(key, new KeyValueSlot<TKey, TValue>(key, default!), out var slotIdx);
        return ref algo.SlotArray![slotIdx].Value!;
    }
}
