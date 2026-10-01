using System.Runtime.CompilerServices;
using TUnit.Assertions.Enums;

namespace Prest.Tests;

public partial class PooledListTests
{
    [Test]
    public async Task RemoveAt_FirstIndex_ShiftsRemainingDown()
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        const int Third = 3;
        using var list = new PooledList<int>(4);
        list.AddRange([First, Second, Third]);

        // Act
        list.RemoveAt(0);

        // Assert
        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(list.Span.ToArray()).IsEquivalentTo([Second, Third], CollectionOrdering.Matching);
    }

    [Test]
    public async Task RemoveAt_MiddleIndex_ShiftsTailDown()
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        const int Third = 3;
        const int Fourth = 4;
        using var list = new PooledList<int>(4);
        list.AddRange([First, Second, Third, Fourth]);

        // Act
        list.RemoveAt(1);

        // Assert
        await Assert.That(list.Span.ToArray()).IsEquivalentTo([First, Third, Fourth], CollectionOrdering.Matching);
    }

    [Test]
    public async Task RemoveAt_LastIndex_DropsOnlyLastElement()
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        using var list = new PooledList<int>(4);
        list.AddRange([First, Second]);

        // Act
        list.RemoveAt(1);

        // Assert
        await Assert.That(list.Span.ToArray()).IsEquivalentTo([First], CollectionOrdering.Matching);
    }

    [Test]
    public async Task RemoveAt_ThenAdd_AppendsAfterRemainingElements()
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        const int Added = 9;
        using var list = new PooledList<int>(4);
        list.AddRange([First, Second]);
        list.RemoveAt(0);

        // Act
        list.Add(Added);

        // Assert
        await Assert.That(list.Span.ToArray()).IsEquivalentTo([Second, Added], CollectionOrdering.Matching);
    }

    [Test]
    public async Task RemoveAt_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var list = new PooledList<int>(4);
        list.Add(1);

        // Act & Assert
        await Assert.That(() => list.RemoveAt(-1)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task RemoveAt_IndexEqualToCount_ThrowsEvenWithSpareCapacity()
    {
        // Arrange
        const int Count = 2;
        using var list = new PooledList<int>(16);
        list.AddRange([1, 2]);

        // Act & Assert
        await Assert.That(() => list.RemoveAt(Count)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(list.Count).IsEqualTo(Count);
    }

    [Test]
    public async Task RemoveAt_ReferenceType_ReleasesVacatedSlot()
    {
        // Arrange
        using var list = new PooledList<object>(4);

        // Act
        var removed = AddTwoThenRemoveBothFromFront(list);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var isAlive = removed.IsAlive;
        GC.KeepAlive(list);

        // Assert
        await Assert.That(isAlive).IsFalse();
    }

    // [other, item] → RemoveAt(0) shifts item down and leaves a stale copy in slot 1 unless
    // the vacated slot is cleared; the second RemoveAt(0) drops the live copy. The item is
    // only collectable if both removals cleared their vacated slot.
    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference AddTwoThenRemoveBothFromFront(PooledList<object> list)
    {
        var item = new object();
        list.Add(new object());
        list.Add(item);
        list.RemoveAt(0);
        list.RemoveAt(0);
        return new WeakReference(item);
    }
}
