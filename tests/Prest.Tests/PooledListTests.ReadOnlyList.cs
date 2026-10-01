using System.Collections;
using System.Runtime.CompilerServices;
using TUnit.Assertions.Enums;

namespace Prest.Tests;

public partial class PooledListTests
{
    [Test]
    public async Task ReadOnlyListIndexer_ValidIndex_ReturnsElement()
    {
        // Arrange
        const int First = 10;
        const int Second = 20;
        using var list = new PooledList<int>(4);
        list.AddRange([First, Second]);
        IReadOnlyList<int> readOnly = list;

        // Act
        var value = readOnly[1];

        // Assert
        await Assert.That(value).IsEqualTo(Second);
        await Assert.That(readOnly.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ReadOnlyListIndexer_IndexEqualToCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const int Count = 1;
        using var list = new PooledList<int>(16);
        list.Add(1);
        IReadOnlyList<int> readOnly = list;

        // Act & Assert
        await Assert.That(() => _ = readOnly[Count]).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Linq_Select_ProjectsElementsInOrder()
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        const int Third = 3;
        const int Factor = 10;
        using var list = new PooledList<int>(4);
        list.AddRange([First, Second, Third]);

        // Act
        var projected = list.Select(x => x * Factor).ToArray();

        // Assert
        await Assert.That(projected)
            .IsEquivalentTo([First * Factor, Second * Factor, Third * Factor], CollectionOrdering.Matching);
    }

    [Test]
    public async Task NonGenericEnumerable_Foreach_IteratesAllElements()
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        using var list = new PooledList<int>(4);
        list.AddRange([First, Second]);
        IEnumerable untyped = list;

        // Act
        var collected = new List<int>();
        foreach (var item in untyped)
        {
            collected.Add((int)item!);
        }

        // Assert
        await Assert.That(collected).IsEquivalentTo([First, Second], CollectionOrdering.Matching);
    }

    [Test]
    public async Task InterfaceEnumerator_Reset_RestartsIteration()
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        using var list = new PooledList<int>(4);
        list.AddRange([First, Second]);
        using var enumerator = ((IEnumerable<int>)list).GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveNext();

        // Act
        enumerator.Reset();
        var moved = enumerator.MoveNext();

        // Assert
        await Assert.That(moved).IsTrue();
        await Assert.That(enumerator.Current).IsEqualTo(First);
    }

    [Test]
    public async Task InterfaceEnumerator_CurrentBeforeMoveNext_ThrowsInsteadOfReadingOutOfBounds()
    {
        // Arrange
        using var list = new PooledList<string>(4);
        list.Add("a");
        using var enumerator = ((IEnumerable<string>)list).GetEnumerator();

        // Act & Assert
        await Assert.That(() => _ = enumerator.Current).ThrowsExactly<IndexOutOfRangeException>();
    }

    [Test]
    public async Task Foreach_OverPooledList_DoesNotAllocate()
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        const int Third = 3;
        using var list = new PooledList<int>(4);
        list.AddRange([First, Second, Third]);
        _ = SumWithForeach(list); // warm up so JIT work is not measured

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = SumWithForeach(list);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(sum).IsEqualTo(First + Second + Third);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int SumWithForeach(PooledList<int> list)
    {
        var sum = 0;
        foreach (var item in list)
        {
            sum += item;
        }
        return sum;
    }
}
