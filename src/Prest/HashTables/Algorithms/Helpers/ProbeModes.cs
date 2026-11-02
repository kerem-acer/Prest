using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Prest;

// Mode markers for the algorithms' shared find/insert/remove loops. A loop
// instantiated with KeyMode hashes and compares the key with the algorithm's own
// hasher; one instantiated with ProbeMode asks the IHashProbe. They are
// non-generic, so `typeof(TMode) == typeof(KeyMode)` is a JIT-time constant even
// in shared generic code, and each instantiation compiles only its own branch.

/// <summary>Shared-loop mode: search by key with the algorithm's own hasher.</summary>
readonly struct KeyMode
{
    /// <summary>
    /// <see langword="true" /> when <typeparamref name="TMode" /> is <see cref="KeyMode" />. Folds to a
    /// constant; a call keeps the loops' IL small, which the JIT's inlining budget is based on.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Is<TMode>() => typeof(TMode) == typeof(KeyMode);
}

/// <summary>Shared-loop mode: search with an <see cref="IHashProbe{TSlot}" />.</summary>
readonly struct ProbeMode;

/// <summary>
/// Probe argument for <see cref="KeyMode" /> calls, which never use it. It only satisfies the
/// loops' <c>TProbe</c> constraint.
/// </summary>
[ExcludeFromCodeCoverage]
readonly struct NoProbe<TSlot> : IInsertProbe<TSlot>
{
    public uint ComputeHash() => throw new InvalidOperationException();

    public bool Matches(in TSlot slot) => throw new InvalidOperationException();

    public TSlot CreateSlot() => throw new InvalidOperationException();
}
