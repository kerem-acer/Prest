using BenchmarkDotNet.Attributes;
using Faster.Map.Core;

namespace Prest.Benchmarks;

/// <summary>
/// String-keyed counterpart to <see cref="PooledHashMapBenchmarks" />. String keys
/// exercise the comparer/hash paths the BCL is most tuned for, so this is the
/// scenario where Prest must prove it's competitive on real-world workloads.
/// </summary>
[MemoryDiagnoser]
public class PooledHashMapStringBenchmarks
{
    [Params(256, 4096, 65536)]
    public int N;

    string[] _presentKeys = null!;
    string[] _missingKeys = null!;
    Dictionary<string, int> _dict = null!;
    PooledHashMap<string, int, SwissTableAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>> _pooled = null!;
    DenseMap<string, int> _dense = null!;

    [GlobalSetup]
    public void Setup()
    {
        _presentKeys = new string[N];
        _missingKeys = new string[N];
        for (var i = 0; i < N; i++)
        {
            _presentKeys[i] = $"key-{i}";
            _missingKeys[i] = $"absent-{i}";
        }

        _dict = new Dictionary<string, int>(N);
        _pooled = PooledHashMap<string, int>.Create(N);
        _dense = new DenseMap<string, int>((uint)N);
        for (var i = 0; i < N; i++)
        {
            _dict.Add(_presentKeys[i], i);
            _pooled.Add(_presentKeys[i], i);
            _dense.Insert(_presentKeys[i], i);
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _pooled.Dispose();

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

    [Benchmark(Description = "Dictionary.Add (from empty)")]
    public int Dictionary_Add()
    {
        var dict = new Dictionary<string, int>(N);
        var keys = _presentKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            dict.Add(keys[i], i);
        }
        return dict.Count;
    }

    [Benchmark(Description = "PooledHashMap.Add (from empty)")]
    public int PooledHashMap_Add()
    {
        using var map = PooledHashMap<string, int>.Create(N);
        var keys = _presentKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            map.Add(keys[i], i);
        }
        return map.Count;
    }

    [Benchmark(Description = "DenseMap.Insert (from empty)")]
    public int DenseMap_Add()
    {
        var map = new DenseMap<string, int>((uint)N);
        var keys = _presentKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            map.Insert(keys[i], i);
        }
        return map.Count;
    }
}
