using BenchmarkDotNet.Attributes;

namespace Prest.Benchmarks;

/// <summary>
/// Measures the cost of each hash finalizer on sequential int keys — the workload
/// most likely to expose clustering that a finalizer would fix (or regress).
/// </summary>
[MemoryDiagnoser]
public class FinalizerComparisonBenchmarks
{
    const int N = 65536;

    int[] _keys = null!;
    PooledHashMap<int, int, SwissTableAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>> _swissIdentity = null!;
    PooledHashMap<int, int, SwissTableAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, Lowbias32Finalizer>> _swissLowbias = null!;
    PooledHashMap<int, int, SwissTableAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, FibonacciFinalizer>> _swissFib = null!;
    PooledHashMap<int, int, SwissTableAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, XmxFinalizer>> _swissXmx = null!;

    PooledHashMap<int, int, RobinHoodAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>> _robinIdentity = null!;
    PooledHashMap<int, int, RobinHoodAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, FibonacciFinalizer>> _robinFib = null!;

    [GlobalSetup]
    public void Setup()
    {
        _keys = new int[N];
        for (var i = 0; i < N; i++)
        {
            _keys[i] = i;
        }

        _swissIdentity = PooledHashMap<int, int>.Create(N);
        _swissLowbias = SwissHashMap<int, int, Lowbias32Finalizer>.Create(N);
        _swissFib = SwissHashMap<int, int, FibonacciFinalizer>.Create(N);
        _swissXmx = SwissHashMap<int, int, XmxFinalizer>.Create(N);
        _robinIdentity = RobinHoodHashMap<int, int>.Create(N);
        _robinFib = RobinHoodHashMap<int, int, FibonacciFinalizer>.Create(N);

        for (var i = 0; i < N; i++)
        {
            _swissIdentity.Add(i, i);
            _swissLowbias.Add(i, i);
            _swissFib.Add(i, i);
            _swissXmx.Add(i, i);
            _robinIdentity.Add(i, i);
            _robinFib.Add(i, i);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _swissIdentity.Dispose();
        _swissLowbias.Dispose();
        _swissFib.Dispose();
        _swissXmx.Dispose();
        _robinIdentity.Dispose();
        _robinFib.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int Swiss_Identity()
    {
        var sum = 0;
        var keys = _keys;
        var m = _swissIdentity;
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
    public int Swiss_Lowbias32()
    {
        var sum = 0;
        var keys = _keys;
        var m = _swissLowbias;
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
    public int Swiss_Fibonacci()
    {
        var sum = 0;
        var keys = _keys;
        var m = _swissFib;
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
    public int Swiss_Xmx()
    {
        var sum = 0;
        var keys = _keys;
        var m = _swissXmx;
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
    public int RobinHood_Identity()
    {
        var sum = 0;
        var keys = _keys;
        var m = _robinIdentity;
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
    public int RobinHood_Fibonacci()
    {
        var sum = 0;
        var keys = _keys;
        var m = _robinFib;
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
