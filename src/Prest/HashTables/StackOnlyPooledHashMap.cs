using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#pragma warning disable IDE0044 // Readonly not possible — algorithm methods mutate _algo.

namespace Prest;

/// <summary>
/// Stack-only pooled hash map parameterized over a struct hashtable algorithm
/// (<see cref="IHashAlgorithm{TSlot,TKey}" />). The algorithm struct owns the rented buffers
/// and lives inline in this <see langword="ref struct" />, so the map itself never allocates:
/// no class instance and no per-thread cache. Compiler-enforced single-consumer: cannot be
/// stored in a field, boxed, or captured across <c>await</c>.
/// </summary>
/// <remarks>
/// <para>
/// Use <c>using var map = new StackOnlyPooledHashMap&lt;TKey, TValue, TAlgo&gt;(16);</c> so
/// <see cref="Dispose" /> returns the buffers to <see cref="ArrayPool{T}.Shared" />. <c>default</c>
/// and <c>new()</c> are empty maps that rent on the first add. For the default configuration
/// (SwissTable with an optional comparer, like <see cref="PooledHashMap{TKey,TValue}" />) use
/// <see cref="StackOnlyPooledHashMap{TKey,TValue}" />.
/// </para>
/// <para>
/// <b>Copy hazard:</b> assigning this struct to another local, or passing it by value, copies
/// the algorithm state. Both copies then share the same rented buffers with independent counts:
/// writes through one corrupt the other's table, a grow through one returns buffers the other
/// still uses, and disposing both returns the buffers to the pool twice. Keep a single local and
/// pass it to helpers only by <see langword="ref" />. Not by <see langword="in" /> either: a
/// mutating call on an <see langword="in" /> parameter runs on a hidden copy. A <c>using var</c>
/// local cannot be passed by <see langword="ref" /> (CS1657), so when a helper has to mutate the
/// map, declare it without <c>using</c> and call <see cref="Dispose" /> in a <c>finally</c> block.
/// </para>
/// <para>
/// An algorithm whose hasher holds state, such as <see cref="ComparerHasher{T}" />, must be
/// passed to <see cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}(int, TAlgo)" />; <c>default</c>
/// works only for stateless hashers.
/// </para>
/// <para>
/// <c>foreach</c> over the map, <see cref="Keys" />, or <see cref="Values" /> uses the same
/// value-type enumerators as <see cref="PooledHashMap{TKey,TValue,TAlgo}" />. Enumerators do not
/// detect modification: do not add or remove entries while enumerating — a grow returns the old
/// buffers to the pool. For a map that crosses <c>await</c>, lives in a field, or is handed out
/// through interfaces, use <see cref="PooledHashMap{TKey,TValue,TAlgo}" />.
/// </para>
/// </remarks>
[DebuggerDisplay("Count = {Count}")]
public ref struct StackOnlyPooledHashMap<TKey, TValue, TAlgo> : IDisposable
    where TKey : notnull
    where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
{
    TAlgo _algo;

    /// <summary>
    /// Creates a map with at least <paramref name="capacity" /> slots pre-allocated, using a
    /// <c>default</c> algorithm. A capacity of zero or less defers renting to the first add.
    /// </summary>
    public StackOnlyPooledHashMap(int capacity)
        : this(capacity, default)
    {
    }

    /// <summary>
    /// Creates a map that uses <paramref name="algorithm" /> (and the hasher and finalizer it was
    /// built with), with at least <paramref name="capacity" /> slots pre-allocated. A capacity of
    /// zero or less defers renting to the first add.
    /// </summary>
    public StackOnlyPooledHashMap(int capacity, TAlgo algorithm)
    {
        _algo = algorithm;
        if (capacity > 0)
        {
            _algo.Initialize(capacity);
        }
    }

    /// <summary>Number of entries in the map.</summary>
    public readonly int Count => _algo.Count;

    /// <summary>True when the map holds no entries.</summary>
    public readonly bool IsEmpty => _algo.Count == 0;

    /// <summary>
    /// Entries the map can hold before the next grow; zero while no buffers are rented (before the
    /// first add and after <see cref="Dispose" />).
    /// </summary>
    // The algorithms keep their last capacity after Dispose; report the buffers actually held.
    public readonly int Capacity => _algo.IsAllocated ? _algo.Capacity : 0;

    /// <summary>Value-type view over the keys.</summary>
    public readonly PooledHashMap<TKey, TValue, TAlgo>.KeyCollection Keys => new(_algo);

    /// <summary>Value-type view over the values.</summary>
    public readonly PooledHashMap<TKey, TValue, TAlgo>.ValueCollection Values => new(_algo);

    /// <summary>Gets the value stored for <paramref name="key" />.</summary>
    /// <exception cref="KeyNotFoundException">The key is not in the map.</exception>
    public TValue this[TKey key]
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

    /// <summary>Adds an entry.</summary>
    /// <exception cref="ArgumentException">An entry with the same key already exists.</exception>
    public void Add(TKey key, TValue value)
    {
        if (!TryAdd(key, value))
        {
            throw new ArgumentException("An entry with the same key already exists.", nameof(key));
        }
    }

    /// <summary>
    /// Adds an entry unless the key is already present. Returns <see langword="false" /> (and
    /// keeps the existing value) on a duplicate.
    /// </summary>
    public bool TryAdd(TKey key, TValue value)
    {
        if (!_algo.IsAllocated)
        {
            EnsureInitialized();
        }
        return _algo.Insert<PooledHashMap<TKey, TValue, TAlgo>.Extractor>(key, new KeyValueSlot<TKey, TValue>(key, value), out _);
    }

    /// <summary>Looks up the value stored for <paramref name="key" />.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        ref readonly var slot = ref _algo.FindSlot<PooledHashMap<TKey, TValue, TAlgo>.Extractor>(key);
        if (Unsafe.IsNullRef(ref Unsafe.AsRef(in slot)))
        {
            value = default;
            return false;
        }
        value = slot.Value;
        return true;
    }

    /// <summary>Returns <see langword="true" /> if the map contains <paramref name="key" />.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(TKey key) =>
        !Unsafe.IsNullRef(ref Unsafe.AsRef(in _algo.FindSlot<PooledHashMap<TKey, TValue, TAlgo>.Extractor>(key)));

    /// <summary>Removes the entry for <paramref name="key" /> if present.</summary>
    public bool Remove(TKey key) => _algo.Remove<PooledHashMap<TKey, TValue, TAlgo>.Extractor>(key);

    /// <summary>Removes every entry and keeps the rented buffers for reuse.</summary>
    public void Clear() => _algo.Clear();

    /// <summary>Returns an enumerator over the entries. Traversal order is arbitrary.</summary>
    public readonly PooledHashMap<TKey, TValue, TAlgo>.Enumerator GetEnumerator() => new(_algo);

    /// <summary>
    /// Disposes every key and value whose type implements <see cref="IDisposable" />, then returns
    /// the rented buffers to <see cref="ArrayPool{T}.Shared" />. The map is empty afterwards, and
    /// disposing again (or disposing <c>default</c>) does nothing.
    /// </summary>
    public void Dispose()
    {
        PooledHashMap<TKey, TValue, TAlgo>.DisposeEntries(ref _algo);
        _algo.Dispose();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void EnsureInitialized() => _algo.Initialize(capacity: 8);
}

