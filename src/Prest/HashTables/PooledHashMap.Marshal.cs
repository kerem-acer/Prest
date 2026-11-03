using System.Runtime.CompilerServices;

namespace Prest;

public partial class PooledHashMap<TKey, TValue, TAlgo>
{
    /// <summary>Backs <see cref="PooledCollectionsMarshal.GetValueRefOrNullRef{TKey,TValue,TAlgo}" />.</summary>
    internal ref TValue GetValueRefOrNullRef(TKey key)
    {
        ref var slot = ref Unsafe.AsRef(in _algo.FindSlot<Extractor>(key));
        if (Unsafe.IsNullRef(ref slot))
        {
            return ref Unsafe.NullRef<TValue>();
        }
        return ref slot.Value;
    }

    /// <summary>Backs <see cref="PooledCollectionsMarshal.GetValueRefOrAddDefault{TKey,TValue,TAlgo}" />.</summary>
    internal ref TValue? GetValueRefOrAddDefault(TKey key, out bool exists)
    {
        if (!_algo.IsAllocated)
        {
            EnsureInitialized();
        }
        exists = !_algo.Insert<Extractor>(key, new KeyValueSlot<TKey, TValue>(key, default!), out var slotIdx);
        return ref _algo.SlotArray![slotIdx].Value!;
    }
}
