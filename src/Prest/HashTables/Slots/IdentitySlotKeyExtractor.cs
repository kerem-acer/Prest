using System.Runtime.CompilerServices;

namespace Prest;

/// <summary>
/// Identity extractor for slot types that already are the key (e.g.
/// <see cref="PooledHashSet{T,TAlgo}" /> stores the item directly in the slot).
/// <see langword="readonly struct" /> so the JIT inlines <see cref="Extract" />
/// at every call site.
/// </summary>
public readonly struct IdentitySlotKeyExtractor<T> : ISlotKeyExtractor<T, T>
    where T : notnull
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Extract(in T slot) => slot;
}
