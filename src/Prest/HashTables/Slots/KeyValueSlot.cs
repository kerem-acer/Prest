using System.Runtime.CompilerServices;

#pragma warning disable CA1051 // Public fields intentional — zero-overhead field access for hot paths.

namespace Prest;

/// <summary>
/// Key/value slot layout shared by all <see cref="PooledHashMap{TKey,TValue,TAlgo}" />
/// algorithms. Public mutable fields — a hot-path struct with no encapsulation
/// tax. Stored directly in the algorithm's slot array.
/// </summary>
public struct KeyValueSlot<TKey, TValue>
{
    public TKey Key;
    public TValue Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KeyValueSlot(TKey key, TValue value)
    {
        Key = key;
        Value = value;
    }
}
