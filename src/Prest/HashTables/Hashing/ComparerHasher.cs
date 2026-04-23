using System.Runtime.CompilerServices;

namespace Prest;

/// <summary>
/// Hasher that wraps a user-supplied <see cref="IEqualityComparer{T}" />. Costs
/// one virtual call per hash/equals, but kept as a struct (not a class) so it
/// still fits cleanly in the struct-hasher slot.
/// </summary>
public readonly struct ComparerHasher<T> : IHasher<T> where T : notnull
{
    readonly IEqualityComparer<T> _comparer;

    public ComparerHasher(IEqualityComparer<T> comparer) => _comparer = comparer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ComputeHash(T key) => (uint)_comparer.GetHashCode(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(T x, T y) => _comparer.Equals(x, y);
}
