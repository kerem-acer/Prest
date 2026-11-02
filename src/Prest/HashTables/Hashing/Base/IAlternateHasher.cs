#if NET9_0_OR_GREATER
namespace Prest;

/// <summary>
/// Struct hasher for looking up <typeparamref name="T" /> keys by another type, such as UTF-8
/// <c>ReadOnlySpan&lt;byte&gt;</c> against <see cref="string" /> keys. It is the struct counterpart of
/// <see cref="IAlternateEqualityComparer{TAlternate,T}" />: pass it to
/// <c>GetAlternateLookup&lt;TAlternate, TAltHasher&gt;()</c> on a map or set and the JIT specializes the
/// lookup for it, with no interface calls.
/// </summary>
/// <remarks>
/// The hasher must agree with the collection's own hasher. Whenever
/// <see cref="Equals(TAlternate,T)" /> returns <see langword="true" />, <see cref="ComputeHash" /> must
/// return the same value as the collection hasher's <c>ComputeHash</c> of that key (the raw hash,
/// before any finalizer), and <see cref="Create" /> must return a key equal to the alternate. The
/// collection cannot check this. If they disagree, lookups miss entries that are present and
/// <c>TryAdd</c>/<c>Add</c> can insert duplicates.
/// </remarks>
/// <typeparam name="TAlternate">The alternate key type; may be a <see langword="ref struct" />.</typeparam>
/// <typeparam name="T">The collection's key type.</typeparam>
public interface IAlternateHasher<in TAlternate, T>
    where TAlternate : allows ref struct
    where T : notnull
{
    /// <summary>
    /// Hashes <paramref name="alternate" />. Must equal the collection hasher's raw hash of the key
    /// that <paramref name="alternate" /> stands for.
    /// </summary>
    uint ComputeHash(TAlternate alternate);

    /// <summary>Returns <see langword="true" /> if <paramref name="alternate" /> stands for <paramref name="other" />.</summary>
    bool Equals(TAlternate alternate, T other);

    /// <summary>Creates the key that <paramref name="alternate" /> stands for. Called only when an alternate key is added.</summary>
    T Create(TAlternate alternate);
}
#endif
