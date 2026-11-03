#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Prest;

public ref partial struct StackOnlyPooledHashSet<T, TAlgo>
{
    /// <summary>
    /// Returns a view of this set that looks up items by <typeparamref name="TAlternate" /> instead of
    /// <typeparamref name="T" />, for example a <c>ReadOnlySpan&lt;char&gt;</c> for
    /// <see cref="string" /> items, so a lookup does not have to create an item. Mirrors
    /// <see cref="PooledHashSet{T,TAlgo}.GetAlternateLookup{TAlternate}" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Works when the set's comparer implements
    /// <see cref="IAlternateEqualityComparer{TAlternate,T}" /> for <typeparamref name="TAlternate" />.
    /// <see cref="EqualityComparer{T}.Default" /> does for <c>ReadOnlySpan&lt;char&gt;</c> on
    /// <see cref="string" /> items, and so do the built-in <see cref="StringComparer" />s, whether
    /// passed to <see cref="StackOnlyPooledHashSet{T}(int, IEqualityComparer{T})" /> or wrapped in
    /// <see cref="ComparerHasher{T}" />. Each hash and equality check is one interface call to the
    /// comparer. For lookups with no interface calls, use
    /// <see cref="GetAlternateLookup{TAlternate,TAltHasher}" />.
    /// </para>
    /// <para>
    /// The view is a <see langword="ref struct" /> that refers to this set's table, not a copy of it,
    /// so writes through it change this set, and the compiler keeps it from outliving the set
    /// variable. It works on a <c>using var</c> local. Unlike the set, the view can be copied and
    /// passed by value freely: every copy refers to the same table.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The set's comparer does not implement <see cref="IAlternateEqualityComparer{TAlternate,T}" />
    /// for <typeparamref name="TAlternate" />.
    /// </exception>
    [UnscopedRef]
    public AlternateLookup<TAlternate> GetAlternateLookup<TAlternate>()
        where TAlternate : notnull, allows ref struct
    {
        if (!TryGetAlternateLookup<TAlternate>(out var lookup))
        {
            PooledHashSet<T, TAlgo>.ThrowIncompatibleComparer(typeof(TAlternate));
        }
        return lookup;
    }

    /// <summary>
    /// Gets a view of this set that looks up items by <typeparamref name="TAlternate" />, if the set's
    /// comparer supports it. See <see cref="GetAlternateLookup{TAlternate}" />.
    /// </summary>
    /// <returns>
    /// <see langword="false" /> if the set's comparer does not implement
    /// <see cref="IAlternateEqualityComparer{TAlternate,T}" /> for <typeparamref name="TAlternate" />.
    /// </returns>
    [UnscopedRef]
    public bool TryGetAlternateLookup<TAlternate>(out AlternateLookup<TAlternate> lookup)
        where TAlternate : notnull, allows ref struct
    {
        if (_algo.Comparer is IAlternateEqualityComparer<TAlternate, T> comparer)
        {
            lookup = new AlternateLookup<TAlternate>(ref _algo, comparer);
            return true;
        }
        lookup = default;
        return false;
    }

    /// <summary>
    /// Returns a view of this set that looks up items by <typeparamref name="TAlternate" /> using the
    /// struct hasher <typeparamref name="TAltHasher" />, with no interface calls. Mirrors
    /// <see cref="PooledHashSet{T,TAlgo}.GetAlternateLookup{TAlternate,TAltHasher}" />; the view refers
    /// to this set the same way as <see cref="GetAlternateLookup{TAlternate}" />.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TAltHasher" /> must agree with the set's hasher: for any item it considers
    /// equal, it must return the same raw hash as the set's hasher (before the finalizer) and
    /// <see cref="IAlternateHasher{TAlternate,T}.Create" /> must return an equal item. The set cannot
    /// check this. If they disagree, lookups miss items that are present and <c>Add</c> can add
    /// duplicates.
    /// </remarks>
    /// <param name="hasher">The hasher instance; defaults to <c>default(TAltHasher)</c>.</param>
    [UnscopedRef]
    public AlternateLookup<TAlternate, TAltHasher> GetAlternateLookup<TAlternate, TAltHasher>(TAltHasher hasher = default)
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, T>
        => new(ref _algo, hasher);

    /// <summary>
    /// A view of a <see cref="StackOnlyPooledHashSet{T,TAlgo}" /> that looks up items by
    /// <typeparamref name="TAlternate" /> through the set's comparer. Get one from
    /// <see cref="GetAlternateLookup{TAlternate}" />. It refers to the set's table, so reads and
    /// writes go to the set itself, and it allocates nothing except the item that <see cref="Add" />
    /// creates.
    /// </summary>
    /// <typeparam name="TAlternate">The alternate item type; may be a <see langword="ref struct" />.</typeparam>
    public readonly ref struct AlternateLookup<TAlternate>
        where TAlternate : notnull, allows ref struct
    {
        readonly AlternateLookup<TAlternate, AlternateComparerHasher<TAlternate, T>> _lookup;

        internal AlternateLookup(ref TAlgo algo, IAlternateEqualityComparer<TAlternate, T> comparer)
            => _lookup = new(ref algo, new AlternateComparerHasher<TAlternate, T>(comparer));

        /// <summary>
        /// Adds the item that <paramref name="item" /> stands for unless it is already present. The
        /// comparer creates the <typeparamref name="T" /> only when it is added.
        /// </summary>
        /// <returns><see langword="true" /> if the item was added; <see langword="false" /> if it was already present.</returns>
        public bool Add(TAlternate item) => _lookup.Add(item);

        /// <summary>Returns <see langword="true" /> if the set contains an item equal to <paramref name="item" />.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(TAlternate item) => _lookup.Contains(item);

        /// <summary>Removes the item equal to <paramref name="item" />, if present.</summary>
        public bool Remove(TAlternate item) => _lookup.Remove(item);

        /// <summary>Gets the stored item equal to <paramref name="equalValue" />, if present.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TAlternate equalValue, [MaybeNullWhen(false)] out T actualValue)
            => _lookup.TryGetValue(equalValue, out actualValue);
    }

    /// <summary>
    /// A view of a <see cref="StackOnlyPooledHashSet{T,TAlgo}" /> that looks up items by
    /// <typeparamref name="TAlternate" /> through the struct hasher <typeparamref name="TAltHasher" />.
    /// Get one from <see cref="GetAlternateLookup{TAlternate,TAltHasher}" />. It refers to the set's
    /// table, so reads and writes go to the set itself, and it allocates nothing except the item that
    /// <see cref="Add" /> creates.
    /// </summary>
    /// <typeparam name="TAlternate">The alternate item type; may be a <see langword="ref struct" />.</typeparam>
    /// <typeparam name="TAltHasher">Struct hasher that agrees with the set's hasher.</typeparam>
    public readonly ref struct AlternateLookup<TAlternate, TAltHasher>
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, T>
    {
        // A ref field cannot point at the set (a ref struct, CS9050), so the view refers to the
        // algorithm struct inside it: that is where the buffers, count and hasher live.
        readonly ref TAlgo _algo;
        readonly TAltHasher _hasher;

        internal AlternateLookup(ref TAlgo algo, TAltHasher hasher)
        {
            _algo = ref algo;
            _hasher = hasher;
        }

        /// <summary>
        /// Adds the item that <paramref name="item" /> stands for unless it is already present, in a
        /// single probe. <see cref="IAlternateHasher{TAlternate,T}.Create" /> builds the
        /// <typeparamref name="T" /> only when it is added.
        /// </summary>
        /// <returns><see langword="true" /> if the item was added; <see langword="false" /> if it was already present.</returns>
        public bool Add(TAlternate item)
        {
            if (!_algo.IsAllocated)
            {
                EnsureInitialized(ref _algo);
            }
            return _algo.Insert<IdentitySlotKeyExtractor<T>, PooledHashSet<T, TAlgo>.AlternateInsertProbe<TAlternate, TAltHasher>>(
                new(item, _hasher), out _);
        }

        /// <summary>Returns <see langword="true" /> if the set contains an item equal to <paramref name="item" />.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(TAlternate item) =>
            !Unsafe.IsNullRef(in _algo.FindSlot<IdentitySlotKeyExtractor<T>, PooledHashSet<T, TAlgo>.AlternateProbe<TAlternate, TAltHasher>>(new(item, _hasher)));

        /// <summary>Removes the item equal to <paramref name="item" />, if present.</summary>
        public bool Remove(TAlternate item) =>
            _algo.Remove<IdentitySlotKeyExtractor<T>, PooledHashSet<T, TAlgo>.AlternateProbe<TAlternate, TAltHasher>>(
                new(item, _hasher), out _);

        /// <summary>Gets the stored item equal to <paramref name="equalValue" />, if present.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TAlternate equalValue, [MaybeNullWhen(false)] out T actualValue)
        {
            ref readonly var slot = ref _algo.FindSlot<IdentitySlotKeyExtractor<T>, PooledHashSet<T, TAlgo>.AlternateProbe<TAlternate, TAltHasher>>(new(equalValue, _hasher));
            if (Unsafe.IsNullRef(in slot))
            {
                actualValue = default;
                return false;
            }
            actualValue = slot;
            return true;
        }
    }
}
#endif
