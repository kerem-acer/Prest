#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Prest;

public partial class PooledHashSet<T, TAlgo>
{
    /// <summary>
    /// Returns a view of this set that looks up items by <typeparamref name="TAlternate" /> instead of
    /// <typeparamref name="T" />, for example a <c>ReadOnlySpan&lt;char&gt;</c> for
    /// <see cref="string" /> items, so a lookup does not have to create an item. Mirrors
    /// <see cref="HashSet{T}.GetAlternateLookup{TAlternate}" />.
    /// </summary>
    /// <remarks>
    /// Works when the set's comparer implements
    /// <see cref="IAlternateEqualityComparer{TAlternate,T}" /> for <typeparamref name="TAlternate" />.
    /// The default hasher uses <see cref="EqualityComparer{T}.Default" />, which does for
    /// <c>ReadOnlySpan&lt;char&gt;</c> on <see cref="string" /> items, as do the built-in
    /// <see cref="StringComparer" />s. Each hash and equality check is one interface call to the
    /// comparer, like <see cref="HashSet{T}" />. For lookups with no interface calls, use
    /// <see cref="GetAlternateLookup{TAlternate,TAltHasher}" />.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The set's comparer does not implement <see cref="IAlternateEqualityComparer{TAlternate,T}" />
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
    /// Gets a view of this set that looks up items by <typeparamref name="TAlternate" />, if the set's
    /// comparer supports it. See <see cref="GetAlternateLookup{TAlternate}" />.
    /// </summary>
    /// <returns>
    /// <see langword="false" /> if the set's comparer does not implement
    /// <see cref="IAlternateEqualityComparer{TAlternate,T}" /> for <typeparamref name="TAlternate" />.
    /// </returns>
    public bool TryGetAlternateLookup<TAlternate>(out AlternateLookup<TAlternate> lookup)
        where TAlternate : notnull, allows ref struct
    {
        if (_algo.Comparer is IAlternateEqualityComparer<TAlternate, T> comparer)
        {
            lookup = new AlternateLookup<TAlternate>(this, comparer);
            return true;
        }
        lookup = default;
        return false;
    }

    /// <summary>
    /// Returns a view of this set that looks up items by <typeparamref name="TAlternate" /> using the
    /// struct hasher <typeparamref name="TAltHasher" />. The JIT specializes the lookup for the hasher,
    /// so there are no interface calls, and it works whatever the set's hasher is, as long as the two
    /// agree.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TAltHasher" /> must agree with the set's hasher: for any item it considers
    /// equal, it must return the same raw hash as the set's hasher (before the finalizer) and
    /// <see cref="IAlternateHasher{TAlternate,T}.Create" /> must return an equal item. The set cannot
    /// check this. If they disagree, lookups miss items that are present and <c>Add</c> can add
    /// duplicates.
    /// </remarks>
    /// <param name="hasher">The hasher instance; defaults to <c>default(TAltHasher)</c>.</param>
    public AlternateLookup<TAlternate, TAltHasher> GetAlternateLookup<TAlternate, TAltHasher>(TAltHasher hasher = default)
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, T>
        => new(this, hasher);

    /// <summary>Shared with <see cref="StackOnlyPooledHashSet{T,TAlgo}" />.</summary>
    [DoesNotReturn]
    internal static void ThrowIncompatibleComparer(Type alternateType) =>
        throw new InvalidOperationException(
            $"The set's comparer does not implement IAlternateEqualityComparer<{alternateType.Name}, {typeof(T).Name}>. " +
            "Use a comparer that does, or pass an IAlternateHasher to GetAlternateLookup<TAlternate, TAltHasher>().");

