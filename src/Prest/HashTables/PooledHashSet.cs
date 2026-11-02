using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#pragma warning disable IDE0044 // Readonly not possible — algorithm methods mutate _algo.

namespace Prest;

/// <summary>
/// Pooled flat hash set parameterized over a struct hashtable algorithm
/// (<see cref="IHashAlgorithm{TSlot,TKey}" />). Use the concrete alias
/// <c>PooledHashSet&lt;T&gt;</c> (SwissTable default) or one of the per-algorithm
/// aliases (<c>RobinHoodHashSet</c>, <c>LinearHashSet</c>, <c>ChainedHashSet</c>)
/// for ergonomic construction.
/// </summary>
/// <remarks>
/// <para>
/// The algorithm is a compile-time generic parameter — the JIT monomorphizes
/// each closed generic with hash, equality, probe, and enumeration logic fully
/// inlined. Zero interface-dispatch cost.
/// </para>
/// <para>
/// Implements <see cref="IReadOnlyCollection{T}" /> so it can be handed to APIs and LINQ.
/// <c>foreach</c> over the set still binds to the value-type <see cref="Enumerator" /> and
/// does not allocate; enumerating through the interface boxes it. Enumerators do not
/// detect modification: do not add or remove items while enumerating — a grow returns
/// the old buffers to the pool.
/// </para>
/// </remarks>
[DebuggerDisplay("Count = {Count}")]
public class PooledHashSet<T, TAlgo> : IReadOnlyCollection<T>, IDisposable
    where T : notnull
    where TAlgo : struct, IHashAlgorithm<T, T>
{
    [ThreadStatic]
    static PooledHashSet<T, TAlgo>? _threadCache;

    TAlgo _algo;

    /// <summary>
    /// Returns a set from the per-thread cache, or allocates a new one. Pair with
    /// <see cref="Dispose" /> (or <see cref="Return" />) — disposal returns the
    /// instance to the cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread and per concrete type: this generic type and each alias
    /// (<c>PooledHashSet&lt;T&gt;</c>, <c>SwissHashSet&lt;T&gt;</c>, …) have their own. A
    /// second <see cref="Create()" /> before the first is returned allocates fresh. For pooling
    /// that survives <c>await</c> boundaries, use <c>PooledHashSetPool</c> in
    /// <c>Prest.ObjectPool</c>.
    /// </remarks>
    public static PooledHashSet<T, TAlgo> Create()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached ?? new PooledHashSet<T, TAlgo>();
    }

    /// <summary>
    /// Returns a set with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static PooledHashSet<T, TAlgo> Create(int capacity)
    {
        var cached = _threadCache;
        if (cached is not null)
        {
            _threadCache = null;
            return cached;
        }
        return new PooledHashSet<T, TAlgo>(capacity);
    }

    /// <summary>
    /// Clears <paramref name="set" /> and places it in the per-thread cache of its own type —
    /// the one its type's <c>Create</c> reads from, so an alias instance goes back to that
    /// alias's cache. If the slot is already occupied, releases the set's rented buffers instead.
    /// </summary>
    public static void Return(PooledHashSet<T, TAlgo> set) => set.ReturnToCache();

    /// <summary>Puts this instance into its own type's per-thread cache. Aliases override it.</summary>
    private protected virtual void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    /// <summary>
    /// Clears <paramref name="set" /> into <paramref name="slot" /> when the slot is free and the
    /// set is exactly a <typeparamref name="TSelf" />; otherwise releases its rented buffers. The
    /// exact-type check keeps subclass instances (which that type's <c>Create</c> never hands
    /// out) out of the cache.
    /// </summary>
    private protected static void ReturnToSlot<TSelf>(ref TSelf? slot, TSelf set)
        where TSelf : PooledHashSet<T, TAlgo>
    {
        if (slot is null && set.GetType() == typeof(TSelf))
        {
            set._algo.Clear();
            slot = set;
        }
        else
        {
            set.ReleaseBuffers();
        }
    }

    /// <summary>Returns the rented buffers to the pool without caching the instance.</summary>
    private protected void ReleaseBuffers() => _algo.Dispose();

    protected PooledHashSet(int capacity = 0, TAlgo algorithm = default)
    {
        _algo = algorithm;
        if (capacity > 0)
        {
            _algo.Initialize(capacity);
        }
    }

    /// <summary>Number of items currently in the set.</summary>
    public int Count => _algo.Count;

    /// <summary>True when this set holds no items.</summary>
    public bool IsEmpty => _algo.Count == 0;

    /// <summary>Current target item count before the next rehash.</summary>
    public int Capacity => _algo.Capacity;

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
    public bool Contains(T item) => !Unsafe.IsNullRef(ref Unsafe.AsRef(in _algo.FindSlot<IdentitySlotKeyExtractor<T>>(item)));

    /// <summary>
    /// Looks up an entry equal to <paramref name="equalValue" />; if found, yields the
    /// stored instance via <paramref name="actualValue" />.
    /// </summary>
    public bool TryGetValue(T equalValue, out T actualValue)
    {
        ref readonly var slot = ref _algo.FindSlot<IdentitySlotKeyExtractor<T>>(equalValue);
        if (!Unsafe.IsNullRef(ref Unsafe.AsRef(in slot)))
        {
            actualValue = slot;
            return true;
        }
        actualValue = default!;
        return false;
    }

    /// <summary>
    /// Removes <paramref name="item" /> if present.
    /// </summary>
    public bool Remove(T item) => _algo.Remove<IdentitySlotKeyExtractor<T>>(item);

    /// <summary>Resets the set to empty state, keeping rented buffers.</summary>
    public void Clear() => _algo.Clear();

    /// <summary>Returns an enumerator over the items. Traversal order is arbitrary.</summary>
    public Enumerator GetEnumerator() => new(_algo);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Copies the items into a freshly-allocated array.</summary>
    public T[] ToArray()
    {
        if (_algo.Count == 0)
        {
            return [];
        }

        var result = new T[_algo.Count];
        var slots = _algo.SlotArray!;
        var scanLimit = _algo.SlotScanLimit;
        var idx = 0;
        for (var i = 0; i < scanLimit; i++)
        {
            if (_algo.IsSlotLive(i))
            {
                result[idx++] = slots[i];
            }
        }
        return result;
    }

    public virtual void Dispose()
    {
        ReturnToCache();
        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void EnsureInitialized() => _algo.Initialize(capacity: 8);

    /// <summary>Dense-scan enumerator over full slots. Holds a value-copy of the
    /// algorithm struct (cheap — the struct is ~40 bytes of array refs and ints)
    /// so MoveNext calls <c>_algo.IsSlotLive</c> directly, which the JIT
    /// monomorphizes per closed TAlgo. Implements <see cref="IEnumerator{T}" /> for interface
    /// consumers; it is only boxed when the set is enumerated through an interface.</summary>
    public struct Enumerator : IEnumerator<T>
    {
        TAlgo _algo;
        int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(TAlgo algo)
        {
            _algo = algo;
            _index = -1;
        }

        public readonly ref readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _algo.SlotArray![_index];
        }

        readonly T IEnumerator<T>.Current => Current;

        readonly object IEnumerator.Current => Current;

        void IEnumerator.Reset() => _index = -1;

        readonly void IDisposable.Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            var scanLimit = _algo.SlotScanLimit;
            while (++_index < scanLimit)
            {
                if (_algo.IsSlotLive(_index))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

