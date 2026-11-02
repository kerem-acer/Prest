#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Prest;

public partial class PooledHashMap<TKey, TValue, TAlgo>
{
    /// <summary>
    /// Returns a view of this map that looks up entries by <typeparamref name="TAlternate" /> instead
    /// of <typeparamref name="TKey" />, for example a <c>ReadOnlySpan&lt;char&gt;</c> for
    /// <see cref="string" /> keys, so a lookup does not have to create a key. Mirrors
    /// <see cref="Dictionary{TKey,TValue}.GetAlternateLookup{TAlternate}" />.
    /// </summary>
    /// <remarks>
    /// Works when the map's comparer implements
    /// <see cref="IAlternateEqualityComparer{TAlternate,T}" /> for <typeparamref name="TAlternate" />.
    /// The default hasher uses <see cref="EqualityComparer{T}.Default" />, which does for
    /// <c>ReadOnlySpan&lt;char&gt;</c> on <see cref="string" /> keys, as do the built-in
    /// <see cref="StringComparer" />s. Each hash and equality check is one interface call to the
    /// comparer, like <see cref="Dictionary{TKey,TValue}" />. For lookups with no interface calls,
    /// use <see cref="GetAlternateLookup{TAlternate,TAltHasher}" />.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The map's comparer does not implement <see cref="IAlternateEqualityComparer{TAlternate,T}" />
    /// for <typeparamref name="TAlternate" />.
    /// </exception>
    public AlternateLookup<TAlternate> GetAlternateLookup<TAlternate>()
        where TAlternate : notnull, allows ref struct
    {
        if (!TryGetAlternateLookup<TAlternate>(out var lookup))
        {
            ThrowIncompatibleComparer(typeof(TAlternate));
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
    public bool TryGetAlternateLookup<TAlternate>(out AlternateLookup<TAlternate> lookup)
        where TAlternate : notnull, allows ref struct
    {
        if (_algo.Comparer is IAlternateEqualityComparer<TAlternate, TKey> comparer)
        {
            lookup = new AlternateLookup<TAlternate>(this, comparer);
            return true;
        }
        lookup = default;
        return false;
    }

    /// <summary>
    /// Returns a view of this map that looks up entries by <typeparamref name="TAlternate" /> using the
    /// struct hasher <typeparamref name="TAltHasher" />. The JIT specializes the lookup for the hasher,
    /// so there are no interface calls, and it works whatever the map's hasher is, as long as the two
    /// agree.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TAltHasher" /> must agree with the map's hasher: for any key it considers
    /// equal, it must return the same raw hash as the map's hasher (before the finalizer) and
    /// <see cref="IAlternateHasher{TAlternate,T}.Create" /> must return an equal key. The map cannot
    /// check this. If they disagree, lookups miss entries that are present and <c>TryAdd</c> can add
    /// duplicate keys.
    /// </remarks>
    /// <param name="hasher">The hasher instance; defaults to <c>default(TAltHasher)</c>.</param>
    public AlternateLookup<TAlternate, TAltHasher> GetAlternateLookup<TAlternate, TAltHasher>(TAltHasher hasher = default)
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, TKey>
        => new(this, hasher);

    [DoesNotReturn]
    static void ThrowIncompatibleComparer(Type alternateType) =>
        throw new InvalidOperationException(
            $"The map's comparer does not implement IAlternateEqualityComparer<{alternateType.Name}, {typeof(TKey).Name}>. " +
            "Use a comparer that does, or pass an IAlternateHasher to GetAlternateLookup<TAlternate, TAltHasher>().");

    /// <summary>
    /// A view of a <see cref="PooledHashMap{TKey,TValue,TAlgo}" /> that looks up entries by
    /// <typeparamref name="TAlternate" /> through the map's comparer. Get one from
    /// <see cref="GetAlternateLookup{TAlternate}" />. It reads and writes the map itself, holds no
    /// state of its own, and allocates nothing except the key that <see cref="TryAdd" /> creates for a
    /// new entry.
    /// </summary>
    /// <typeparam name="TAlternate">The alternate key type; may be a <see langword="ref struct" />.</typeparam>
    public readonly struct AlternateLookup<TAlternate>
        where TAlternate : notnull, allows ref struct
    {
        readonly AlternateLookup<TAlternate, AlternateComparerHasher<TAlternate, TKey>> _lookup;

        internal AlternateLookup(PooledHashMap<TKey, TValue, TAlgo> map, IAlternateEqualityComparer<TAlternate, TKey> comparer)
            => _lookup = new(map, new AlternateComparerHasher<TAlternate, TKey>(comparer));

        /// <summary>The map this lookup reads and writes.</summary>
        public PooledHashMap<TKey, TValue, TAlgo> Map => _lookup.Map;

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
    }

    /// <summary>
    /// A view of a <see cref="PooledHashMap{TKey,TValue,TAlgo}" /> that looks up entries by
    /// <typeparamref name="TAlternate" /> through the struct hasher <typeparamref name="TAltHasher" />.
    /// Get one from <see cref="GetAlternateLookup{TAlternate,TAltHasher}" />. It reads and writes the
    /// map itself and allocates nothing except the key that <see cref="TryAdd" /> creates for a new
    /// entry.
    /// </summary>
    /// <typeparam name="TAlternate">The alternate key type; may be a <see langword="ref struct" />.</typeparam>
    /// <typeparam name="TAltHasher">Struct hasher that agrees with the map's hasher.</typeparam>
    public readonly struct AlternateLookup<TAlternate, TAltHasher>
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, TKey>
    {
        readonly PooledHashMap<TKey, TValue, TAlgo> _map;
        readonly TAltHasher _hasher;

        internal AlternateLookup(PooledHashMap<TKey, TValue, TAlgo> map, TAltHasher hasher)
        {
            _map = map;
            _hasher = hasher;
        }

        /// <summary>The map this lookup reads and writes.</summary>
        public PooledHashMap<TKey, TValue, TAlgo> Map => _map;

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
            !Unsafe.IsNullRef(in _map._algo.FindSlot<Extractor, AlternateProbe<TAlternate, TAltHasher>>(new(key, _hasher)));

        /// <summary>Gets the value for <paramref name="key" />, if present.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TAlternate key, [MaybeNullWhen(false)] out TValue value)
        {
            ref readonly var slot = ref _map._algo.FindSlot<Extractor, AlternateProbe<TAlternate, TAltHasher>>(new(key, _hasher));
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
            ref readonly var slot = ref _map._algo.FindSlot<Extractor, AlternateProbe<TAlternate, TAltHasher>>(new(key, _hasher));
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
            var map = _map;
            if (!map._algo.IsAllocated)
            {
                map.EnsureInitialized();
            }
            return map._algo.Insert<Extractor, AlternateInsertProbe<TAlternate, TAltHasher>>(
                new(key, _hasher, value), out _);
        }

        /// <summary>Removes the entry for <paramref name="key" />, if present.</summary>
        public bool Remove(TAlternate key) =>
            _map._algo.Remove<Extractor, AlternateProbe<TAlternate, TAltHasher>>(
                new(key, _hasher), out _);

        /// <summary>Removes the entry for <paramref name="key" />, if present, and returns its stored key and value.</summary>
        public bool Remove(TAlternate key, [MaybeNullWhen(false)] out TKey actualKey, [MaybeNullWhen(false)] out TValue value)
        {
            if (_map._algo.Remove<Extractor, AlternateProbe<TAlternate, TAltHasher>>(
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
    }

    readonly ref struct AlternateProbe<TAlternate, TAltHasher> : IHashProbe<KeyValueSlot<TKey, TValue>>
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, TKey>
    {
        readonly TAlternate _key;
        readonly TAltHasher _hasher;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AlternateProbe(TAlternate key, TAltHasher hasher)
        {
            _key = key;
            _hasher = hasher;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ComputeHash() => Unsafe.AsRef(in _hasher).ComputeHash(_key);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(in KeyValueSlot<TKey, TValue> slot) => Unsafe.AsRef(in _hasher).Equals(_key, slot.Key);
    }

    readonly ref struct AlternateInsertProbe<TAlternate, TAltHasher> : IInsertProbe<KeyValueSlot<TKey, TValue>>
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, TKey>
    {
        readonly TAlternate _key;
        readonly TAltHasher _hasher;
        readonly TValue _value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AlternateInsertProbe(TAlternate key, TAltHasher hasher, TValue value)
        {
            _key = key;
            _hasher = hasher;
            _value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ComputeHash() => Unsafe.AsRef(in _hasher).ComputeHash(_key);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(in KeyValueSlot<TKey, TValue> slot) => Unsafe.AsRef(in _hasher).Equals(_key, slot.Key);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KeyValueSlot<TKey, TValue> CreateSlot() => new(Unsafe.AsRef(in _hasher).Create(_key), _value);
    }
}
#endif
