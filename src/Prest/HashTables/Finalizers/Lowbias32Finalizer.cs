using System.Runtime.CompilerServices;

namespace Prest;

/// <summary>
/// Wellons' <c>lowbias32</c> finalizer — two multiplies, three XOR-shifts.
/// Lowest avalanche bias of any 2-op integer mixer per hash-prospector's
/// exhaustive search.
/// </summary>
public readonly struct Lowbias32Finalizer : IHashFinalizer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Finalize(uint rawHash)
    {
        rawHash ^= rawHash >> 16;
        rawHash *= 0x7FEB352Du;
        rawHash ^= rawHash >> 15;
        rawHash *= 0x846CA68Bu;
        rawHash ^= rawHash >> 16;
        return rawHash;
    }
}
