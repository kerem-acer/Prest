using System.Numerics;
using System.Runtime.CompilerServices;
#if NET7_0_OR_GREATER
using System.Runtime.Intrinsics;
#endif

namespace Prest;

/// <summary>
/// SwissTable constants, SIMD/scalar group helpers, hash finalizer, and bucket-count math.
/// Shared across all algorithm implementations (not just <see cref="SwissTableAlgorithm{TSlot,TKey,THasher,TFinalizer}" />).
/// </summary>
static class SwissTableHelpers
{
    public const int GroupWidth = 16;
    public const byte CtrlEmpty = 0xFF;
    public const byte CtrlDeleted = 0x80;

    /// <summary>
    /// Top-bit mask distinguishing live from not-live control bytes. Live H2 tags
    /// occupy <c>0x00..0x7F</c> (top bit clear); <see cref="CtrlEmpty" /> and
    /// <see cref="CtrlDeleted" /> both have the top bit set. So
    /// <c>(ctrl &amp; CtrlLiveBit) == 0</c> means "this slot holds a live entry".
    /// </summary>
    public const byte CtrlLiveBit = 0x80;

    /// <summary>H2 tag mask: live control byte is the hash's top-7-bits fold.</summary>
    public const byte H2Mask = 0x7F;

    /// <summary>
    /// Returns the power-of-two bucket count required to hold <paramref name="capacity" />
    /// entries at the 7/8 load factor, floored at <see cref="GroupWidth" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ComputeBucketCount(int capacity)
    {
        var minBuckets = ((capacity * 8) + 6) / 7;
        return Math.Max(GroupWidth, (int)BitOperations.RoundUpToPowerOf2((uint)minBuckets));
    }

    /// <summary>
    /// 7-bit control tag derived from the hash's top bits (after folding low into high).
    /// Faster.Map's scheme — lets us skip a full finalizer pass on the hot path.
    /// Top bit is always 0 so the byte never collides with <see cref="CtrlEmpty" />
    /// or <see cref="CtrlDeleted" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte H2(uint hash)
    {
        hash ^= hash >> 16;
        return (byte)((hash >> 25) & H2Mask);
    }

    /// <summary>
    /// Starting probe position: low bits of the hash masked to the bucket count.
    /// Unaligned — group loads from any offset are valid thanks to the trailing
    /// mirror region (see <see cref="WriteControl" />).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int InitialPos(uint hash, int bucketMask) => (int)(hash & (uint)bucketMask);

    /// <summary>
    /// Returns <see langword="true" /> if the slot being erased at <paramref name="slotIdx" /> can go
    /// back to <see cref="CtrlEmpty" />, or <see langword="false" /> if it must become a tombstone.
    /// </summary>
    /// <remarks>
    /// A lookup stops at the first group that contains an empty slot, and groups start at any
    /// position. If <paramref name="slotIdx" /> sits inside some group-wide run of non-empty slots,
    /// another key may have probed past that run when it was inserted, and an empty slot there would
    /// end that key's lookup early. So the slot may only become empty when the run of non-empty
    /// slots through it is shorter than a group. Same test as abseil's <c>EraseMetaOnly</c>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanMarkEmpty(ref byte controlBase, int bucketMask, int slotIdx)
    {
        // Bit k of each mask is slot (start + k); the mirror region makes wrapped loads valid.
        var emptyBefore = MatchVector(LoadGroup(ref controlBase, (slotIdx - GroupWidth) & bucketMask), EmptyVector);
        var emptyAfter = MatchVector(LoadGroup(ref controlBase, slotIdx), EmptyVector);
        if (emptyBefore == 0 || emptyAfter == 0)
        {
            return false;
        }

        // Non-empty slots directly before slotIdx, plus slotIdx itself and the non-empty slots after it.
        return LeadingZeroCount16(emptyBefore) + BitOperations.TrailingZeroCount(emptyAfter) < GroupWidth;
    }

