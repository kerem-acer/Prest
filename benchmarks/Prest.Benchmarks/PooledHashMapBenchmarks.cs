using BenchmarkDotNet.Attributes;
using Faster.Map.Core;

namespace Prest.Benchmarks;

/// <summary>
/// Compares <c>PooledHashMap&lt;TKey,TValue,TAlgo&gt;</c> against the BCL
/// <see cref="Dictionary{TKey,TValue}" /> and Faster.Map's
/// <see cref="DenseMap{TKey,TValue}" /> across operations that dominate real
/// workloads: hit lookup, miss lookup, insert from empty, and churn.
/// </summary>
[MemoryDiagnoser]
public class PooledHashMapBenchmarks
{
    [Params(16, 256, 4096, 65536)]
    public int N;

    int[] _presentKeys = null!;
    int[] _missingKeys = null!;
    Dictionary<int, int> _dict = null!;
    PooledHashMap<int, int, SwissTableAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>> _pooled = null!;
    DenseMap<int, int> _dense = null!;

    [GlobalSetup]
    public void Setup()
    {
        _presentKeys = new int[N];
        _missingKeys = new int[N];
        for (var i = 0; i < N; i++)
        {
            _presentKeys[i] = i;
            _missingKeys[i] = N + i;
        }

        _dict = new Dictionary<int, int>(N);
        _pooled = PooledHashMap<int, int>.Create(N);
        _dense = new DenseMap<int, int>((uint)N);
        for (var i = 0; i < N; i++)
        {
            _dict.Add(i, i * 2);
            _pooled.Add(i, i * 2);
            _dense.Insert(i, i * 2);
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _pooled.Dispose();

    // -------- Lookup (hit) --------

    [Benchmark(Baseline = true, Description = "Dictionary.TryGetValue (hit)")]
    public int Dictionary_LookupHit()
    {
        var sum = 0;
        var dict = _dict;
        var keys = _presentKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            if (dict.TryGetValue(keys[i], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    [Benchmark(Description = "PooledHashMap.TryGetValue (hit)")]
    public int PooledHashMap_LookupHit()
    {
        var sum = 0;
        var map = _pooled;
        var keys = _presentKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            if (map.TryGetValue(keys[i], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    [Benchmark(Description = "DenseMap.Get (hit)")]
    public int DenseMap_LookupHit()
    {
        var sum = 0;
        var map = _dense;
        var keys = _presentKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            if (map.Get(keys[i], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    // -------- Lookup (miss) --------

    [Benchmark(Description = "Dictionary.TryGetValue (miss)")]
    public int Dictionary_LookupMiss()
    {
        var count = 0;
        var dict = _dict;
        var keys = _missingKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            if (!dict.TryGetValue(keys[i], out _))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "PooledHashMap.TryGetValue (miss)")]
    public int PooledHashMap_LookupMiss()
    {
        var count = 0;
        var map = _pooled;
        var keys = _missingKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            if (!map.TryGetValue(keys[i], out _))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "DenseMap.Get (miss)")]
    public int DenseMap_LookupMiss()
    {
        var count = 0;
        var map = _dense;
        var keys = _missingKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            if (!map.Get(keys[i], out _))
            {
                count++;
            }
        }
        return count;
    }

    // -------- ContainsKey --------

    [Benchmark(Description = "Dictionary.ContainsKey")]
    public int Dictionary_ContainsKey()
    {
        var count = 0;
        var dict = _dict;
        var keys = _presentKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            if (dict.ContainsKey(keys[i]))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "PooledHashMap.ContainsKey")]
    public int PooledHashMap_ContainsKey()
    {
        var count = 0;
        var map = _pooled;
        var keys = _presentKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            if (map.ContainsKey(keys[i]))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "DenseMap.Contains")]
    public int DenseMap_ContainsKey()
    {
        var count = 0;
        var map = _dense;
        var keys = _presentKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            if (map.Contains(keys[i]))
            {
                count++;
            }
        }
        return count;
    }

    // -------- Insert from empty --------

    [Benchmark(Description = "Dictionary.Add (from empty)")]
    public int Dictionary_Add()
    {
        var dict = new Dictionary<int, int>(N);
        var keys = _presentKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            dict.Add(keys[i], keys[i]);
        }
        return dict.Count;
    }

    [Benchmark(Description = "PooledHashMap.Add (from empty)")]
    public int PooledHashMap_Add()
    {
        using var map = PooledHashMap<int, int>.Create(N);
        var keys = _presentKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            map.Add(keys[i], keys[i]);
        }
        return map.Count;
    }

    [Benchmark(Description = "DenseMap.Insert (from empty)")]
    public int DenseMap_Add()
    {
        var map = new DenseMap<int, int>((uint)N);
        var keys = _presentKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            map.Insert(keys[i], keys[i]);
        }
        return map.Count;
    }

    // -------- Churn: repeatedly add + remove at stable size --------

    [Benchmark(Description = "Dictionary add/remove churn")]
    public int Dictionary_Churn()
    {
        var dict = new Dictionary<int, int>(N);
        var keys = _presentKeys;

        // Fill.
        for (var i = 0; i < keys.Length; i++)
        {
            dict.Add(keys[i], keys[i]);
        }

        // One full churn pass: remove each key then re-add with a different value.
        for (var i = 0; i < keys.Length; i++)
        {
            dict.Remove(keys[i]);
            dict.Add(keys[i], keys[i] + 1);
        }

        return dict.Count;
    }

    [Benchmark(Description = "PooledHashMap add/remove churn")]
    public int PooledHashMap_Churn()
    {
        using var map = PooledHashMap<int, int>.Create(N);
        var keys = _presentKeys;

        for (var i = 0; i < keys.Length; i++)
        {
            map.Add(keys[i], keys[i]);
        }

        for (var i = 0; i < keys.Length; i++)
        {
            map.Remove(keys[i]);
            map.Add(keys[i], keys[i] + 1);
        }

        return map.Count;
    }

    [Benchmark(Description = "DenseMap add/remove churn")]
    public int DenseMap_Churn()
    {
        var map = new DenseMap<int, int>((uint)N);
        var keys = _presentKeys;

        for (var i = 0; i < keys.Length; i++)
        {
            map.Insert(keys[i], keys[i]);
        }

        for (var i = 0; i < keys.Length; i++)
        {
            map.Remove(keys[i]);
            map.Insert(keys[i], keys[i] + 1);
        }

        return map.Count;
    }
}