    /// <summary>
    /// A view of a <see cref="PooledHashSet{T,TAlgo}" /> that looks up items by
    /// <typeparamref name="TAlternate" /> through the set's comparer. Get one from
    /// <see cref="GetAlternateLookup{TAlternate}" />. It reads and writes the set itself, holds no
    /// state of its own, and allocates nothing except the item that <see cref="Add" /> creates.
    /// </summary>
    /// <typeparam name="TAlternate">The alternate item type; may be a <see langword="ref struct" />.</typeparam>
    public readonly struct AlternateLookup<TAlternate>
        where TAlternate : notnull, allows ref struct
    {
        readonly AlternateLookup<TAlternate, AlternateComparerHasher<TAlternate, T>> _lookup;

        internal AlternateLookup(PooledHashSet<T, TAlgo> set, IAlternateEqualityComparer<TAlternate, T> comparer)
            => _lookup = new(set, new AlternateComparerHasher<TAlternate, T>(comparer));

        /// <summary>The set this lookup reads and writes.</summary>
        public PooledHashSet<T, TAlgo> Set => _lookup.Set;

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
    /// A view of a <see cref="PooledHashSet{T,TAlgo}" /> that looks up items by
    /// <typeparamref name="TAlternate" /> through the struct hasher <typeparamref name="TAltHasher" />.
    /// Get one from <see cref="GetAlternateLookup{TAlternate,TAltHasher}" />. It reads and writes the
    /// set itself and allocates nothing except the item that <see cref="Add" /> creates.
    /// </summary>
    /// <typeparam name="TAlternate">The alternate item type; may be a <see langword="ref struct" />.</typeparam>
    /// <typeparam name="TAltHasher">Struct hasher that agrees with the set's hasher.</typeparam>
    public readonly struct AlternateLookup<TAlternate, TAltHasher>
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, T>
    {
        readonly PooledHashSet<T, TAlgo> _set;
        readonly TAltHasher _hasher;

        internal AlternateLookup(PooledHashSet<T, TAlgo> set, TAltHasher hasher)
        {
            _set = set;
            _hasher = hasher;
        }

        /// <summary>The set this lookup reads and writes.</summary>
        public PooledHashSet<T, TAlgo> Set => _set;

        /// <summary>
        /// Adds the item that <paramref name="item" /> stands for unless it is already present, in a
        /// single probe. <see cref="IAlternateHasher{TAlternate,T}.Create" /> builds the
        /// <typeparamref name="T" /> only when it is added.
        /// </summary>
        /// <returns><see langword="true" /> if the item was added; <see langword="false" /> if it was already present.</returns>
        public bool Add(TAlternate item)
        {
            var set = _set;
            if (!set._algo.IsAllocated)
            {
                set.EnsureInitialized();
            }
            return set._algo.Insert<IdentitySlotKeyExtractor<T>, AlternateInsertProbe<TAlternate, TAltHasher>>(
                new(item, _hasher), out _);
        }

        /// <summary>Returns <see langword="true" /> if the set contains an item equal to <paramref name="item" />.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(TAlternate item) =>
            !Unsafe.IsNullRef(in _set._algo.FindSlot<IdentitySlotKeyExtractor<T>, AlternateProbe<TAlternate, TAltHasher>>(new(item, _hasher)));

        /// <summary>Removes the item equal to <paramref name="item" />, if present.</summary>
        public bool Remove(TAlternate item) =>
            _set._algo.Remove<IdentitySlotKeyExtractor<T>, AlternateProbe<TAlternate, TAltHasher>>(
                new(item, _hasher), out _);

        /// <summary>Gets the stored item equal to <paramref name="equalValue" />, if present.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TAlternate equalValue, [MaybeNullWhen(false)] out T actualValue)
        {
            ref readonly var slot = ref _set._algo.FindSlot<IdentitySlotKeyExtractor<T>, AlternateProbe<TAlternate, TAltHasher>>(new(equalValue, _hasher));
            if (Unsafe.IsNullRef(in slot))
            {
                actualValue = default;
                return false;
            }
            actualValue = slot;
            return true;
        }
    }

    /// <summary>Probe for a lookup by alternate item. Shared with <see cref="StackOnlyPooledHashSet{T,TAlgo}" />.</summary>
    internal readonly ref struct AlternateProbe<TAlternate, TAltHasher> : IHashProbe<T>
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, T>
    {
        readonly TAlternate _item;
        readonly TAltHasher _hasher;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AlternateProbe(TAlternate item, TAltHasher hasher)
        {
            _item = item;
            _hasher = hasher;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ComputeHash() => Unsafe.AsRef(in _hasher).ComputeHash(_item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(in T slot) => Unsafe.AsRef(in _hasher).Equals(_item, slot);
    }

    /// <summary>Probe for an add by alternate item. Shared with <see cref="StackOnlyPooledHashSet{T,TAlgo}" />.</summary>
    internal readonly ref struct AlternateInsertProbe<TAlternate, TAltHasher> : IInsertProbe<T>
        where TAlternate : notnull, allows ref struct
        where TAltHasher : struct, IAlternateHasher<TAlternate, T>
    {
        readonly TAlternate _item;
        readonly TAltHasher _hasher;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AlternateInsertProbe(TAlternate item, TAltHasher hasher)
        {
            _item = item;
            _hasher = hasher;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ComputeHash() => Unsafe.AsRef(in _hasher).ComputeHash(_item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(in T slot) => Unsafe.AsRef(in _hasher).Equals(_item, slot);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T CreateSlot() => Unsafe.AsRef(in _hasher).Create(_item);
    }
}
#endif
