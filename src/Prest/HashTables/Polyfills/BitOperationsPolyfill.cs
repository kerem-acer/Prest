#if !NET6_0_OR_GREATER

namespace System.Numerics;

// ---------------------------------------------------------------------------
// Polyfill for System.Numerics.BitOperations on netstandard2.0 / netstandard2.1.
//
// On net6+ this file compiles to nothing — the BCL's BitOperations is used.
// On older TFMs we declare a stub type so `BitOperations.XXX(...)` resolves,
// and add the missing members via the C# 14 extension-member syntax.
// ---------------------------------------------------------------------------

/// <summary>Polyfill stub for <c>System.Numerics.BitOperations</c>. Members are
/// added via extension declarations below; keeping the stub minimal keeps this
/// file's diff against the BCL trivial.</summary>
public static class BitOperations
{
}

/// <summary>C# 14 extension members adding missing <see cref="BitOperations" />
/// methods for pre-net6 TFMs.</summary>
public static class BitOperationsPolyfill
{
    extension(BitOperations)
    {
        /// <summary>
        /// Round up to the smallest power of 2 that is &gt;= <paramref name="value" />.
        /// Matches the BCL signature on net6+.
        /// </summary>
        public static uint RoundUpToPowerOf2(uint value)
        {
            --value;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        /// <summary>
        /// Count trailing zero bits. Matches the BCL signature on net6+.
        /// Uses the de Bruijn sequence trick — constant-time, branch-free past the zero-check.
        /// </summary>
        public static int TrailingZeroCount(uint value)
        {
            if (value == 0)
            {
                return 32;
            }
            var lowBit = value & (uint)-(int)value;
            return BitOperationsPolyfillTables.TrailingZeroCountDeBruijn32[(lowBit * 0x077CB531u) >> 27];
        }
    }
}

static class BitOperationsPolyfillTables
{
    internal static readonly byte[] TrailingZeroCountDeBruijn32 =
    [
         0,  1, 28,  2, 29, 14, 24,  3,
        30, 22, 20, 15, 25, 17,  4,  8,
        31, 27, 13, 23, 21, 19, 16,  7,
        26, 12, 18,  6, 11,  5, 10,  9,
    ];
}

#endif
