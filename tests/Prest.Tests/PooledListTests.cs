namespace Prest.Tests;

public class PooledListTests
{
    [Test]
    public async Task Add_Grows_ContainsAll()
    {
        using var list = new PooledList<int>(4);
        for (var i = 0; i < 20; i++)
        {
            list.Add(i);
        }

        await Assert.That(list.Count).IsEqualTo(20);
        await Assert.That(list.Span.ToArray()).IsEquivalentTo(Enumerable.Range(0, 20).ToArray());
    }

    [Test]
    public async Task Indexer_MutatesElement()
    {
        using var list = new PooledList<int>(4);
        list.Add(1);
        list[0] = 99;

        await Assert.That(list[0]).IsEqualTo(99);
    }

    [Test]
    public async Task Indexer_OutOfRange_Throws()
    {
        using var list = new PooledList<int>(4);
        list.Add(1);

        await Assert.That(() => list[5]).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ToPooledArray_HandsOwnership_EmptiesList()
    {
        var list = new PooledList<int>(4);
        list.Add(10);
        list.Add(20);

        using var array = list.ToPooledArray();
        var listCount = list.Count;
        list.Dispose();

        await Assert.That(array.Count).IsEqualTo(2);
        await Assert.That(listCount).IsEqualTo(0);
    }

    [Test]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var list = new PooledList<int>(4);
        list.Add(1);
        list.Dispose();
        list.Dispose();
    }

    [Test]
    public async Task SurvivesAcrossAwait()
    {
        using var list = new PooledList<int>(4);
        list.Add(1);

        await Task.Yield();

        list.Add(2);
        await Assert.That(list.Span.ToArray()).IsEquivalentTo([1, 2]);
    }

    [Test]
    public async Task GetEnumerator_Foreach_IteratesAllElements()
    {
        using var list = new PooledList<int>(4);
        list.Add(10);
        list.Add(20);
        list.Add(30);

        var collected = new List<int>();
        foreach (var x in list)
        {
            collected.Add(x);
        }

        await Assert.That(collected).IsEquivalentTo(new List<int> { 10, 20, 30 });
    }

    [Test]
    public async Task GetEnumerator_RefCurrent_AllowsMutation()
    {
        using var list = new PooledList<int>(4);
        list.Add(1);
        list.Add(2);
        list.Add(3);

        foreach (ref var x in list)
        {
            x *= 10;
        }

        await Assert.That(list.Span.ToArray()).IsEquivalentTo([10, 20, 30]);
    }

    [Test]
    public async Task Capacity_ReflectsRentedLength()
    {
        using var list = new PooledList<int>(16);

        await Assert.That(list.Capacity).IsGreaterThanOrEqualTo(16);
    }

    [Test]
    public async Task ToArray_ReturnsIndependentCopy()
    {
        using var list = new PooledList<int>(4);
        list.Add(1);
        list.Add(2);

        var arr = list.ToArray();

        await Assert.That(arr).IsEquivalentTo([1, 2]);
    }

    [Test]
    public async Task AsMemory_MatchesContent()
    {
        using var list = new PooledList<int>(4);
        list.Add(7);
        list.Add(8);

        var mem = list.AsMemory();

        await Assert.That(mem.Length).IsEqualTo(2);
        await Assert.That(mem.Span.ToArray()).IsEquivalentTo([7, 8]);
    }

    [Test]
    public async Task IndexOf_FindsElement()
    {
        using var list = new PooledList<string>(4);
        list.Add("a");
        list.Add("b");
        list.Add("c");

        await Assert.That(list.IndexOf("b")).IsEqualTo(1);
        await Assert.That(list.IndexOf("missing")).IsEqualTo(-1);
    }

    [Test]
    public async Task Contains_Works()
    {
        using var list = new PooledList<int>(4);
        list.Add(42);

        await Assert.That(list.Contains(42)).IsTrue();
        await Assert.That(list.Contains(99)).IsFalse();
    }

    [Test]
    public async Task Sort_SortsInPlace()
    {
        using var list = new PooledList<int>(8);
        list.Add(3);
        list.Add(1);
        list.Add(2);

        list.Sort();

        await Assert.That(list.Span.ToArray()).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task Reverse_ReversesInPlace()
    {
        using var list = new PooledList<int>(4);
        list.Add(1);
        list.Add(2);
        list.Add(3);

        list.Reverse();

        await Assert.That(list.Span.ToArray()).IsEquivalentTo([3, 2, 1]);
    }

    [Test]
    public async Task Clear_ResetsCount_KeepsBuffer()
    {
        using var list = new PooledList<int>(16);
        list.Add(1);
        list.Add(2);
        var capBefore = list.Capacity;

        list.Clear();

        await Assert.That(list.Count).IsEqualTo(0);
        await Assert.That(list.Capacity).IsEqualTo(capBefore);
    }

    [Test]
    public async Task Clear_ThenAdd_Works()
    {
        using var list = new PooledList<int>(4);
        list.Add(1);
        list.Clear();
        list.Add(99);

        await Assert.That(list.Count).IsEqualTo(1);
        await Assert.That(list[0]).IsEqualTo(99);
    }

    [Test]
    public async Task AddRange_AppendsAll()
    {
        using var list = new PooledList<int>(2);
        list.Add(1);
        list.AddRange([2, 3, 4, 5]);

        await Assert.That(list.Count).IsEqualTo(5);
        await Assert.That(list.Span.ToArray()).IsEquivalentTo([1, 2, 3, 4, 5]);
    }

    [Test]
    public async Task AddRange_Empty_NoOp()
    {
        using var list = new PooledList<int>(4);
        list.Add(1);
        list.AddRange(ReadOnlySpan<int>.Empty);

        await Assert.That(list.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Identity_Equality()
    {
        var a = new PooledList<int>(4);
        var b = new PooledList<int>(4);
        var aAlias = a;

        await Assert.That(a.Equals(aAlias)).IsTrue();
        await Assert.That(a.Equals(b)).IsFalse();
        await Assert.That(a == aAlias).IsTrue();
        await Assert.That(a == b).IsFalse();
        await Assert.That(a.GetHashCode()).IsEqualTo(aAlias.GetHashCode());

        a.Dispose();
        b.Dispose();
    }
}
