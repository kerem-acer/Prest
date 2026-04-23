using System.Runtime.CompilerServices;

namespace Prest;

/// <summary>
/// Fibonacci hashing: multiply by <c>2^32 / phi ≈ 0x9E3779B9</c>. One
/// instruction. Works well for sequential integer keys — spreads them
/// pseudo-randomly across the high bits.
/// </summary>
public readonly struct FibonacciFinalizer : IHashFinalizer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Finalize(uint rawHash) => rawHash * 0x9E3779B9u;
}
