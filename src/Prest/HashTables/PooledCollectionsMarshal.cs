namespace Prest;

/// <summary>
/// Ref-returning access to <see cref="PooledHashMap{TKey,TValue,TAlgo}" /> values, the Prest
/// counterpart of <c>System.Runtime.InteropServices.CollectionsMarshal</c> for
/// <see cref="Dictionary{TKey,TValue}" />. Read or update a value in place with a single lookup:
/// <code>
/// ref var count = ref PooledCollectionsMarshal.GetValueRefOrAddDefault(counts, word, out _);
/// count++;
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// Every overload works on any map type, aliases included (<c>PooledHashMap&lt;K,V&gt;</c>,
/// <c>ComparerSwissHashMap&lt;K,V&gt;</c>, …), and on the stack-only
/// <see cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}" /> and
/// <see cref="StackOnlyPooledHashMap{TKey,TValue}" />. The alternate-lookup overloads take the
/// views returned by <c>GetAlternateLookup</c>, so a <c>ReadOnlySpan&lt;byte&gt;</c> can find or add
/// the value for a <c>ReadOnlyMemory&lt;byte&gt;</c> key.
/// </para>
/// <para>
/// The stack-only overloads take the map by <see langword="in" /> so that a <c>using var</c> local,
/// which cannot be passed by <see langword="ref" />, can be passed. They still read and write the
/// map you pass, not a copy: <c>GetValueRefOrAddDefault</c> adds the entry to it. Pass the map
/// variable itself. A method result or other value is copied into a temporary first, and the
/// entry would be added to that copy.
/// </para>
/// <para>
/// A returned reference points into the map's rented slot array and is valid only until the map
/// changes. Adding or removing an entry, <c>Clear</c>, <c>Dispose</c> or <c>Return</c> can move
/// entries or give the array back to <see cref="System.Buffers.ArrayPool{T}" />. After that, the
/// reference reads stale data or writes into an array someone else has rented. Use it right away
/// and don't keep it across such calls. Writing through it changes only the value, never the key.
/// </para>
/// </remarks>
public static class PooledCollectionsMarshal
{
    /// <summary>
    /// Gets a reference to the value for <paramref name="key" /> in <paramref name="map" />, or a null
    /// reference if the map has no entry for it.
    /// </summary>
    /// <returns>
    /// A reference to the value, or a null reference when <paramref name="key" /> is absent; check with
    /// <c>Unsafe.IsNullRef</c>. Valid only until the map is next changed.
    /// </returns>
    public static ref TValue GetValueRefOrNullRef<TKey, TValue, TAlgo>(PooledHashMap<TKey, TValue, TAlgo> map, TKey key)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
        => ref map.GetValueRefOrNullRef(key);

    /// <summary>
    /// Gets a reference to the value for <paramref name="key" /> in <paramref name="map" />, adding an
    /// entry with a <see langword="default" /> value first if there is none. One probe either way.
    /// </summary>
    /// <param name="map">The map to look up or add to.</param>
    /// <param name="key">The key to find or add.</param>
    /// <param name="exists">
    /// <see langword="true" /> if the entry was already there; <see langword="false" /> if it was just added.
    /// </param>
    /// <returns>A reference to the value. Valid only until the map is next changed.</returns>
    public static ref TValue? GetValueRefOrAddDefault<TKey, TValue, TAlgo>(PooledHashMap<TKey, TValue, TAlgo> map, TKey key, out bool exists)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
        => ref map.GetValueRefOrAddDefault(key, out exists);

    /// <summary>
    /// Gets a reference to the value for <paramref name="key" /> in the stack-only
    /// <paramref name="map" />, or a null reference if the map has no entry for it.
    /// </summary>
    /// <param name="map">
    /// The map variable, by <see langword="in" /> so a <c>using var</c> local can be passed; see the
    /// class remarks.
    /// </param>
    /// <param name="key">The key to find.</param>
    /// <returns>
    /// A reference to the value, or a null reference when <paramref name="key" /> is absent; check with
    /// <c>Unsafe.IsNullRef</c>. Valid only until the map is next changed.
    /// </returns>
    public static ref TValue GetValueRefOrNullRef<TKey, TValue, TAlgo>(in StackOnlyPooledHashMap<TKey, TValue, TAlgo> map, TKey key)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
        => ref StackOnlyPooledHashMap<TKey, TValue, TAlgo>.GetValueRefOrNullRef(in map, key);

    /// <summary>
    /// Gets a reference to the value for <paramref name="key" /> in the stack-only
    /// <paramref name="map" />, adding an entry with a <see langword="default" /> value first if there
    /// is none. One probe either way.
    /// </summary>
    /// <param name="map">
    /// The map variable, by <see langword="in" /> so a <c>using var</c> local can be passed. The entry
    /// is added to this map, not to a copy; see the class remarks.
    /// </param>
    /// <param name="key">The key to find or add.</param>
    /// <param name="exists">
    /// <see langword="true" /> if the entry was already there; <see langword="false" /> if it was just added.
    /// </param>
    /// <returns>A reference to the value. Valid only until the map is next changed.</returns>
    public static ref TValue? GetValueRefOrAddDefault<TKey, TValue, TAlgo>(in StackOnlyPooledHashMap<TKey, TValue, TAlgo> map, TKey key, out bool exists)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
        => ref StackOnlyPooledHashMap<TKey, TValue, TAlgo>.GetValueRefOrAddDefault(in map, key, out exists);

