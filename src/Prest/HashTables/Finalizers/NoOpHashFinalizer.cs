using System.Runtime.CompilerServices;

namespace Prest;

/// <summary>
/// No-op finalizer: passes the raw hash through unchanged. This is the default
/// when the key's <see cref="object.GetHashCode" /> is already well-distributed
/// (e.g. modern <c>string.GetHashCode</c>) or when the algorithm's internal tag
/// derivation (SwissTable's H2) is sufficient mixing on its own.
/// </summary>
public readonly struct NoOpHashFinalizer : IHashFinalizer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Finalize(uint rawHash) => rawHash;
}
