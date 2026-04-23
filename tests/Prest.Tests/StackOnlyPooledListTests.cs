namespace Prest.Tests;

public class StackOnlyPooledListTests
{
    [Test]
    public async Task Add_SingleItem_CountIsOne()
    {
        var list = new StackOnlyPooledList<int>(4);
        list.Add(42);
        var count = list.Count;
        list.Dispose();

        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Add_MultipleItems_SpanContainsAll()
    {
        var list = new StackOnlyPooledList<int>(4);
        list.Add(1);
        list.Add(2);
        list.Add(3);
        var count = list.Count;
        var items = list.Span.ToArray();
        list.Dispose();

        await Assert.That(count).IsEqualTo(3);
        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task Add_BeyondCapacity_GrowsCorrectly()
    {
        var list = new StackOnlyPooledList<int>(4);
        for (var i = 0; i < 20; i++)
        {
            list.Add(i);
        }

        var count = list.Count;
        var items = list.Span.ToArray();
        list.Dispose();

        await Assert.That(count).IsEqualTo(20);
        var expected = Enumerable.Range(0, 20).ToArray();
        await Assert.That(items).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Indexer_ValidIndex_ReturnsCorrectValue()
    {
        var list = new StackOnlyPooledList<int>(4);
        list.Add(10);
        list.Add(20);
        list.Add(30);

        var v0 = list[0];
        var v1 = list[1];
        var v2 = list[2];
        list.Dispose();

        await Assert.That(v0).IsEqualTo(10);
        await Assert.That(v1).IsEqualTo(20);
        await Assert.That(v2).IsEqualTo(30);
    }

    [Test]
    public async Task Indexer_RefReturn_AllowsMutation()
    {
        var list = new StackOnlyPooledList<int>(4);
        list.Add(1);

        list[0] = 99;
        var value = list[0];
        list.Dispose();

        await Assert.That(value).IsEqualTo(99);
    }

    [Test]
    public void Dispose_DoesNotThrow()
    {
        var list = new StackOnlyPooledList<int>(4);
        list.Add(1);

        list.Dispose();
    }

    [Test]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var list = new StackOnlyPooledList<int>(4);
        list.Add(1);

        list.Dispose();
        list.Dispose();
    }

    [Test]
    public async Task Dispose_ResetsCount()
    {
        var list = new StackOnlyPooledList<string>(4, clearOnReturn: true);
        list.Add("hello");
        list.Add("world");

        list.Dispose();
        var count = list.Count;

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task AsSpan_Empty_ReturnsEmptySpan()
    {
        var list = new StackOnlyPooledList<int>(4);
        var length = list.Span.Length;
        list.Dispose();

        await Assert.That(length).IsEqualTo(0);
    }

    [Test]
    public async Task StackBufferCtor_AvoidsPoolUntilOverflow()
    {
        int[] snapshot;
        Span<int> stack = stackalloc int[4];
        var list = new StackOnlyPooledList<int>(stack);
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);
        list.Add(5); // forces pool rent

        snapshot = list.Span.ToArray();
        list.Dispose();

        await Assert.That(snapshot).IsEquivalentTo([1, 2, 3, 4, 5]);
    }

    [Test]
    public async Task ToPooledArray_HandsOwnershipAndEmptiesList()
    {
        var list = new StackOnlyPooledList<int>(4);
        list.Add(10);
        list.Add(20);

        var array = list.ToPooledArray();
        var listCount = list.Count;
        list.Dispose();

        await Assert.That(array.Count).IsEqualTo(2);
        await Assert.That(array[0]).IsEqualTo(10);
        await Assert.That(array[1]).IsEqualTo(20);
        await Assert.That(listCount).IsEqualTo(0);

        array.Dispose();
    }

    [Test]
    public async Task GetEnumerator_Foreach_IteratesAllElements()
    {
        var list = new StackOnlyPooledList<int>(4);
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
    public async Task GetEnumerator_RefCurrent_AllowsMutation()
    {
        var list = new StackOnlyPooledList<int>(4);
        list.Add(1);
        list.Add(2);
        list.Add(3);

        foreach (ref var x in list)
        {
            x *= 10;
        }

        var snapshot = list.Span.ToArray();
        list.Dispose();

        await Assert.That(snapshot).IsEquivalentTo([10, 20, 30]);
    }

    [Test]
    public async Task Capacity_ReflectsSpanLength()
    {
        var list = new StackOnlyPooledList<int>(16);
        var cap = list.Capacity;
        list.Dispose();

        await Assert.That(cap).IsGreaterThanOrEqualTo(16);
    }

    [Test]
    public async Task ToArray_ReturnsIndependentCopy()
    {
        var list = new StackOnlyPooledList<int>(4);
        list.Add(1);
        list.Add(2);
        var arr = list.ToArray();
        list.Dispose();

        await Assert.That(arr).IsEquivalentTo([1, 2]);
    }

    [Test]
    public async Task IndexOf_FindsElement()
    {
        var list = new StackOnlyPooledList<string>(4);
        list.Add("a");
        list.Add("b");
        list.Add("c");
        var foundB = list.IndexOf("b");
        var missing = list.IndexOf("z");
        list.Dispose();

        await Assert.That(foundB).IsEqualTo(1);
        await Assert.That(missing).IsEqualTo(-1);
    }

    [Test]
    public async Task Contains_Works()
    {
        var list = new StackOnlyPooledList<int>(4);
        list.Add(42);
        var found = list.Contains(42);
        var missing = list.Contains(99);
        list.Dispose();

        await Assert.That(found).IsTrue();
        await Assert.That(missing).IsFalse();
    }

    [Test]
    public async Task Sort_SortsInPlace()
    {
        var list = new StackOnlyPooledList<int>(8);
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
        var list = new StackOnlyPooledList<int>(4);
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
        var list = new StackOnlyPooledList<int>(16);
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
        var list = new StackOnlyPooledList<int>(2);
        list.Add(1);
        list.AddRange([2, 3, 4, 5]);
        var snapshot = list.Span.ToArray();
        list.Dispose();

        await Assert.That(snapshot).IsEquivalentTo([1, 2, 3, 4, 5]);
    }

    [Test]
    public async Task Indexer_OutOfRange_ThrowsAgainstCount()
    {
        var list = new StackOnlyPooledList<int>(16);
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

    [Test]
    public async Task StackAlloc_Sort_Works()
    {
        int[] snapshot;
        {
            Span<int> stack = stackalloc int[8];
            var list = new StackOnlyPooledList<int>(stack);
            list.Add(3);
            list.Add(1);
            list.Add(2);
            list.Sort();
            snapshot = list.Span.ToArray();
            list.Dispose();
        }

        await Assert.That(snapshot).IsEquivalentTo([1, 2, 3]);
    }
}
