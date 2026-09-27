using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Prest.Benchmarks;

/// <summary>
/// Compares <see cref="PooledStack{T}" /> and <see cref="StackOnlyPooledStack{T}" /> against
/// <see cref="Stack{T}" />: push <c>N</c> items then pop them all.
/// <c>Fresh</c> constructs a new stack per call (the allocation story — a method-local
/// traversal stack); <c>Reused</c> keeps one pre-sized stack alive (raw push/pop throughput).
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class PooledStackBenchmarks
{
    const int InlineCapacity = 64;

    [Params(16, 256, 4096)]
    public int N;

    Stack<int> _stack = null!;
    PooledStack<int> _pooled = null!;

    [GlobalSetup]
    public void Setup()
    {
        _stack = new Stack<int>(N);
        _pooled = new PooledStack<int>(N);
    }

    [GlobalCleanup]
    public void Cleanup() => _pooled.Dispose();

    [Benchmark(Baseline = true, Description = "Stack<T>")]
    [BenchmarkCategory("Fresh")]
    public int Stack_PushPopFresh()
    {
        var stack = new Stack<int>();
        for (var i = 0; i < N; i++)
        {
            stack.Push(i);
        }

        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            sum += stack.Pop();
        }
        return sum;
    }

    [Benchmark(Description = "PooledStack<T>")]
    [BenchmarkCategory("Fresh")]
    public int PooledStack_PushPopFresh()
    {
        using var stack = new PooledStack<int>();
        for (var i = 0; i < N; i++)
        {
            stack.Push(i);
        }

        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            sum += stack.Pop();
        }
        return sum;
    }

    [Benchmark(Description = "StackOnlyPooledStack<T> (64 inline)")]
    [BenchmarkCategory("Fresh")]
    public int StackOnlyPooledStack_PushPopFresh()
    {
        using var stack = new StackOnlyPooledStack<int>(stackalloc int[InlineCapacity]);
        for (var i = 0; i < N; i++)
        {
            stack.Push(i);
        }

        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            sum += stack.Pop();
        }
        return sum;
    }

    [Benchmark(Baseline = true, Description = "Stack<T>")]
    [BenchmarkCategory("Reused")]
    public int Stack_PushPopReused()
    {
        var stack = _stack;
        for (var i = 0; i < N; i++)
        {
            stack.Push(i);
        }

        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            sum += stack.Pop();
        }
        return sum;
    }

    [Benchmark(Description = "PooledStack<T>")]
    [BenchmarkCategory("Reused")]
    public int PooledStack_PushPopReused()
    {
        var stack = _pooled;
        for (var i = 0; i < N; i++)
        {
            stack.Push(i);
        }

        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            sum += stack.Pop();
        }
        return sum;
    }
}
