using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#pragma warning disable IDE0044 // Readonly not possible — algorithm methods mutate _algo.

namespace Prest;

/// <summary>
/// Pooled flat hash map parameterized over a struct hashtable algorithm
/// (<see cref="IHashAlgorithm{TSlot,TKey}" />). Use the concrete alias
/// <c>PooledHashMap&lt;TKey,TValue&gt;</c> (SwissTable default) or one of the
/// per-algorithm aliases (<c>RobinHoodHashMap</c>, <c>LinearHashMap</c>,
/// <c>ChainedHashMap</c>) for ergonomic construction.
/// </summary>
[DebuggerDisplay("Count = {Count}")]
public class PooledHashMap<TKey, TValue, TAlgo> : IDisposable
    where TKey : notnull
    where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
{
    readonly struct Extractor : ISlotKeyExtractor<KeyValueSlot<TKey, TValue>, TKey>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TKey Extract(in KeyValueSlot<TKey, TValue> slot) => slot.Key;
    }

    [ThreadStatic]
    static PooledHashMap<TKey, TValue, TAlgo>? _threadCache;

    TAlgo _algo;

    /// <summary>
    /// Returns a map from the per-thread cache, or allocates a new one. Pair with
    /// <see cref="Dispose" /> (or <see cref="Return" />) — disposal returns the
    /// instance to the cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache keyed per closed generic and per thread. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh. For pooling
    /// that survives <c>await</c> boundaries, use <c>PooledHashMapPool</c> in
    /// <c>Prest.ObjectPool</c>.
    /// </remarks>
    public static PooledHashMap<TKey, TValue, TAlgo> Create()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached ?? new PooledHashMap<TKey, TValue, TAlgo>();
    }

    /// <summary>
    /// Returns a map with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static PooledHashMap<TKey, TValue, TAlgo> Create(int capacity)
    {
        var cached = _threadCache;
        if (cached is not null)
        {
            _threadCache = null;
            return cached;
        }
        return new PooledHashMap<TKey, TValue, TAlgo>(capacity);
    }

    /// <summary>
    /// Creates a fresh map pre-populated from <paramref name="rentedKeys" /> and
    /// <paramref name="rentedValues" />. Bypasses the per-thread cache.
    /// </summary>
    public static PooledHashMap<TKey, TValue, TAlgo> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(rentedKeys, rentedValues, count);

    /// <summary>
    /// Clears <paramref name="map" /> and places it in the per-thread cache. If the
    /// slot is already occupied, releases the map's rented buffers instead.
    /// </summary>
    public static void Return(PooledHashMap<TKey, TValue, TAlgo> map)
    {
        if (_threadCache is null)
        {
            map._algo.Clear();
            _threadCache = map;
        }
        else
        {
            map._algo.Dispose();
        }
    }

    protected PooledHashMap(int capacity = 0, TAlgo algorithm = default)
    {
        _algo = algorithm;
        if (capacity > 0)
        {
            _algo.Initialize(capacity);
        }
    }

    protected PooledHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count, TAlgo algorithm = default)
    {
        _algo = algorithm;
        if (count <= 0)
        {
            ArrayPool<TKey>.Shared.Return(rentedKeys, clearArray: true);
            ArrayPool<TValue>.Shared.Return(rentedValues, clearArray: true);
            return;
        }

        try
        {
            _algo.Initialize(count);
            for (var i = 0; i < count; i++)
            {
                if (!_algo.Insert<Extractor>(rentedKeys[i], new KeyValueSlot<TKey, TValue>(rentedKeys[i], rentedValues[i]), out _))
                {
                    throw new ArgumentException(
                        "An entry with the same key already exists.", nameof(rentedKeys));
                }
            }
        }
        finally
        {
            ArrayPool<TKey>.Shared.Return(rentedKeys, clearArray: true);
            ArrayPool<TValue>.Shared.Return(rentedValues, clearArray: true);
        }
    }

    public int Count => _algo.Count;
    public bool IsEmpty => _algo.Count == 0;
    public int Capacity => _algo.Capacity;

    public KeyCollection Keys => new(_algo);
    public ValueCollection Values => new(_algo);

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

    public void Add(TKey key, TValue value)
    {
        if (!TryAdd(key, value))
        {
            throw new ArgumentException("An entry with the same key already exists.", nameof(key));
        }
    }

    public bool TryAdd(TKey key, TValue value)
    {
        if (!_algo.IsAllocated)
        {
            EnsureInitialized();
        }
        return _algo.Insert<Extractor>(key, new KeyValueSlot<TKey, TValue>(key, value), out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(TKey key, out TValue value)
    {
        ref readonly var slot = ref _algo.FindSlot<Extractor>(key);
        if (Unsafe.IsNullRef(ref Unsafe.AsRef(in slot)))
        {
            value = default!;
            return false;
        }
        value = slot.Value;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(TKey key) => !Unsafe.IsNullRef(ref Unsafe.AsRef(in _algo.FindSlot<Extractor>(key)));

    public bool Remove(TKey key) => _algo.Remove<Extractor>(key);

    public void Clear() => _algo.Clear();

    public Enumerator GetEnumerator() => new(_algo);

    public virtual void Dispose()
    {
        if (_algo.IsAllocated
            && (typeof(IDisposable).IsAssignableFrom(typeof(TKey))
                || typeof(IDisposable).IsAssignableFrom(typeof(TValue))))
        {
            var disposeKeys = typeof(IDisposable).IsAssignableFrom(typeof(TKey));
            var disposeValues = typeof(IDisposable).IsAssignableFrom(typeof(TValue));
            var slots = _algo.SlotArray!;
            var scanLimit = _algo.SlotScanLimit;
            for (var i = 0; i < scanLimit; i++)
            {
                if (_algo.IsSlotLive(i))
                {
                    if (disposeKeys)
                    {
                        (slots[i].Key as IDisposable)?.Dispose();
                    }
                    if (disposeValues)
                    {
                        (slots[i].Value as IDisposable)?.Dispose();
                    }
                }
            }
        }
        Return(this);
        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void EnsureInitialized() => _algo.Initialize(capacity: 8);

    public struct Enumerator
    {
        TAlgo _algo;
        int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(TAlgo algo)
        {
            _algo = algo;
            _index = -1;
        }

        public readonly KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ref readonly var slot = ref _algo.SlotArray![_index];
                return new KeyValuePair<TKey, TValue>(slot.Key, slot.Value);
            }
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

    public readonly struct KeyCollection
    {
        readonly TAlgo _algo;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyCollection(TAlgo algo) => _algo = algo;

        public int Count => _algo.Count;

        public KeyEnumerator GetEnumerator() => new(_algo);
    }

    public readonly struct ValueCollection
    {
        readonly TAlgo _algo;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueCollection(TAlgo algo) => _algo = algo;

        public int Count => _algo.Count;

        public ValueEnumerator GetEnumerator() => new(_algo);
    }

    public struct KeyEnumerator
    {
        TAlgo _algo;
        int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyEnumerator(TAlgo algo)
        {
            _algo = algo;
            _index = -1;
        }

        public readonly ref readonly TKey Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _algo.SlotArray![_index].Key;
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

    public struct ValueEnumerator
    {
        TAlgo _algo;
        int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueEnumerator(TAlgo algo)
        {
            _algo = algo;
            _index = -1;
        }

        public readonly ref readonly TValue Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _algo.SlotArray![_index].Value;
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

