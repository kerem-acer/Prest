#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Prest;

public ref partial struct StackOnlyPooledHashMap<TKey, TValue, TAlgo>
{
    /// <summary>
    /// Returns a view of this map that looks up entries by <typeparamref name="TAlternate" /> instead
    /// of <typeparamref name="TKey" />, for example a <c>ReadOnlySpan&lt;char&gt;</c> for
    /// <see cref="string" /> keys, so a lookup does not have to create a key. Mirrors
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.GetAlternateLookup{TAlternate}" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Works when the map's comparer implements
    /// <see cref="IAlternateEqualityComparer{TAlternate,T}" /> for <typeparamref name="TAlternate" />.
    /// <see cref="EqualityComparer{T}.Default" /> does for <c>ReadOnlySpan&lt;char&gt;</c> on
    /// <see cref="string" /> keys, and so do the built-in <see cref="StringComparer" />s, whether
    /// passed to <see cref="StackOnlyPooledHashMap{TKey,TValue}(int, IEqualityComparer{TKey})" /> or
    /// wrapped in <see cref="ComparerHasher{T}" />. Each hash and equality check is one interface call
    /// to the comparer. For lookups with no interface calls, use
    /// <see cref="GetAlternateLookup{TAlternate,TAltHasher}" />.
    /// </para>
    /// <para>
    /// The view is a <see langword="ref struct" /> that refers to this map's table, not a copy of it,
    /// so writes through it change this map, and the compiler keeps it from outliving the map
    /// variable. It works on a <c>using var</c> local. Unlike the map, the view can be copied and
    /// passed by value freely: every copy refers to the same table.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The map's comparer does not implement <see cref="IAlternateEqualityComparer{TAlternate,T}" />
    /// for <typeparamref name="TAlternate" />.
    /// </exception>
    [UnscopedRef]
    public AlternateLookup<TAlternate> GetAlternateLookup<TAlternate>()
        where TAlternate : notnull, allows ref struct
    {
        if (!TryGetAlternateLookup<TAlternate>(out var lookup))
        {
            PooledHashMap<TKey, TValue, TAlgo>.ThrowIncompatibleComparer(typeof(TAlternate));
        }
        return lookup;
    }

    /// <summary>
    /// Gets a view of this map that looks up entries by <typeparamref name="TAlternate" />, if the
    /// map's comparer supports it. See <see cref="GetAlternateLookup{TAlternate}" />.
    /// </summary>
    /// <returns>
    /// <see langword="false" /> if the map's comparer does not implement
    /// <see cref="IAlternateEqualityComparer{TAlternate,T}" /> for <typeparamref name="TAlternate" />.
    /// </returns>
    [UnscopedRef]
    public bool TryGetAlternateLookup<TAlternate>(out AlternateLookup<TAlternate> lookup)
        where TAlternate : notnull, allows ref struct
    {
        if (_algo.Comparer is IAlternateEqualityComparer<TAlternate, TKey> comparer)
        {
            lookup = new AlternateLookup<TAlternate>(ref _algo, comparer);
            return true;
        }
        lookup = default;
        return false;
    }

    /// <summary>
    /// Returns a view of this map that looks up entries by <typeparamref name="TAlternate" /> using
    /// the struct hasher <typeparamref name="TAltHasher" />, with no interface calls. Mirrors
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.GetAlternateLookup{TAlternate,TAltHasher}" />;
    /// the view refers to this map the same way as <see cref="GetAlternateLookup{TAlternate}" />.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TAltHasher" /> must agree with the map's hasher: for any key it considers
    /// equal, it must return the same raw hash as the map's hasher (before the finalizer) and
    /// <see cref="IAlternateHasher{TAlternate,T}.Create" /> must return an equal key. The map cannot
    /// check this. If they disagree, lookups miss entries that are present and <c>TryAdd</c> can add
    /// duplicate keys.
    /// </remarks>
    /// <param name="hasher">The hasher instance; defaults to <c>default(TAltHasher)</c>.</param>
    [UnscopedRef]
    public AlternateLookup<TAlternate, TAltHasher> GetAlternateLookup<TAlternate, TAltHasher>(TAltHasher hasher = default)
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, TKey>
        => new(ref _algo, hasher);

    /// <summary>
    /// A view of a <see cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}" /> that looks up entries by
    /// <typeparamref name="TAlternate" /> through the map's comparer. Get one from
    /// <see cref="GetAlternateLookup{TAlternate}" />. It refers to the map's table, so reads and
    /// writes go to the map itself, and it allocates nothing except the key that
    /// <see cref="TryAdd" /> creates for a new entry.
    /// </summary>
    /// <typeparam name="TAlternate">The alternate key type; may be a <see langword="ref struct" />.</typeparam>
    public readonly ref struct AlternateLookup<TAlternate>
        where TAlternate : notnull, allows ref struct
    {
        readonly AlternateLookup<TAlternate, AlternateComparerHasher<TAlternate, TKey>> _lookup;

        internal AlternateLookup(ref TAlgo algo, IAlternateEqualityComparer<TAlternate, TKey> comparer)
            => _lookup = new(ref algo, new AlternateComparerHasher<TAlternate, TKey>(comparer));

        /// <summary>Gets the value for <paramref name="key" />.</summary>
        /// <exception cref="KeyNotFoundException">No entry has a key equal to <paramref name="key" />.</exception>
        public TValue this[TAlternate key] => _lookup[key];

        /// <summary>Returns <see langword="true" /> if the map has an entry for <paramref name="key" />.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TAlternate key) => _lookup.ContainsKey(key);

        /// <summary>Gets the value for <paramref name="key" />, if present.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TAlternate key, [MaybeNullWhen(false)] out TValue value)
            => _lookup.TryGetValue(key, out value);

        /// <summary>Gets the stored key and the value for <paramref name="key" />, if present.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TAlternate key, [MaybeNullWhen(false)] out TKey actualKey, [MaybeNullWhen(false)] out TValue value)
            => _lookup.TryGetValue(key, out actualKey, out value);

        /// <summary>
        /// Adds an entry unless one already exists for <paramref name="key" />. The comparer creates the
        /// <typeparamref name="TKey" /> only when the entry is added.
        /// </summary>
        /// <returns><see langword="true" /> if the entry was added; <see langword="false" /> if the key was already present.</returns>
        public bool TryAdd(TAlternate key, TValue value) => _lookup.TryAdd(key, value);

        /// <summary>Removes the entry for <paramref name="key" />, if present.</summary>
        public bool Remove(TAlternate key) => _lookup.Remove(key);

        /// <summary>Removes the entry for <paramref name="key" />, if present, and returns its stored key and value.</summary>
        public bool Remove(TAlternate key, [MaybeNullWhen(false)] out TKey actualKey, [MaybeNullWhen(false)] out TValue value)
            => _lookup.Remove(key, out actualKey, out value);

        /// <summary>Backs <see cref="PooledCollectionsMarshal" />'s alternate-lookup <c>GetValueRefOrNullRef</c>.</summary>
        internal ref TValue GetValueRefOrNullRef(TAlternate key) => ref _lookup.GetValueRefOrNullRef(key);

        /// <summary>Backs <see cref="PooledCollectionsMarshal" />'s alternate-lookup <c>GetValueRefOrAddDefault</c>.</summary>
        internal ref TValue? GetValueRefOrAddDefault(TAlternate key, out bool exists)
            => ref _lookup.GetValueRefOrAddDefault(key, out exists);
    }

    /// <summary>
    /// A view of a <see cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}" /> that looks up entries by
    /// <typeparamref name="TAlternate" /> through the struct hasher <typeparamref name="TAltHasher" />.
    /// Get one from <see cref="GetAlternateLookup{TAlternate,TAltHasher}" />. It refers to the map's
    /// table, so reads and writes go to the map itself, and it allocates nothing except the key that
    /// <see cref="TryAdd" /> creates for a new entry.
    /// </summary>
    /// <typeparam name="TAlternate">The alternate key type; may be a <see langword="ref struct" />.</typeparam>
    /// <typeparam name="TAltHasher">Struct hasher that agrees with the map's hasher.</typeparam>
    public readonly ref struct AlternateLookup<TAlternate, TAltHasher>
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, TKey>
    {
        // A ref field cannot point at the map (a ref struct, CS9050), so the view refers to the
        // algorithm struct inside it: that is where the buffers, count and hasher live.
        readonly ref TAlgo _algo;
        readonly TAltHasher _hasher;

        internal AlternateLookup(ref TAlgo algo, TAltHasher hasher)
        {
            _algo = ref algo;
            _hasher = hasher;
        }

        /// <summary>Gets the value for <paramref name="key" />.</summary>
        /// <exception cref="KeyNotFoundException">No entry has a key equal to <paramref name="key" />.</exception>
        public TValue this[TAlternate key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }
                throw new KeyNotFoundException();
            }
        }

        /// <summary>Returns <see langword="true" /> if the map has an entry for <paramref name="key" />.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TAlternate key) =>
            !Unsafe.IsNullRef(in _algo.FindSlot<PooledHashMap<TKey, TValue, TAlgo>.Extractor, PooledHashMap<TKey, TValue, TAlgo>.AlternateProbe<TAlternate, TAltHasher>>(new(key, _hasher)));

        /// <summary>Gets the value for <paramref name="key" />, if present.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TAlternate key, [MaybeNullWhen(false)] out TValue value)
        {
            ref readonly var slot = ref _algo.FindSlot<PooledHashMap<TKey, TValue, TAlgo>.Extractor, PooledHashMap<TKey, TValue, TAlgo>.AlternateProbe<TAlternate, TAltHasher>>(new(key, _hasher));
            if (Unsafe.IsNullRef(in slot))
            {
                value = default;
                return false;
            }
            value = slot.Value;
            return true;
        }

        /// <summary>Gets the stored key and the value for <paramref name="key" />, if present.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TAlternate key, [MaybeNullWhen(false)] out TKey actualKey, [MaybeNullWhen(false)] out TValue value)
        {
            ref readonly var slot = ref _algo.FindSlot<PooledHashMap<TKey, TValue, TAlgo>.Extractor, PooledHashMap<TKey, TValue, TAlgo>.AlternateProbe<TAlternate, TAltHasher>>(new(key, _hasher));
            if (Unsafe.IsNullRef(in slot))
            {
                actualKey = default;
                value = default;
                return false;
            }
            actualKey = slot.Key;
            value = slot.Value;
            return true;
        }

        /// <summary>
        /// Adds an entry unless one already exists for <paramref name="key" />, in a single probe.
        /// <see cref="IAlternateHasher{TAlternate,T}.Create" /> builds the <typeparamref name="TKey" />
        /// only when the entry is added.
        /// </summary>
        /// <returns><see langword="true" /> if the entry was added; <see langword="false" /> if the key was already present.</returns>
        public bool TryAdd(TAlternate key, TValue value)
        {
            if (!_algo.IsAllocated)
            {
                EnsureInitialized(ref _algo);
            }
            return _algo.Insert<PooledHashMap<TKey, TValue, TAlgo>.Extractor, PooledHashMap<TKey, TValue, TAlgo>.AlternateInsertProbe<TAlternate, TAltHasher>>(
                new(key, _hasher, value), out _);
        }

        /// <summary>Removes the entry for <paramref name="key" />, if present.</summary>
        public bool Remove(TAlternate key) =>
            _algo.Remove<PooledHashMap<TKey, TValue, TAlgo>.Extractor, PooledHashMap<TKey, TValue, TAlgo>.AlternateProbe<TAlternate, TAltHasher>>(
                new(key, _hasher), out _);

        /// <summary>Removes the entry for <paramref name="key" />, if present, and returns its stored key and value.</summary>
        public bool Remove(TAlternate key, [MaybeNullWhen(false)] out TKey actualKey, [MaybeNullWhen(false)] out TValue value)
        {
            if (_algo.Remove<PooledHashMap<TKey, TValue, TAlgo>.Extractor, PooledHashMap<TKey, TValue, TAlgo>.AlternateProbe<TAlternate, TAltHasher>>(
                new(key, _hasher), out var removed))
            {
                actualKey = removed.Key;
                value = removed.Value;
                return true;
            }
            actualKey = default;
            value = default;
            return false;
        }

        /// <summary>Backs <see cref="PooledCollectionsMarshal" />'s alternate-lookup <c>GetValueRefOrNullRef</c>.</summary>
        internal ref TValue GetValueRefOrNullRef(TAlternate key)
        {
            ref var slot = ref Unsafe.AsRef(in _algo.FindSlot<PooledHashMap<TKey, TValue, TAlgo>.Extractor, PooledHashMap<TKey, TValue, TAlgo>.AlternateProbe<TAlternate, TAltHasher>>(new(key, _hasher)));
            if (Unsafe.IsNullRef(ref slot))
            {
                return ref Unsafe.NullRef<TValue>();
            }
            return ref slot.Value;
        }

        /// <summary>
        /// Backs <see cref="PooledCollectionsMarshal" />'s alternate-lookup <c>GetValueRefOrAddDefault</c>.
        /// One probe; <see cref="IAlternateHasher{TAlternate,T}.Create" /> builds the key only on an add.
        /// </summary>
        internal ref TValue? GetValueRefOrAddDefault(TAlternate key, out bool exists)
        {
            if (!_algo.IsAllocated)
            {
                EnsureInitialized(ref _algo);
            }
            exists = !_algo.Insert<PooledHashMap<TKey, TValue, TAlgo>.Extractor, PooledHashMap<TKey, TValue, TAlgo>.AlternateInsertProbe<TAlternate, TAltHasher>>(
                new(key, _hasher, default!), out var slotIdx);
            return ref _algo.SlotArray![slotIdx].Value!;
        }
    }
}
#endif
