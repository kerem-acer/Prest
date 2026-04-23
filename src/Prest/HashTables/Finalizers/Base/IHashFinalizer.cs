namespace Prest;

/// <summary>
/// Pluggable hash finalizer — a pure <see langword="uint" />→<see langword="uint" />
/// bit-mixing function applied to the raw <c>GetHashCode</c> output before the
/// hashtable uses it for position/tag. Implementations must be
/// <see langword="readonly struct" />s with
/// <see cref="System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining" />
/// so the JIT inlines the mixer at every hash call site.
/// </summary>
/// <remarks>
/// Pick based on key distribution:
/// <list type="bullet">
/// <item><description><see cref="NoOpHashFinalizer" /> — use for already-avalanched hashes (e.g. modern .NET <c>string.GetHashCode</c>), or when the algorithm's internal tag derivation (SwissTable's H2) is sufficient.</description></item>
/// <item><description><see cref="Lowbias32Finalizer" /> — Wellons' lowbias32, lowest avalanche bias per hash-prospector. Use for integer keys or unknown distributions.</description></item>
/// <item><description><see cref="FibonacciFinalizer" /> — single multiply by 2^32/phi. Cheapest non-trivial mixer; good for integer keys.</description></item>
/// <item><description><see cref="XmxFinalizer" /> — MurmurHash3 finalizer. Classic, solid mixing.</description></item>
/// </list>
/// </remarks>
public interface IHashFinalizer
{
    uint Finalize(uint rawHash);
}
