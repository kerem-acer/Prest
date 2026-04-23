using System.Runtime.CompilerServices;

namespace Prest;

/// <summary>
/// MurmurHash3's <c>fmix32</c> finalizer — two multiplies, three XOR-shifts,
/// slightly worse avalanche than <see cref="Lowbias32Finalizer" /> but
/// time-tested and widely deployed.
/// </summary>
public readonly struct XmxFinalizer : IHashFinalizer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Finalize(uint rawHash)
    {
        rawHash ^= rawHash >> 16;
        rawHash *= 0x85EBCA6Bu;
        rawHash ^= rawHash >> 13;
        rawHash *= 0xC2B2AE35u;
        rawHash ^= rawHash >> 16;
        return rawHash;
    }
}
