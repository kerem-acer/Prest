namespace Prest;

/// <summary>
/// Struct-specialized hasher used by <see cref="PooledHashSet{T,TAlgo}" /> and
/// <see cref="PooledHashMap{TKey,TValue,TAlgo}" />. Implementations should be
/// <see langword="readonly struct" />s with
/// <see cref="System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining" />
/// on both members so the JIT monomorphizes hash + equality into every call site with
/// zero indirection. Mirrors Faster.Map's <c>IHasher&lt;T&gt;</c> pattern.
/// </summary>
public interface IHasher<in T> where T : notnull
{
    uint ComputeHash(T key);
    bool Equals(T x, T y);
}
