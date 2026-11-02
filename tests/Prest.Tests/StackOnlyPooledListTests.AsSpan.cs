using TUnit.Assertions.Enums;

namespace Prest.Tests;

public partial class StackOnlyPooledListTests
{
    [Test]
    public async Task AsSpan_WriteThroughSpan_VisibleThroughIndexerAndSpan()
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        const int Third = 3;
        const int Written = 99;
        var list = new StackOnlyPooledList<int>(4);
        list.AddRange([First, Second, Third]);

        // Act
        list.AsSpan()[1] = Written;
        var indexed = list[1];
        var items = list.Span.ToArray();
        list.Dispose();

        // Assert
        await Assert.That(indexed).IsEqualTo(Written);
        await Assert.That(items).IsEquivalentTo([First, Written, Third], CollectionOrdering.Matching);
    }

    [Test]
    public async Task AsSpan_InlineBuffer_WritesReachInlineBuffer()
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        const int Written = 99;
        Span<int> inline = stackalloc int[4];
        var list = new StackOnlyPooledList<int>(inline);
        list.Add(First);
        list.Add(Second);

        // Act
        list.AsSpan()[0] = Written;
        var indexed = list[0];
        var inlineValue = inline[0];
        var items = list.Span.ToArray();
        list.Dispose();

        // Assert
        await Assert.That(indexed).IsEqualTo(Written);
        await Assert.That(inlineValue).IsEqualTo(Written);
        await Assert.That(items).IsEquivalentTo([Written, Second], CollectionOrdering.Matching);
    }

    [Test]
    public async Task AsSpan_AfterSpillToPool_WritesReachRentedArrayNotInlineBuffer()
    {
        // Arrange
        const int InlineCapacity = 2;
        const int Count = 3;
        const int First = 10;
        const int Second = 20;
        const int Third = 30;
        const int Written = 99;
        Span<int> inline = stackalloc int[InlineCapacity];
        var list = new StackOnlyPooledList<int>(inline);
        list.Add(First);
        list.Add(Second);
        list.Add(Third); // overflows the inline buffer and moves to a rented array

        // Act
        var span = list.AsSpan();
        span[0] = Written;
        var length = span.Length;
        var indexed = list[0];
        var inlineValue = inline[0];
        list.Dispose();

        // Assert
        await Assert.That(length).IsEqualTo(Count);
        await Assert.That(indexed).IsEqualTo(Written);
        await Assert.That(inlineValue).IsEqualTo(First);
    }

    [Test]
    public async Task AsSpan_SpareCapacity_LengthEqualsCount()
    {
        // Arrange
        const int InitialCapacity = 16;
        const int Count = 3;
        var list = new StackOnlyPooledList<int>(InitialCapacity);
        for (var i = 0; i < Count; i++)
        {
            list.Add(i);
        }

        // Act
        var length = list.AsSpan().Length;
        var capacity = list.Capacity;
        list.Dispose();

        // Assert
        await Assert.That(capacity).IsGreaterThan(Count);
        await Assert.That(length).IsEqualTo(Count);
    }

    [Test]
    [Arguments(0)]
    [Arguments(8)]
    public async Task AsSpan_EmptyList_ReturnsEmptySpan(int initialCapacity)
    {
        // Arrange
        var list = new StackOnlyPooledList<int>(initialCapacity);

        // Act
        var isEmpty = list.AsSpan().IsEmpty;
        list.Dispose();

        // Assert
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task AsSpan_AfterClear_ReturnsEmptySpan()
    {
        // Arrange
        var list = new StackOnlyPooledList<int>(4);
        list.AddRange([1, 2, 3]);
        list.Clear();

        // Act
        var isEmpty = list.AsSpan().IsEmpty;
        list.Dispose();

        // Assert
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task AsSpan_AfterDispose_ReturnsEmptySpan()
    {
        // Arrange
        var list = new StackOnlyPooledList<int>(4);
        list.AddRange([1, 2, 3]);
        list.Dispose();

        // Act
        var isEmpty = list.AsSpan().IsEmpty;

        // Assert
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task AsSpan_SortWithComparison_SortsListInPlace()
    {
        // Arrange
        const int Low = 1;
        const int Mid = 2;
        const int High = 3;
        Span<int> inline = stackalloc int[4];
        var list = new StackOnlyPooledList<int>(inline);
        list.AddRange([Mid, Low, High]);

        // Act
        list.AsSpan().Sort((a, b) => b.CompareTo(a));
        var items = list.Span.ToArray();
        list.Dispose();

        // Assert
        await Assert.That(items).IsEquivalentTo([High, Mid, Low], CollectionOrdering.Matching);
    }
}
