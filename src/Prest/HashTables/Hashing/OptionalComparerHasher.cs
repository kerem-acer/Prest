using System.Runtime.CompilerServices;

namespace Prest;

/// <summary>
/// Hasher behind the default <see cref="PooledHashMap{TKey,TValue}" /> and
/// <see cref="PooledHashSet{T}" />: uses an <see cref="IEqualityComparer{T}" /> when one is
/// supplied and <see cref="EqualityComparer{T}.Default" /> otherwise, the way
/// <see cref="Dictionary{TKey,TValue}" /> does.
/// </summary>
/// <remarks>
/// Without a comparer, hashing and equality go straight to <see cref="EqualityComparer{T}.Default" />,
/// which the JIT devirtualizes. Every call still tests the comparer field first. That costs
/// little next to hashing a string, but is measurable for integer keys. For the fastest
/// default-equality path, use <see cref="EqualityDefaultHasher{T}" /> (<c>SwissHashMap&lt;K,V&gt;</c>).
/// </remarks>
public readonly struct OptionalComparerHasher<T> : IHasher<T> where T : notnull
{
    readonly IEqualityComparer<T>? _comparer;

    /// <summary>
    /// Wraps <paramref name="comparer" />. <see langword="null" /> and
    /// <see cref="EqualityComparer{T}.Default" /> both select the default path.
    /// </summary>
    public OptionalComparerHasher(IEqualityComparer<T>? comparer) => _comparer = Normalize(comparer);

    /// <summary>The comparer in use: the supplied one, or <see cref="EqualityComparer{T}.Default" />.</summary>
    internal IEqualityComparer<T> Comparer => _comparer ?? EqualityComparer<T>.Default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ComputeHash(T key) => _comparer is null
        ? (uint)EqualityComparer<T>.Default.GetHashCode(key)
        : (uint)_comparer.GetHashCode(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(T x, T y) => _comparer is null
        ? EqualityComparer<T>.Default.Equals(x, y)
        : _comparer.Equals(x, y);

    /// <summary>Maps <see cref="EqualityComparer{T}.Default" /> to <see langword="null" />, the default path.</summary>
    internal static IEqualityComparer<T>? Normalize(IEqualityComparer<T>? comparer)
        => comparer is null || ReferenceEquals(comparer, EqualityComparer<T>.Default) ? null : comparer;
}
