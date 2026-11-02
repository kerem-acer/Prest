using TUnit.Assertions.Enums;

namespace Prest.Tests;

public partial class PooledListTests
{
    [Test]
    public async Task AsSpan_WriteThroughSpan_VisibleThroughIndexerAndSpan()
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        const int Third = 3;
        const int Written = 99;
        using var list = new PooledList<int>(4);
        list.AddRange([First, Second, Third]);

        // Act
        list.AsSpan()[1] = Written;

        // Assert
        await Assert.That(list[1]).IsEqualTo(Written);
        await Assert.That(list.Span.ToArray()).IsEquivalentTo([First, Written, Third], CollectionOrdering.Matching);
    }

    [Test]
    public async Task AsSpan_SpareCapacity_LengthEqualsCount()
    {
        // Arrange
        const int InitialCapacity = 16;
        const int Count = 3;
        using var list = new PooledList<int>(InitialCapacity);
        for (var i = 0; i < Count; i++)
        {
            list.Add(i);
        }

        // Act
        var length = list.AsSpan().Length;

        // Assert
        await Assert.That(list.Capacity).IsGreaterThan(Count);
        await Assert.That(length).IsEqualTo(Count);
    }

    [Test]
    [Arguments(0)]
    [Arguments(8)]
    public async Task AsSpan_EmptyList_ReturnsEmptySpan(int initialCapacity)
    {
        // Arrange
        using var list = new PooledList<int>(initialCapacity);

        // Act
        var isEmpty = list.AsSpan().IsEmpty;

        // Assert
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task AsSpan_AfterClear_ReturnsEmptySpan()
    {
        // Arrange
        using var list = new PooledList<int>(4);
        list.AddRange([1, 2, 3]);
        list.Clear();

        // Act
        var isEmpty = list.AsSpan().IsEmpty;

        // Assert
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task AsSpan_AfterDispose_ReturnsEmptySpan()
    {
        // Arrange
        var list = new PooledList<int>(4);
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
        using var list = new PooledList<int>(4);
        list.AddRange([Mid, Low, High]);

        // Act
        list.AsSpan().Sort((a, b) => b.CompareTo(a));

        // Assert
        await Assert.That(list.Span.ToArray()).IsEquivalentTo([High, Mid, Low], CollectionOrdering.Matching);
    }
}