    /// <summary>Counts the zero bits above the highest set bit of a non-zero 16-bit mask.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int LeadingZeroCount16(uint mask)
    {
#if NET
        return BitOperations.LeadingZeroCount(mask) - 16;
#else
        var count = 0;
        for (var bit = 1u << 15; (mask & bit) == 0; bit >>= 1)
        {
            count++;
        }
        return count;
#endif
    }

    /// <summary>
    /// Branchless mirror-write. Always performs two stores: one to the primary
    /// position, one to <c>slot + (length &amp; mirrorMask)</c>. When slot &lt; GroupWidth
    /// the mirror mask is all-ones so the write lands in the trailing mirror region;
    /// otherwise the mask is zero so the second store is a no-op on the primary slot.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteControl(ref byte controlBase, int bucketCount, int slotIdx, byte value)
    {
        Unsafe.Add(ref controlBase, (nint)(uint)slotIdx) = value;
        // ((slotIdx - GroupWidth) >> 31) is 0 when slotIdx >= GroupWidth, -1 otherwise.
        var mirrorMask = (uint)((slotIdx - GroupWidth) >> 31);
        var mirrorOffset = (uint)slotIdx + (mirrorMask & (uint)bucketCount);
        Unsafe.Add(ref controlBase, (nint)mirrorOffset) = value;
    }

#if NET7_0_OR_GREATER
    /// <summary>Cached broadcast vector for empty-slot match scans.</summary>
    public static readonly Vector128<byte> EmptyVector = Vector128.Create(CtrlEmpty);

    /// <summary>Cached broadcast vector for deleted-slot match scans.</summary>
    public static readonly Vector128<byte> DeletedVector = Vector128.Create(CtrlDeleted);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> LoadGroup(ref byte controlBase, int pos) =>
        Vector128.LoadUnsafe(ref Unsafe.Add(ref controlBase, (nint)(uint)pos));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint MatchByte(Vector128<byte> group, byte target) =>
        Vector128.Equals(group, Vector128.Create(target)).ExtractMostSignificantBits();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint MatchVector(Vector128<byte> group, Vector128<byte> target) =>
        Vector128.Equals(group, target).ExtractMostSignificantBits();
#else
    public static readonly (ulong Lo, ulong Hi) EmptyVector = (0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul);
    public static readonly (ulong Lo, ulong Hi) DeletedVector = (0x8080808080808080ul, 0x8080808080808080ul);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint MatchVector((ulong Lo, ulong Hi) group, (ulong Lo, ulong Hi) target)
    {
        uint mask = 0;
        var lo = group.Lo;
        var hi = group.Hi;
        var tlo = target.Lo;
        var thi = target.Hi;
        for (var i = 0; i < 8; i++)
        {
            if ((byte)lo == (byte)tlo)
            {
                mask |= 1u << i;
            }
            if ((byte)hi == (byte)thi)
            {
                mask |= 1u << (i + 8);
            }
            lo >>= 8;
            hi >>= 8;
            tlo >>= 8;
            thi >>= 8;
        }
        return mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (ulong Lo, ulong Hi) LoadGroup(ref byte controlBase, int pos)
    {
        ref var b = ref Unsafe.Add(ref controlBase, (nint)(uint)pos);
        var lo = Unsafe.ReadUnaligned<ulong>(ref b);
        var hi = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref b, 8));
        return (lo, hi);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint MatchByte((ulong Lo, ulong Hi) group, byte target)
    {
        uint mask = 0;
        var lo = group.Lo;
        var hi = group.Hi;
        for (var i = 0; i < 8; i++)
        {
            if ((byte)lo == target)
            {
                mask |= 1u << i;
            }
            if ((byte)hi == target)
            {
                mask |= 1u << (i + 8);
            }
            lo >>= 8;
            hi >>= 8;
        }
        return mask;
    }
#endif
}
