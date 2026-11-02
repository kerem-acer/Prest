#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;

namespace Prest;

/// <summary>
/// Adapts a BCL <see cref="IAlternateEqualityComparer{TAlternate,T}" /> to
/// <see cref="IAlternateHasher{TAlternate,T}" />. Backs <c>GetAlternateLookup&lt;TAlternate&gt;()</c> on
/// maps and sets whose hasher wraps a comparer. Costs one interface call per hash/equality, the
/// same as <see cref="Dictionary{TKey,TValue}" />'s alternate lookup.
/// </summary>
readonly struct AlternateComparerHasher<TAlternate, T> : IAlternateHasher<TAlternate, T>
    where TAlternate : allows ref struct
    where T : notnull
{
    readonly IAlternateEqualityComparer<TAlternate, T> _comparer;

    public AlternateComparerHasher(IAlternateEqualityComparer<TAlternate, T> comparer) => _comparer = comparer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ComputeHash(TAlternate alternate) => (uint)_comparer.GetHashCode(alternate);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TAlternate alternate, T other) => _comparer.Equals(alternate, other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Create(TAlternate alternate) => _comparer.Create(alternate);
}
#endif
