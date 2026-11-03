#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;

namespace Prest;

/// <summary>
/// Finds the <see cref="IEqualityComparer{T}" /> behind one of Prest's built-in hashers, which is
/// what the algorithms report as <see cref="IHashAlgorithm{TSlot,TKey}.Comparer" />.
/// </summary>
static class HasherComparer
{
    /// <summary>
    /// Returns <see cref="EqualityComparer{T}.Default" /> for <see cref="EqualityDefaultHasher{T}" />,
    /// the wrapped comparer for <see cref="ComparerHasher{T}" />, the comparer in use for
    /// <see cref="OptionalComparerHasher{T}" />, and <see langword="null" /> for any other hasher.
    /// The <see langword="typeof" /> checks are JIT-time constants, so each closed
    /// algorithm compiles to just its own branch.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEqualityComparer<T>? Get<T, THasher>(ref readonly THasher hasher)
        where T : notnull
        where THasher : struct, IHasher<T>
    {
        if (typeof(THasher) == typeof(EqualityDefaultHasher<T>))
        {
            return EqualityComparer<T>.Default;
        }

        if (typeof(THasher) == typeof(ComparerHasher<T>))
        {
            return Unsafe.As<THasher, ComparerHasher<T>>(ref Unsafe.AsRef(in hasher)).Comparer;
        }

        if (typeof(THasher) == typeof(OptionalComparerHasher<T>))
        {
            return Unsafe.As<THasher, OptionalComparerHasher<T>>(ref Unsafe.AsRef(in hasher)).Comparer;
        }

        return null;
    }
}
#endif
