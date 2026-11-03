using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#pragma warning disable IDE0044 // Readonly not possible — algorithm methods mutate _algo.

namespace Prest;

/// <summary>
/// Stack-only pooled hash set parameterized over a struct hashtable algorithm
/// (<see cref="IHashAlgorithm{TSlot,TKey}" />). The algorithm struct owns the rented buffers
/// and lives inline in this <see langword="ref struct" />, so the set itself never allocates:
/// no class instance and no per-thread cache. Compiler-enforced single-consumer: cannot be
/// stored in a field, boxed, or captured across <c>await</c>.
/// </summary>
/// <remarks>
/// <para>
/// Use <c>using var set = new StackOnlyPooledHashSet&lt;T, TAlgo&gt;(16);</c> so
/// <see cref="Dispose" /> returns the buffers to <see cref="ArrayPool{T}.Shared" />. <c>default</c>
/// and <c>new()</c> are empty sets that rent on the first add. For the default configuration
/// (SwissTable, <see cref="EqualityDefaultHasher{T}" />, <see cref="NoOpHashFinalizer" />) use
/// <see cref="StackOnlyPooledHashSet{T}" />.
/// </para>
/// <para>
/// <b>Copy hazard:</b> assigning this struct to another local, or passing it by value, copies
/// the algorithm state. Both copies then share the same rented buffers with independent counts:
/// writes through one corrupt the other's table, a grow through one returns buffers the other
/// still uses, and disposing both returns the buffers to the pool twice. Keep a single local and
/// pass it to helpers only by <see langword="ref" />. Not by <see langword="in" /> either: a
/// mutating call on an <see langword="in" /> parameter runs on a hidden copy. A <c>using var</c>
/// local cannot be passed by <see langword="ref" /> (CS1657), so when a helper has to mutate the
/// set, declare it without <c>using</c> and call <see cref="Dispose" /> in a <c>finally</c> block.
/// </para>
/// <para>
/// An algorithm whose hasher holds state, such as <see cref="ComparerHasher{T}" />, must be
/// passed to <see cref="StackOnlyPooledHashSet{T,TAlgo}(int, TAlgo)" />; <c>default</c> works
/// only for stateless hashers.
/// </para>
/// <para>
/// <c>foreach</c> uses the same value-type enumerator as <see cref="PooledHashSet{T,TAlgo}" />.
/// Enumerators do not detect modification: do not add or remove items while enumerating — a grow
/// returns the old buffers to the pool. For a set that crosses <c>await</c>, lives in a field, or
/// is handed out through interfaces, use <see cref="PooledHashSet{T,TAlgo}" />.
/// </para>
/// </remarks>
[DebuggerDisplay("Count = {Count}")]
public ref struct StackOnlyPooledHashSet<T, TAlgo> : IDisposable
    where T : notnull
    where TAlgo : struct, IHashAlgorithm<T, T>
{
    TAlgo _algo;

    /// <summary>
    /// Creates a set with at least <paramref name="capacity" /> slots pre-allocated, using a
    /// <c>default</c> algorithm. A capacity of zero or less defers renting to the first add.
    /// </summary>
    public StackOnlyPooledHashSet(int capacity)
        : this(capacity, default)
    {
    }

    /// <summary>
    /// Creates a set that uses <paramref name="algorithm" /> (and the hasher and finalizer it was
    /// built with), with at least <paramref name="capacity" /> slots pre-allocated. A capacity of
    /// zero or less defers renting to the first add.
    /// </summary>
    public StackOnlyPooledHashSet(int capacity, TAlgo algorithm)
    {
        _algo = algorithm;
        if (capacity > 0)
        {
            _algo.Initialize(capacity);
        }
    }

    /// <summary>Number of items in the set.</summary>
    public readonly int Count => _algo.Count;

    /// <summary>True when the set holds no items.</summary>
    public readonly bool IsEmpty => _algo.Count == 0;

    /// <summary>
    /// Items the set can hold before the next grow; zero while no buffers are rented (before the
    /// first add and after <see cref="Dispose" />).
    /// </summary>
    // The algorithms keep their last capacity after Dispose; report the buffers actually held.
    public readonly int Capacity => _algo.IsAllocated ? _algo.Capacity : 0;

    /// <summary>
    /// Adds <paramref name="item" /> if not already present. Returns <see langword="true" />
    /// if inserted, <see langword="false" /> if a duplicate.
    /// </summary>
    public bool Add(T item)
    {
        if (!_algo.IsAllocated)
        {
            EnsureInitialized();
        }
        return _algo.Insert<IdentitySlotKeyExtractor<T>>(item, item, out _);
    }

    /// <summary>Returns <see langword="true" /> if the set contains <paramref name="item" />.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item) =>
        !Unsafe.IsNullRef(ref Unsafe.AsRef(in _algo.FindSlot<IdentitySlotKeyExtractor<T>>(item)));

    /// <summary>
    /// Looks up an item equal to <paramref name="equalValue" />; if found, yields the stored
    /// instance via <paramref name="actualValue" />.
    /// </summary>
    public bool TryGetValue(T equalValue, [MaybeNullWhen(false)] out T actualValue)
    {
        ref readonly var slot = ref _algo.FindSlot<IdentitySlotKeyExtractor<T>>(equalValue);
        if (Unsafe.IsNullRef(ref Unsafe.AsRef(in slot)))
        {
            actualValue = default;
            return false;
        }
        actualValue = slot;
        return true;
    }

    /// <summary>Removes <paramref name="item" /> if present.</summary>
    public bool Remove(T item) => _algo.Remove<IdentitySlotKeyExtractor<T>>(item);

    /// <summary>Removes every item and keeps the rented buffers for reuse.</summary>
    public void Clear() => _algo.Clear();

    /// <summary>Returns an enumerator over the items. Traversal order is arbitrary.</summary>
    public readonly PooledHashSet<T, TAlgo>.Enumerator GetEnumerator() => new(_algo);

    /// <summary>Copies the items into a freshly-allocated array.</summary>
    public readonly T[] ToArray()
    {
        if (_algo.Count == 0)
        {
            return [];
        }

        var result = new T[_algo.Count];
        var index = 0;
        foreach (var item in this)
        {
            result[index++] = item;
        }
        return result;
    }

    /// <summary>
    /// Returns the rented buffers to <see cref="ArrayPool{T}.Shared" />. Items are not disposed,
    /// matching <see cref="PooledHashSet{T,TAlgo}" />. The set is empty afterwards, and disposing
    /// again (or disposing <c>default</c>) does nothing.
    /// </summary>
    public void Dispose() => _algo.Dispose();

    [MethodImpl(MethodImplOptions.NoInlining)]
    void EnsureInitialized() => _algo.Initialize(capacity: 8);
}