    /// <inheritdoc cref="GetValueRefOrNullRef{TKey,TValue,TAlgo}(in StackOnlyPooledHashMap{TKey,TValue,TAlgo}, TKey)" />
    public static ref TValue GetValueRefOrNullRef<TKey, TValue>(in StackOnlyPooledHashMap<TKey, TValue> map, TKey key)
        where TKey : notnull
        => ref StackOnlyPooledHashMap<TKey, TValue>.GetValueRefOrNullRef(in map, key);

    /// <inheritdoc cref="GetValueRefOrAddDefault{TKey,TValue,TAlgo}(in StackOnlyPooledHashMap{TKey,TValue,TAlgo}, TKey, out bool)" />
    public static ref TValue? GetValueRefOrAddDefault<TKey, TValue>(in StackOnlyPooledHashMap<TKey, TValue> map, TKey key, out bool exists)
        where TKey : notnull
        => ref StackOnlyPooledHashMap<TKey, TValue>.GetValueRefOrAddDefault(in map, key, out exists);

#if NET9_0_OR_GREATER
    /// <summary>
    /// Gets a reference to the value for the alternate <paramref name="key" />, or a null reference if
    /// the map has no entry for it.
    /// </summary>
    /// <param name="lookup">A view from <c>GetAlternateLookup&lt;TAlternate&gt;()</c>.</param>
    /// <param name="key">The alternate key to find.</param>
    /// <returns>
    /// A reference to the value, or a null reference when <paramref name="key" /> is absent; check with
    /// <c>Unsafe.IsNullRef</c>. Valid only until the map is next changed.
    /// </returns>
    public static ref TValue GetValueRefOrNullRef<TKey, TValue, TAlgo, TAlternate>(
        PooledHashMap<TKey, TValue, TAlgo>.AlternateLookup<TAlternate> lookup, TAlternate key)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
        where TAlternate : notnull, allows ref struct
        => ref lookup.GetValueRefOrNullRef(key);

    /// <summary>
    /// Gets a reference to the value for the alternate <paramref name="key" />, adding an entry with a
    /// <see langword="default" /> value first if there is none. The map's comparer creates the
    /// <typeparamref name="TKey" /> only when the entry is added.
    /// </summary>
    /// <param name="lookup">A view from <c>GetAlternateLookup&lt;TAlternate&gt;()</c>.</param>
    /// <param name="key">The alternate key to find or add.</param>
    /// <param name="exists">
    /// <see langword="true" /> if the entry was already there; <see langword="false" /> if it was just added.
    /// </param>
    /// <returns>A reference to the value. Valid only until the map is next changed.</returns>
    public static ref TValue? GetValueRefOrAddDefault<TKey, TValue, TAlgo, TAlternate>(
        PooledHashMap<TKey, TValue, TAlgo>.AlternateLookup<TAlternate> lookup, TAlternate key, out bool exists)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
        where TAlternate : notnull, allows ref struct
        => ref lookup.GetValueRefOrAddDefault(key, out exists);

    /// <summary>
    /// Gets a reference to the value for the alternate <paramref name="key" />, or a null reference if
    /// the map has no entry for it.
    /// </summary>
    /// <param name="lookup">A view from <c>GetAlternateLookup&lt;TAlternate, TAltHasher&gt;()</c>.</param>
    /// <param name="key">The alternate key to find.</param>
    /// <returns>
    /// A reference to the value, or a null reference when <paramref name="key" /> is absent; check with
    /// <c>Unsafe.IsNullRef</c>. Valid only until the map is next changed.
    /// </returns>
    public static ref TValue GetValueRefOrNullRef<TKey, TValue, TAlgo, TAlternate, TAltHasher>(
        PooledHashMap<TKey, TValue, TAlgo>.AlternateLookup<TAlternate, TAltHasher> lookup, TAlternate key)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, TKey>
        => ref lookup.GetValueRefOrNullRef(key);

    /// <summary>
    /// Gets a reference to the value for the alternate <paramref name="key" />, adding an entry with a
    /// <see langword="default" /> value first if there is none.
    /// <see cref="IAlternateHasher{TAlternate,T}.Create" /> builds the <typeparamref name="TKey" /> only
    /// when the entry is added.
    /// </summary>
    /// <param name="lookup">A view from <c>GetAlternateLookup&lt;TAlternate, TAltHasher&gt;()</c>.</param>
    /// <param name="key">The alternate key to find or add.</param>
    /// <param name="exists">
    /// <see langword="true" /> if the entry was already there; <see langword="false" /> if it was just added.
    /// </param>
    /// <returns>A reference to the value. Valid only until the map is next changed.</returns>
    public static ref TValue? GetValueRefOrAddDefault<TKey, TValue, TAlgo, TAlternate, TAltHasher>(
        PooledHashMap<TKey, TValue, TAlgo>.AlternateLookup<TAlternate, TAltHasher> lookup, TAlternate key, out bool exists)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, TKey>
        => ref lookup.GetValueRefOrAddDefault(key, out exists);

