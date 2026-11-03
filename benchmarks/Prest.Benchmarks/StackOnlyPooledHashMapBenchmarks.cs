using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Prest.Benchmarks;

/// <summary>
/// A short-lived, method-local map: index <c>N</c> keys (about half of them duplicates) with
/// <c>TryAdd</c> and return the distinct count. Compares a fresh <see cref="Dictionary{TKey,TValue}" />,
/// <see cref="PooledHashMap{TKey,TValue}" /> from its per-thread cache, and
/// <see cref="StackOnlyPooledHashMap{TKey,TValue}" />. <c>Single</c> has one map alive at a time, so the
/// per-thread cache always hits; <c>Nested</c> has two alive at once, so the second
/// <see cref="PooledHashMap{TKey,TValue}" /> misses the single-slot cache and allocates.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class StackOnlyPooledHashMapBenchmarks
{
    [Params(16, 256, 4096)]
    public int N;

    int[] _keys = null!;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        _keys = new int[N];
        for (var i = 0; i < N; i++)
        {
            _keys[i] = random.Next(N / 2);
        }
    }

    [Benchmark(Baseline = true, Description = "Dictionary<K,V>")]
    [BenchmarkCategory("Single")]
    public int Dictionary_Single()
    {
        var map = new Dictionary<int, int>(N);
        foreach (var key in _keys)
        {
            map.TryAdd(key, key);
        }
        return map.Count;
    }

    [Benchmark(Description = "PooledHashMap<K,V>.Create")]
    [BenchmarkCategory("Single")]
    public int PooledHashMap_Single()
    {
        using var map = PooledHashMap<int, int>.Create(N);
        foreach (var key in _keys)
        {
            map.TryAdd(key, key);
        }
        return map.Count;
    }

    [Benchmark(Description = "StackOnlyPooledHashMap<K,V>")]
    [BenchmarkCategory("Single")]
    public int StackOnlyPooledHashMap_Single()
    {
        using var map = new StackOnlyPooledHashMap<int, int>(N);
        foreach (var key in _keys)
        {
            map.TryAdd(key, key);
        }
        return map.Count;
    }

    [Benchmark(Baseline = true, Description = "Dictionary<K,V>")]
    [BenchmarkCategory("Nested")]
    public int Dictionary_Nested()
    {
        var even = new Dictionary<int, int>(N);
        var odd = new Dictionary<int, int>(N);
        foreach (var key in _keys)
        {
            (key % 2 == 0 ? even : odd).TryAdd(key, key);
        }
        return even.Count + odd.Count;
    }

    [Benchmark(Description = "PooledHashMap<K,V>.Create")]
    [BenchmarkCategory("Nested")]
    public int PooledHashMap_Nested()
    {
        using var even = PooledHashMap<int, int>.Create(N);
        using var odd = PooledHashMap<int, int>.Create(N);
        foreach (var key in _keys)
        {
            (key % 2 == 0 ? even : odd).TryAdd(key, key);
        }
        return even.Count + odd.Count;
    }

    [Benchmark(Description = "StackOnlyPooledHashMap<K,V>")]
    [BenchmarkCategory("Nested")]
    public int StackOnlyPooledHashMap_Nested()
    {
        using var even = new StackOnlyPooledHashMap<int, int>(N);
        using var odd = new StackOnlyPooledHashMap<int, int>(N);
        foreach (var key in _keys)
        {
            if (key % 2 == 0)
            {
                even.TryAdd(key, key);
            }
            else
            {
                odd.TryAdd(key, key);
            }
        }
        return even.Count + odd.Count;
    }
}
