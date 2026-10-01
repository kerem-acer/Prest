using System.Runtime.CompilerServices;
using TUnit.Assertions.Enums;

namespace Prest.Tests;

public partial class ValuePooledListTests
{
    [Test]
    public async Task RemoveAt_MiddleIndex_ShiftsTailDown()
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        const int Third = 3;
        var list = new ValuePooledList<int>(4);
        list.AddRange([First, Second, Third]);

        // Act
        list.RemoveAt(1);
        var count = list.Count;
        var items = list.Span.ToArray();
        list.Dispose();

        // Assert
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(items).IsEquivalentTo([First, Third], CollectionOrdering.Matching);
    }

    [Test]
    public async Task RemoveAt_LastIndex_DropsOnlyLastElement()
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        var list = new ValuePooledList<int>(4);
        list.AddRange([First, Second]);

        // Act
        list.RemoveAt(1);
        var items = list.Span.ToArray();
        list.Dispose();

        // Assert
        await Assert.That(items).IsEquivalentTo([First], CollectionOrdering.Matching);
    }

    [Test]
    public async Task RemoveAt_IndexEqualToCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const int Count = 1;
        var list = new ValuePooledList<int>(16);
        list.Add(1);

        // Act
        Exception? caught = null;
        try
        {
            list.RemoveAt(Count);
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        var count = list.Count;
        list.Dispose();

        // Assert
        await Assert.That(caught).IsTypeOf<ArgumentOutOfRangeException>();
        await Assert.That(count).IsEqualTo(Count);
    }

    [Test]
    public async Task RemoveAt_ReferenceType_ReleasesVacatedSlot()
    {
        // Arrange
        var list = new ValuePooledList<object>(4);

        // Act
        var removed = AddTwoThenRemoveBothFromFront(ref list);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var isAlive = removed.IsAlive;
        list.Dispose();

        // Assert
        await Assert.That(isAlive).IsFalse();
    }

    // See PooledListTests.AddTwoThenRemoveBothFromFront: the item is only collectable if
    // both removals cleared their vacated slot.
    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference AddTwoThenRemoveBothFromFront(ref ValuePooledList<object> list)
    {
        var item = new object();
        list.Add(new object());
        list.Add(item);
        list.RemoveAt(0);
        list.RemoveAt(0);
        return new WeakReference(item);
    }
}
