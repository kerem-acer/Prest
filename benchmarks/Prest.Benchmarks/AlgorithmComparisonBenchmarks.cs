using BenchmarkDotNet.Attributes;

namespace Prest.Benchmarks;

/// <summary>
/// Isolates the four <see cref="IHashAlgorithm{TSlot,TKey}" /> implementations from
/// the rest of the stack by running the same steady-state lookup against each.
/// </summary>
[MemoryDiagnoser]
public class AlgorithmComparisonBenchmarks
{
    [Params(256, 4096, 65536)]
    public int N;

    int[] _keys = null!;
    Dictionary<int, int> _dict = null!;
    PooledHashMap<int, int, SwissTableAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>> _swiss = null!;
    PooledHashMap<int, int, LinearProbingAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>> _linear = null!;
    PooledHashMap<int, int, RobinHoodAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>> _robin = null!;
    PooledHashMap<int, int, ChainedAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>> _chained = null!;

    [GlobalSetup]
    public void Setup()
    {
        _keys = new int[N];
        for (var i = 0; i < N; i++)
        {
            _keys[i] = i;
        }

        _dict = new Dictionary<int, int>(N);
        _swiss = PooledHashMap<int, int>.Create(N);
        _linear = LinearHashMap<int, int>.Create(N);
        _robin = RobinHoodHashMap<int, int>.Create(N);
        _chained = ChainedHashMap<int, int>.Create(N);

        for (var i = 0; i < N; i++)
        {
            _dict.Add(i, i * 2);
            _swiss.Add(i, i * 2);
            _linear.Add(i, i * 2);
            _robin.Add(i, i * 2);
            _chained.Add(i, i * 2);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _swiss.Dispose();
        _linear.Dispose();
        _robin.Dispose();
        _chained.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int Dictionary_Lookup()
    {
        var sum = 0;
        var keys = _keys;
        var d = _dict;
        for (var i = 0; i < keys.Length; i++)
        {
            if (d.TryGetValue(keys[i], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    [Benchmark]
    public int Swiss_Lookup()
    {
        var sum = 0;
        var keys = _keys;
        var m = _swiss;
        for (var i = 0; i < keys.Length; i++)
        {
            if (m.TryGetValue(keys[i], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    [Benchmark]
    public int Linear_Lookup()
    {
        var sum = 0;
        var keys = _keys;
        var m = _linear;
        for (var i = 0; i < keys.Length; i++)
        {
            if (m.TryGetValue(keys[i], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    [Benchmark]
    public int RobinHood_Lookup()
    {
        var sum = 0;
        var keys = _keys;
        var m = _robin;
        for (var i = 0; i < keys.Length; i++)
        {
            if (m.TryGetValue(keys[i], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    [Benchmark]
    public int Chained_Lookup()
    {
        var sum = 0;
        var keys = _keys;
        var m = _chained;
        for (var i = 0; i < keys.Length; i++)
        {
            if (m.TryGetValue(keys[i], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }
}