    /// <summary>
    /// Gets a reference to the value for the alternate <paramref name="key" /> in a stack-only map, or
    /// a null reference if the map has no entry for it.
    /// </summary>
    /// <param name="lookup">
    /// A view from the stack-only map's <c>GetAlternateLookup&lt;TAlternate&gt;()</c>. It refers to the
    /// map, so it is passed by value, and a <c>using var</c> map works.
    /// </param>
    /// <param name="key">The alternate key to find.</param>
    /// <returns>
    /// A reference to the value, or a null reference when <paramref name="key" /> is absent; check with
    /// <c>Unsafe.IsNullRef</c>. Valid only until the map is next changed.
    /// </returns>
    public static ref TValue GetValueRefOrNullRef<TKey, TValue, TAlgo, TAlternate>(
        StackOnlyPooledHashMap<TKey, TValue, TAlgo>.AlternateLookup<TAlternate> lookup, TAlternate key)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
        where TAlternate : notnull, allows ref struct
        => ref lookup.GetValueRefOrNullRef(key);

    /// <summary>
    /// Gets a reference to the value for the alternate <paramref name="key" /> in a stack-only map,
    /// adding an entry with a <see langword="default" /> value first if there is none. The map's
    /// comparer creates the <typeparamref name="TKey" /> only when the entry is added.
    /// </summary>
    /// <param name="lookup">
    /// A view from the stack-only map's <c>GetAlternateLookup&lt;TAlternate&gt;()</c>. It refers to the
    /// map, so it is passed by value, and a <c>using var</c> map works.
    /// </param>
    /// <param name="key">The alternate key to find or add.</param>
    /// <param name="exists">
    /// <see langword="true" /> if the entry was already there; <see langword="false" /> if it was just added.
    /// </param>
    /// <returns>A reference to the value. Valid only until the map is next changed.</returns>
    public static ref TValue? GetValueRefOrAddDefault<TKey, TValue, TAlgo, TAlternate>(
        StackOnlyPooledHashMap<TKey, TValue, TAlgo>.AlternateLookup<TAlternate> lookup, TAlternate key, out bool exists)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
        where TAlternate : notnull, allows ref struct
        => ref lookup.GetValueRefOrAddDefault(key, out exists);

    /// <summary>
    /// Gets a reference to the value for the alternate <paramref name="key" /> in a stack-only map, or
    /// a null reference if the map has no entry for it.
    /// </summary>
    /// <param name="lookup">
    /// A view from the stack-only map's <c>GetAlternateLookup&lt;TAlternate, TAltHasher&gt;()</c>. It
    /// refers to the map, so it is passed by value, and a <c>using var</c> map works.
    /// </param>
    /// <param name="key">The alternate key to find.</param>
    /// <returns>
    /// A reference to the value, or a null reference when <paramref name="key" /> is absent; check with
    /// <c>Unsafe.IsNullRef</c>. Valid only until the map is next changed.
    /// </returns>
    public static ref TValue GetValueRefOrNullRef<TKey, TValue, TAlgo, TAlternate, TAltHasher>(
        StackOnlyPooledHashMap<TKey, TValue, TAlgo>.AlternateLookup<TAlternate, TAltHasher> lookup, TAlternate key)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, TKey>
        => ref lookup.GetValueRefOrNullRef(key);

    /// <summary>
    /// Gets a reference to the value for the alternate <paramref name="key" /> in a stack-only map,
    /// adding an entry with a <see langword="default" /> value first if there is none.
    /// <see cref="IAlternateHasher{TAlternate,T}.Create" /> builds the <typeparamref name="TKey" /> only
    /// when the entry is added.
    /// </summary>
    /// <param name="lookup">
    /// A view from the stack-only map's <c>GetAlternateLookup&lt;TAlternate, TAltHasher&gt;()</c>. It
    /// refers to the map, so it is passed by value, and a <c>using var</c> map works.
    /// </param>
    /// <param name="key">The alternate key to find or add.</param>
    /// <param name="exists">
    /// <see langword="true" /> if the entry was already there; <see langword="false" /> if it was just added.
    /// </param>
    /// <returns>A reference to the value. Valid only until the map is next changed.</returns>
    public static ref TValue? GetValueRefOrAddDefault<TKey, TValue, TAlgo, TAlternate, TAltHasher>(
        StackOnlyPooledHashMap<TKey, TValue, TAlgo>.AlternateLookup<TAlternate, TAltHasher> lookup, TAlternate key, out bool exists)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, TKey>
        => ref lookup.GetValueRefOrAddDefault(key, out exists);
#endif
}