/// <summary>
/// Default stack-only pooled hash set — SwissTable + <see cref="EqualityDefaultHasher{T}" /> +
/// <see cref="NoOpHashFinalizer" />, the same configuration as <see cref="PooledHashSet{T}" />.
/// Compiler-enforced single-consumer: cannot be stored in a field, boxed, or captured across
/// <c>await</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>using var set = new StackOnlyPooledHashSet&lt;string&gt;(16);</c> — no class allocation and
/// no per-thread cache; <see cref="Dispose" /> returns the buffers to
/// <see cref="ArrayPool{T}.Shared" />. <c>default</c> and <c>new()</c> rent on the first add. A thin
/// wrapper over <see cref="StackOnlyPooledHashSet{T,TAlgo}" />; see that type for the full contract.
/// </para>
/// <para>
/// <b>Copy hazard:</b> assigning this struct to another local, or passing it by value, copies
/// the table state. Both copies then share the same rented buffers with independent counts,
/// which corrupts the table and returns the buffers twice when both are disposed. Keep a single
/// local and pass it to helpers only by <see langword="ref" /> (not <see langword="in" />). A
/// <c>using var</c> local cannot be passed by <see langword="ref" />; declare it without
/// <c>using</c> and dispose it in a <c>finally</c> block instead.
/// </para>
/// </remarks>
[DebuggerDisplay("Count = {Count}")]
public ref struct StackOnlyPooledHashSet<T> : IDisposable
    where T : notnull
{
    // The default configuration is spelled out here only. The enumerator type below is named
    // through PooledHashSet<T>, so the two defaults cannot drift apart without a compile error.
    StackOnlyPooledHashSet<T, SwissTableAlgorithm<T, T, EqualityDefaultHasher<T>, NoOpHashFinalizer>> _set;

    /// <summary>
    /// Creates a set with at least <paramref name="capacity" /> slots pre-allocated. A capacity
    /// of zero or less defers renting to the first add.
    /// </summary>
    public StackOnlyPooledHashSet(int capacity) => _set = new(capacity);

    /// <inheritdoc cref="StackOnlyPooledHashSet{T,TAlgo}.Count" />
    public readonly int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _set.Count;
    }

    /// <inheritdoc cref="StackOnlyPooledHashSet{T,TAlgo}.IsEmpty" />
    public readonly bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _set.IsEmpty;
    }

    /// <inheritdoc cref="StackOnlyPooledHashSet{T,TAlgo}.Capacity" />
    public readonly int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _set.Capacity;
    }

    /// <inheritdoc cref="StackOnlyPooledHashSet{T,TAlgo}.Add" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Add(T item) => _set.Add(item);

    /// <inheritdoc cref="StackOnlyPooledHashSet{T,TAlgo}.Contains" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item) => _set.Contains(item);

    /// <inheritdoc cref="StackOnlyPooledHashSet{T,TAlgo}.TryGetValue" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(T equalValue, [MaybeNullWhen(false)] out T actualValue) =>
        _set.TryGetValue(equalValue, out actualValue);

    /// <inheritdoc cref="StackOnlyPooledHashSet{T,TAlgo}.Remove" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(T item) => _set.Remove(item);

    /// <inheritdoc cref="StackOnlyPooledHashSet{T,TAlgo}.Clear" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _set.Clear();

    /// <inheritdoc cref="StackOnlyPooledHashSet{T,TAlgo}.GetEnumerator" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly PooledHashSet<T>.Enumerator GetEnumerator() => _set.GetEnumerator();

    /// <inheritdoc cref="StackOnlyPooledHashSet{T,TAlgo}.ToArray" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T[] ToArray() => _set.ToArray();

    /// <inheritdoc cref="StackOnlyPooledHashSet{T,TAlgo}.Dispose" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _set.Dispose();
}
