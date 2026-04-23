using System.Runtime.CompilerServices;

namespace Prest;

/// <summary>
/// Default hasher backed by <see cref="EqualityComparer{T}.Default" />. For integer
/// and other value types the JIT devirtualizes the call to a direct non-boxing path.
/// </summary>
public readonly struct EqualityDefaultHasher<T> : IHasher<T> where T : notnull
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ComputeHash(T key) => (uint)EqualityComparer<T>.Default.GetHashCode(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(T x, T y) => EqualityComparer<T>.Default.Equals(x, y);
}
