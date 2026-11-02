namespace Prest;

/// <summary>
/// The key an <see cref="IHashAlgorithm{TSlot,TKey}" /> probe-based member searches for: it supplies
/// its own raw hash and recognizes the slot that holds it. This lets a caller search with a key of
/// another type (for example a <see cref="ReadOnlySpan{T}" /> of <see cref="char" /> against
/// <see cref="string" /> keys) without creating a <c>TKey</c>.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are <see langword="struct" />s (usually <see langword="ref struct" />s, so they can
/// hold a span), passed by value and called through a constrained generic, so there is no boxing and
/// no interface dispatch.
/// </para>
/// <para>
/// A probe must agree with the algorithm's hasher: whenever <see cref="Matches" /> returns
/// <see langword="true" /> for a slot, <see cref="ComputeHash" /> must return the hasher's raw hash of
/// that slot's key. Otherwise the probe starts in the wrong place and misses the entry.
/// </para>
/// <para>
/// On .NET 9+ this is public because the alternate-key members of
/// <see cref="IHashAlgorithm{TSlot,TKey}" /> take it. On older targets it is internal; the built-in
/// algorithms' shared loops still name it in their constraints.
/// </para>
/// </remarks>
/// <typeparam name="TSlot">The algorithm's slot type.</typeparam>
#if NET9_0_OR_GREATER
public
#endif
interface IHashProbe<TSlot>
{
    /// <summary>
    /// Raw hash of the key being searched for, before the algorithm applies its finalizer. Must equal
    /// the algorithm hasher's <c>ComputeHash</c> of any key this probe matches.
    /// </summary>
    uint ComputeHash();

    /// <summary>Returns <see langword="true" /> if <paramref name="slot" /> holds the key being searched for.</summary>
    bool Matches(in TSlot slot);
}

/// <summary>
/// An <see cref="IHashProbe{TSlot}" /> that can also build the slot to store when an insert finds no
/// match, so an alternate key is turned into a real key only when it is actually added.
/// </summary>
/// <typeparam name="TSlot">The algorithm's slot type.</typeparam>
#if NET9_0_OR_GREATER
public
#endif
interface IInsertProbe<TSlot> : IHashProbe<TSlot>
{
    /// <summary>
    /// Builds the slot to insert. Algorithms call it at most once per insert, only when the key is
    /// absent, and before they modify the table, so an exception here leaves the table unchanged.
    /// </summary>
    TSlot CreateSlot();
}