/// <summary>
/// Default stack-only pooled hash map — SwissTable with <see cref="EqualityComparer{T}.Default" />
/// or a comparer you pass, the same configuration as <see cref="PooledHashMap{TKey,TValue}" />.
/// Compiler-enforced single-consumer: cannot be stored in a field, boxed, or captured across
/// <c>await</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>using var map = new StackOnlyPooledHashMap&lt;string, int&gt;(16);</c> — no class
/// allocation and no per-thread cache; <see cref="Dispose" /> returns the buffers to
/// <see cref="ArrayPool{T}.Shared" />. <c>default</c> and <c>new()</c> rent on the first add.
/// A thin wrapper over <see cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}" />; see that type
/// for the full contract.
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
public ref struct StackOnlyPooledHashMap<TKey, TValue> : IDisposable
    where TKey : notnull
{
    // The default configuration is spelled out here only. The enumerator and collection types
    // below are named through PooledHashMap<TKey, TValue>, so the two defaults cannot drift
    // apart without a compile error.
    StackOnlyPooledHashMap<TKey, TValue, SwissTableAlgorithm<KeyValueSlot<TKey, TValue>, TKey, OptionalComparerHasher<TKey>, NoOpHashFinalizer>> _map;

    // Null means EqualityComparer<TKey>.Default (OptionalComparerHasher.Normalize).
    readonly IEqualityComparer<TKey>? _comparer;

    /// <summary>
    /// Creates a map with at least <paramref name="capacity" /> slots pre-allocated. A capacity
    /// of zero or less defers renting to the first add.
    /// </summary>
    public StackOnlyPooledHashMap(int capacity) : this(capacity, null) { }

    /// <summary>
    /// Creates a map that uses <paramref name="comparer" /> for key equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />), with at least
    /// <paramref name="capacity" /> slots pre-allocated.
    /// </summary>
    public StackOnlyPooledHashMap(int capacity, IEqualityComparer<TKey>? comparer)
    {
        _comparer = OptionalComparerHasher<TKey>.Normalize(comparer);
        _map = new(capacity, new(new OptionalComparerHasher<TKey>(_comparer), default));
    }

    /// <summary>
    /// The comparer this map uses for key hashing and equality: the one passed to the constructor,
    /// or <see cref="EqualityComparer{T}.Default" />.
    /// </summary>
    public readonly IEqualityComparer<TKey> Comparer => _comparer ?? EqualityComparer<TKey>.Default;

    /// <inheritdoc cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}.Count" />
    public readonly int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _map.Count;
    }

    /// <inheritdoc cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}.IsEmpty" />
    public readonly bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _map.IsEmpty;
    }

    /// <inheritdoc cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}.Capacity" />
    public readonly int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _map.Capacity;
    }

    /// <inheritdoc cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}.Keys" />
    public readonly PooledHashMap<TKey, TValue>.KeyCollection Keys
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _map.Keys;
    }

    /// <inheritdoc cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}.Values" />
    public readonly PooledHashMap<TKey, TValue>.ValueCollection Values
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _map.Values;
    }

    /// <inheritdoc cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}.this[TKey]" />
    public TValue this[TKey key]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _map[key];
    }

    /// <inheritdoc cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}.Add" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TKey key, TValue value) => _map.Add(key, value);

    /// <inheritdoc cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}.TryAdd" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(TKey key, TValue value) => _map.TryAdd(key, value);

    /// <inheritdoc cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}.TryGetValue" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _map.TryGetValue(key, out value);

    /// <inheritdoc cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}.ContainsKey" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(TKey key) => _map.ContainsKey(key);

    /// <inheritdoc cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}.Remove" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(TKey key) => _map.Remove(key);

    /// <inheritdoc cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}.Clear" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _map.Clear();

    /// <inheritdoc cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}.GetEnumerator" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly PooledHashMap<TKey, TValue>.Enumerator GetEnumerator() => _map.GetEnumerator();

    /// <inheritdoc cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}.Dispose" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _map.Dispose();
}
