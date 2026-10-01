using System.Runtime.CompilerServices;
using TUnit.Assertions.Enums;

namespace Prest.Tests;

public partial class PooledStackTests
{
    [Test]
    public async Task Push_SingleItem_CountIsOneAndPeekReturnsItem()
    {
        // Arrange
        const int Item = 42;
        using var stack = new PooledStack<int>(4);

        // Act
        stack.Push(Item);

        // Assert
        await Assert.That(stack.Count).IsEqualTo(1);
        await Assert.That(stack.Peek()).IsEqualTo(Item);
    }

    [Test]
    public async Task Push_BeyondCapacity_GrowsAndKeepsPushOrderInSpan()
    {
        // Arrange
        const int ItemCount = 20;
        using var stack = new PooledStack<int>(4);

        // Act
        for (var i = 0; i < ItemCount; i++)
        {
            stack.Push(i);
        }

        // Assert
        await Assert.That(stack.Count).IsEqualTo(ItemCount);
        await Assert.That(stack.Capacity).IsGreaterThanOrEqualTo(ItemCount);
        await Assert.That(stack.Span.ToArray())
            .IsEquivalentTo(Enumerable.Range(0, ItemCount).ToArray(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task Push_ZeroInitialCapacity_GrowsOnFirstPush()
    {
        // Arrange
        const int Item = 7;
        using var stack = new PooledStack<int>(0);
        var initialCapacity = stack.Capacity;

        // Act
        stack.Push(Item);

        // Assert
        await Assert.That(initialCapacity).IsEqualTo(0);
        await Assert.That(stack.Capacity).IsGreaterThan(0);
        await Assert.That(stack.Peek()).IsEqualTo(Item);
    }

    [Test]
    public async Task Pop_AfterPushes_ReturnsItemsInReverseOrder()
    {
        // Arrange
        const int Bottom = 1;
        const int Middle = 2;
        const int Top = 3;
        using var stack = new PooledStack<int>(4);
        stack.Push(Bottom);
        stack.Push(Middle);
        stack.Push(Top);

        // Act
        var first = stack.Pop();
        var second = stack.Pop();
        var third = stack.Pop();

        // Assert
        await Assert.That(first).IsEqualTo(Top);
        await Assert.That(second).IsEqualTo(Middle);
        await Assert.That(third).IsEqualTo(Bottom);
        await Assert.That(stack.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Pop_Empty_ThrowsInvalidOperationException()
    {
        // Arrange
        using var stack = new PooledStack<int>(4);

        // Act & Assert
        await Assert.That(() => _ = stack.Pop()).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Pop_ReferenceType_ReleasesVacatedSlot()
    {
        // Arrange
        using var stack = new PooledStack<object>(4);

        // Act
        var popped = PushAndPopNewObject(stack);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var isAlive = popped.IsAlive;
        GC.KeepAlive(stack);

        // Assert
        await Assert.That(isAlive).IsFalse();
    }

    [Test]
    public async Task TryPop_Empty_ReturnsFalse()
    {
        // Arrange
        using var stack = new PooledStack<int>(4);

        // Act
        var popped = stack.TryPop(out var result);

        // Assert
        await Assert.That(popped).IsFalse();
        await Assert.That(result).IsEqualTo(default);
    }

    [Test]
    public async Task TryPop_NonEmpty_ReturnsTopAndRemovesIt()
    {
        // Arrange
        const int Bottom = 1;
        const int Top = 2;
        using var stack = new PooledStack<int>(4);
        stack.Push(Bottom);
        stack.Push(Top);

        // Act
        var popped = stack.TryPop(out var result);

        // Assert
        await Assert.That(popped).IsTrue();
        await Assert.That(result).IsEqualTo(Top);
        await Assert.That(stack.Count).IsEqualTo(1);
        await Assert.That(stack.Peek()).IsEqualTo(Bottom);
    }

    [Test]
    public async Task Peek_NonEmpty_ReturnsTopWithoutRemoving()
    {
        // Arrange
        const int Bottom = 1;
        const int Top = 2;
        using var stack = new PooledStack<int>(4);
        stack.Push(Bottom);
        stack.Push(Top);

        // Act
        var peeked = stack.Peek();

        // Assert
        await Assert.That(peeked).IsEqualTo(Top);
        await Assert.That(stack.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Peek_Empty_ThrowsInvalidOperationException()
    {
        // Arrange
        using var stack = new PooledStack<int>(4);

        // Act & Assert
        await Assert.That(() => _ = stack.Peek()).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task TryPeek_Empty_ReturnsFalse()
    {
        // Arrange
        using var stack = new PooledStack<string>(4);

        // Act
        var peeked = stack.TryPeek(out var result);

        // Assert
        await Assert.That(peeked).IsFalse();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task TryPeek_NonEmpty_ReturnsTopWithoutRemoving()
    {
        // Arrange
        const string Top = "top";
        using var stack = new PooledStack<string>(4);
        stack.Push(Top);

        // Act
        var peeked = stack.TryPeek(out var result);

        // Assert
        await Assert.That(peeked).IsTrue();
        await Assert.That(result).IsEqualTo(Top);
        await Assert.That(stack.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetEnumerator_Foreach_IteratesTopToBottom()
    {
        // Arrange
        const int Bottom = 10;
        const int Middle = 20;
        const int Top = 30;
        using var stack = new PooledStack<int>(4);
        stack.Push(Bottom);
        stack.Push(Middle);
        stack.Push(Top);

        // Act
        var collected = new List<int>();
        foreach (var item in stack)
        {
            collected.Add(item);
        }

        // Assert
        await Assert.That(collected).IsEquivalentTo([Top, Middle, Bottom], CollectionOrdering.Matching);
    }

    [Test]
    public async Task GetEnumerator_Empty_YieldsNothing()
    {
        // Arrange
        using var stack = new PooledStack<int>(4);

        // Act
        var iterations = 0;
        foreach (var _ in stack)
        {
            iterations++;
        }

        // Assert
        await Assert.That(iterations).IsEqualTo(0);
    }

    [Test]
    public async Task GetEnumerator_RefCurrent_AllowsMutation()
    {
        // Arrange
        const int Bottom = 1;
        const int Top = 2;
        const int Factor = 10;
        using var stack = new PooledStack<int>(4);
        stack.Push(Bottom);
        stack.Push(Top);

        // Act
        foreach (ref var item in stack)
        {
            item *= Factor;
        }

        // Assert
        await Assert.That(stack.Span.ToArray())
            .IsEquivalentTo([Bottom * Factor, Top * Factor], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Clear_WithItems_ResetsCountAndKeepsCapacity()
    {
        // Arrange
        using var stack = new PooledStack<string>(16);
        stack.Push("a");
        stack.Push("b");
        var capacityBefore = stack.Capacity;

        // Act
        stack.Clear();

        // Assert
        await Assert.That(stack.Count).IsEqualTo(0);
        await Assert.That(stack.Capacity).IsEqualTo(capacityBefore);
        await Assert.That(stack.TryPeek(out _)).IsFalse();
    }

    [Test]
    public async Task Clear_Empty_CountStaysZero()
    {
        // Arrange
        using var stack = new PooledStack<int>(4);

        // Act
        stack.Clear();

        // Assert
        await Assert.That(stack.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_WithItems_ResetsCountAndCapacity()
    {
        // Arrange
        var stack = new PooledStack<int>(4, clearOnReturn: true);
        stack.Push(1);
        stack.Push(2);

        // Act
        stack.Dispose();

        // Assert
        await Assert.That(stack.Count).IsEqualTo(0);
        await Assert.That(stack.Capacity).IsEqualTo(0);
    }

    [Test]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var stack = new PooledStack<int>(4);
        stack.Push(1);

        // Act & Assert
        stack.Dispose();
        stack.Dispose();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference PushAndPopNewObject(PooledStack<object> stack)
    {
        var item = new object();
        stack.Push(item);
        _ = stack.Pop();
        return new WeakReference(item);
    }
}
