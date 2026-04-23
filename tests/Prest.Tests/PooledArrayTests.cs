namespace Prest.Tests;

public class PooledArrayTests
{
    [Test]
    public async Task CollectionExpression_PopulatesElements()
    {
        using PooledArray<int> array = [1, 2, 3];

        await Assert.That(array.Count).IsEqualTo(3);
        await Assert.That(array.Span.ToArray()).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task CollectionExpression_Empty_IsEmpty()
    {
        using PooledArray<int> array = [];

        await Assert.That(array.IsEmpty).IsTrue();
        await Assert.That(array.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Default_IsEmpty()
    {
        PooledArray<int> array = default;

        await Assert.That(array.IsEmpty).IsTrue();
        await Assert.That(array.Count).IsEqualTo(0);
        await Assert.That(array.Span.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Indexer_OutOfRange_Throws()
    {
        using PooledArray<int> array = [1, 2, 3];

        await Assert.That(() =>
        {
            _ = array[3];
        }).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Enumerator_IteratesAllElements()
    {
        using PooledArray<string> array = ["a", "b", "c"];

        var collected = new List<string>();
        foreach (var item in array)
        {
            collected.Add(item);
        }

        await Assert.That(collected).IsEquivalentTo(["a", "b", "c"]);
    }

    [Test]
    public async Task Dispose_IDisposableElements_AreDisposed()
    {
        var tracker1 = new DisposableTracker();
        var tracker2 = new DisposableTracker();

        using (PooledArray<DisposableTracker> array = [tracker1, tracker2]) { }

        await Assert.That(tracker1.Disposed).IsTrue();
        await Assert.That(tracker2.Disposed).IsTrue();
    }

    [Test]
    public async Task Empty_Property_IsEmpty()
    {
        var empty = PooledArray<int>.Empty;

        await Assert.That(empty.IsEmpty).IsTrue();
        await Assert.That(empty.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CreateFilled_PopulatesAllElements()
    {
        using var array = PooledArray.Create(5, 42);

        await Assert.That(array.Count).IsEqualTo(5);
        foreach (var x in array)
        {
            await Assert.That(x).IsEqualTo(42);
        }
    }

    [Test]
    public async Task CreateFilled_ZeroCount_ReturnsEmpty()
    {
        using var array = PooledArray.Create(0, 42);

        await Assert.That(array.IsEmpty).IsTrue();
    }

    [Test]
    public async Task ToArray_ReturnsIndependentCopy()
    {
        using PooledArray<int> pooled = [1, 2, 3];

        var arr = pooled.ToArray();

        await Assert.That(arr.Length).IsEqualTo(3);
        await Assert.That(arr).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task AsMemory_MatchesContent()
    {
        using PooledArray<int> pooled = [1, 2, 3];

        var mem = pooled.AsMemory();

        await Assert.That(mem.Length).IsEqualTo(3);
        await Assert.That(mem.Span.ToArray()).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task AsMemory_Default_ReturnsEmpty()
    {
        PooledArray<int> pooled = default;

        var mem = pooled.AsMemory();

        await Assert.That(mem.Length).IsEqualTo(0);
    }

    [Test]
    public async Task IndexOf_FindsElement()
    {
        using PooledArray<int> pooled = [10, 20, 30];

        await Assert.That(pooled.IndexOf(20)).IsEqualTo(1);
        await Assert.That(pooled.IndexOf(99)).IsEqualTo(-1);
    }

    [Test]
    public async Task Contains_Works()
    {
        using PooledArray<string> pooled = ["a", "b", "c"];

        await Assert.That(pooled.Contains("b")).IsTrue();
        await Assert.That(pooled.Contains("z")).IsFalse();
    }

    [Test]
    public async Task Sort_SortsInPlace()
    {
        using PooledArray<int> pooled = [3, 1, 4, 1, 5, 9, 2, 6];

        pooled.Sort();

        await Assert.That(pooled.Span.ToArray()).IsEquivalentTo([1, 1, 2, 3, 4, 5, 6, 9]);
    }

    [Test]
    public async Task Reverse_ReversesInPlace()
    {
        using PooledArray<int> pooled = [1, 2, 3, 4, 5];

        pooled.Reverse();

        await Assert.That(pooled.Span.ToArray()).IsEquivalentTo([5, 4, 3, 2, 1]);
    }

    [Test]
    public void Sort_EmptyOrSingle_DoesNotThrow()
    {
        PooledArray<int>.Empty.Sort();
        using PooledArray<int> single = [42];
        single.Sort();
    }

    sealed class DisposableTracker : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}
