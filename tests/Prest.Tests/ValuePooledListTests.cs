namespace Prest.Tests;

public class ValuePooledListTests
{
    [Test]
    public async Task Add_Grows_ContainsAll()
    {
        var list = new ValuePooledList<int>(4);
        for (var i = 0; i < 20; i++)
        {
            list.Add(i);
        }

        var snapshot = list.Span.ToArray();
        list.Dispose();

        await Assert.That(snapshot).IsEquivalentTo(Enumerable.Range(0, 20).ToArray());
    }

    [Test]
    public async Task CopyAliasing_Documented()
    {
        // Copies of a struct share the rented backing array. Writes through the
        // copy are visible through the original. Caller must ensure single
        // ownership for disposal.
        var original = new ValuePooledList<int>(4);
        original.Add(1);
        original.Add(2);

        var copy = original;
        copy[0] = 99;

        var originalValue = original[0];
        original.Dispose();

        await Assert.That(originalValue).IsEqualTo(99);
    }

    [Test]
    public async Task ToPooledArray_HandsOwnership()
    {
        var list = new ValuePooledList<string>(4);
        list.Add("x");
        list.Add("y");

        using var array = list.ToPooledArray();

        await Assert.That(array.Count).IsEqualTo(2);
        await Assert.That(array[0]).IsEqualTo("x");
    }

    [Test]
    public async Task GetEnumerator_Foreach_IteratesAllElements()
    {
        var list = new ValuePooledList<int>(4);
        list.Add(10);
        list.Add(20);
        list.Add(30);

        var collected = new List<int>();
        foreach (var x in list)
        {
            collected.Add(x);
        }
        list.Dispose();

        await Assert.That(collected).IsEquivalentTo(new List<int> { 10, 20, 30 });
    }

    [Test]
    public async Task Capacity_ReflectsRentedLength()
    {
        var list = new ValuePooledList<int>(16);
        var cap = list.Capacity;
        list.Dispose();

        await Assert.That(cap).IsGreaterThanOrEqualTo(16);
    }

    [Test]
    public async Task ToArray_ReturnsIndependentCopy()
    {
        var list = new ValuePooledList<int>(4);
        list.Add(1);
        list.Add(2);
        var arr = list.ToArray();
        list.Dispose();

        await Assert.That(arr).IsEquivalentTo([1, 2]);
    }

    [Test]
    public async Task AsMemory_MatchesContent()
    {
        var list = new ValuePooledList<int>(4);
        list.Add(7);
        list.Add(8);
        var len = list.AsMemory().Length;
        var snapshot = list.AsMemory().Span.ToArray();
        list.Dispose();

        await Assert.That(len).IsEqualTo(2);
        await Assert.That(snapshot).IsEquivalentTo([7, 8]);
    }

    [Test]
    public async Task IndexOf_And_Contains_Work()
    {
        var list = new ValuePooledList<string>(4);
        list.Add("a");
        list.Add("b");
        var ix = list.IndexOf("b");
        var hasA = list.Contains("a");
        var hasZ = list.Contains("z");
        list.Dispose();

        await Assert.That(ix).IsEqualTo(1);
        await Assert.That(hasA).IsTrue();
        await Assert.That(hasZ).IsFalse();
    }

    [Test]
    public async Task Sort_SortsInPlace()
    {
        var list = new ValuePooledList<int>(8);
        list.Add(3);
        list.Add(1);
        list.Add(2);
        list.Sort();
        var snapshot = list.Span.ToArray();
        list.Dispose();

        await Assert.That(snapshot).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task Reverse_ReversesInPlace()
    {
        var list = new ValuePooledList<int>(4);
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Reverse();
        var snapshot = list.Span.ToArray();
        list.Dispose();

        await Assert.That(snapshot).IsEquivalentTo([3, 2, 1]);
    }

    [Test]
    public async Task Clear_ResetsCount_KeepsBuffer()
    {
        var list = new ValuePooledList<int>(16);
        list.Add(1);
        list.Add(2);
        var capBefore = list.Capacity;
        list.Clear();
        var count = list.Count;
        var capAfter = list.Capacity;
        list.Dispose();

        await Assert.That(count).IsEqualTo(0);
        await Assert.That(capAfter).IsEqualTo(capBefore);
    }

    [Test]
    public async Task AddRange_AppendsAll()
    {
        var list = new ValuePooledList<int>(2);
        list.Add(1);
        list.AddRange([2, 3, 4, 5]);
        var snapshot = list.Span.ToArray();
        list.Dispose();

        await Assert.That(snapshot).IsEquivalentTo([1, 2, 3, 4, 5]);
    }

    [Test]
    public async Task Indexer_OutOfRange_ThrowsAgainstCount()
    {
        var list = new ValuePooledList<int>(16);
        list.Add(1);

        Exception? caught = null;
        try
        {
            _ = list[5];
        }
        catch (ArgumentOutOfRangeException ex)
        {
            caught = ex;
        }
        list.Dispose();

        await Assert.That(caught).IsNotNull();
    }
}
