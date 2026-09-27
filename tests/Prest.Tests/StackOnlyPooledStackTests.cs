using System.Runtime.CompilerServices;
using TUnit.Assertions.Enums;

namespace Prest.Tests;

public class StackOnlyPooledStackTests
{
    [Test]
    public async Task Push_SingleItem_CountIsOneAndPeekReturnsItem()
    {
        // Arrange
        const int Item = 42;
        var stack = new StackOnlyPooledStack<int>(4);

        // Act
        stack.Push(Item);
        var count = stack.Count;
        var peeked = stack.Peek();
        stack.Dispose();

        // Assert
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(peeked).IsEqualTo(Item);
    }

    [Test]
    public async Task Push_BeyondCapacity_GrowsAndKeepsPushOrderInSpan()
    {
        // Arrange
        const int ItemCount = 20;
        var stack = new StackOnlyPooledStack<int>(4);

        // Act
        for (var i = 0; i < ItemCount; i++)
        {
            stack.Push(i);
        }

        var count = stack.Count;
        var capacity = stack.Capacity;
        var items = stack.Span.ToArray();
        stack.Dispose();

        // Assert
        await Assert.That(count).IsEqualTo(ItemCount);
        await Assert.That(capacity).IsGreaterThanOrEqualTo(ItemCount);
        await Assert.That(items)
            .IsEquivalentTo(Enumerable.Range(0, ItemCount).ToArray(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task Push_ZeroInitialCapacity_GrowsOnFirstPush()
    {
        // Arrange
        const int Item = 7;
        var stack = new StackOnlyPooledStack<int>(0);
        var initialCapacity = stack.Capacity;

        // Act
        stack.Push(Item);
        var capacity = stack.Capacity;
        var peeked = stack.Peek();
        stack.Dispose();

        // Assert
        await Assert.That(initialCapacity).IsEqualTo(0);
        await Assert.That(capacity).IsGreaterThan(0);
        await Assert.That(peeked).IsEqualTo(Item);
    }

    [Test]
    public async Task Push_WithinInlineBuffer_DoesNotRent()
    {
        // Arrange
        const int BufferLength = 4;
        Span<int> buffer = stackalloc int[BufferLength];
        var stack = new StackOnlyPooledStack<int>(buffer);

        // Act
        for (var i = 0; i < BufferLength; i++)
        {
            stack.Push(i);
        }

        var capacity = stack.Capacity;
        var bufferTop = buffer[BufferLength - 1];
        var peeked = stack.Peek();
        stack.Dispose();

        // Assert
        await Assert.That(capacity).IsEqualTo(BufferLength);
        await Assert.That(bufferTop).IsEqualTo(peeked);
    }

    [Test]
    public async Task Push_PastInlineBuffer_RentsAndKeepsPushOrder()
    {
        // Arrange
        const int BufferLength = 4;
        const int ItemCount = BufferLength + 1;
        Span<int> buffer = stackalloc int[BufferLength];
        var stack = new StackOnlyPooledStack<int>(buffer);

        // Act
        for (var i = 0; i < ItemCount; i++)
        {
            stack.Push(i);
        }

        var capacity = stack.Capacity;
        var items = stack.Span.ToArray();
        stack.Dispose();

        // Assert
        await Assert.That(capacity).IsGreaterThan(BufferLength);
        await Assert.That(items)
            .IsEquivalentTo(Enumerable.Range(0, ItemCount).ToArray(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task Pop_AfterPushes_ReturnsItemsInReverseOrder()
    {
        // Arrange
        const int Bottom = 1;
        const int Middle = 2;
        const int Top = 3;
        Span<int> buffer = stackalloc int[4];
        var stack = new StackOnlyPooledStack<int>(buffer);
        stack.Push(Bottom);
        stack.Push(Middle);
        stack.Push(Top);

        // Act
        var first = stack.Pop();
        var second = stack.Pop();
        var third = stack.Pop();
        var count = stack.Count;
        stack.Dispose();

        // Assert
        await Assert.That(first).IsEqualTo(Top);
        await Assert.That(second).IsEqualTo(Middle);
        await Assert.That(third).IsEqualTo(Bottom);
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Pop_Empty_ThrowsInvalidOperationException()
    {
        // Arrange
        var stack = new StackOnlyPooledStack<int>(4);

        // Act
        Exception? caught = null;
        try
        {
            _ = stack.Pop();
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        stack.Dispose();

        // Assert
        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
    }

    [Test]
    public async Task Pop_ReferenceType_ReleasesVacatedSlot()
    {
        // Arrange
        var stack = new StackOnlyPooledStack<object>(4);

        // Act
        var popped = PushAndPopNewObject(ref stack);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var isAlive = popped.IsAlive;
        stack.Dispose();

        // Assert
        await Assert.That(isAlive).IsFalse();
    }

    [Test]
    public async Task TryPop_Empty_ReturnsFalse()
    {
        // Arrange
        var stack = new StackOnlyPooledStack<int>(4);

        // Act
        var popped = stack.TryPop(out var result);
        stack.Dispose();

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
        var stack = new StackOnlyPooledStack<int>(4);
        stack.Push(Bottom);
        stack.Push(Top);

        // Act
        var popped = stack.TryPop(out var result);
        var count = stack.Count;
        var peeked = stack.Peek();
        stack.Dispose();

        // Assert
        await Assert.That(popped).IsTrue();
        await Assert.That(result).IsEqualTo(Top);
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(peeked).IsEqualTo(Bottom);
    }

    [Test]
    public async Task Peek_NonEmpty_ReturnsTopWithoutRemoving()
    {
        // Arrange
        const int Bottom = 1;
        const int Top = 2;
        var stack = new StackOnlyPooledStack<int>(4);
        stack.Push(Bottom);
        stack.Push(Top);

        // Act
        var peeked = stack.Peek();
        var count = stack.Count;
        stack.Dispose();

        // Assert
        await Assert.That(peeked).IsEqualTo(Top);
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task Peek_Empty_ThrowsInvalidOperationException()
    {
        // Arrange
        var stack = new StackOnlyPooledStack<int>(4);

        // Act
        Exception? caught = null;
        try
        {
            _ = stack.Peek();
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        stack.Dispose();

        // Assert
        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
    }

    [Test]
    public async Task TryPeek_Empty_ReturnsFalse()
    {
        // Arrange
        var stack = new StackOnlyPooledStack<string>(4);

        // Act
        var peeked = stack.TryPeek(out var result);
        stack.Dispose();

        // Assert
        await Assert.That(peeked).IsFalse();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task TryPeek_NonEmpty_ReturnsTopWithoutRemoving()
    {
        // Arrange
        const string Top = "top";
        var stack = new StackOnlyPooledStack<string>(4);
        stack.Push(Top);

        // Act
        var peeked = stack.TryPeek(out var result);
        var count = stack.Count;
        stack.Dispose();

        // Assert
        await Assert.That(peeked).IsTrue();
        await Assert.That(result).IsEqualTo(Top);
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task GetEnumerator_Foreach_IteratesTopToBottom()
    {
        // Arrange
        const int Bottom = 10;
        const int Middle = 20;
        const int Top = 30;
        Span<int> buffer = stackalloc int[4];
        var stack = new StackOnlyPooledStack<int>(buffer);
        stack.Push(Bottom);
        stack.Push(Middle);
        stack.Push(Top);

        // Act
        var collected = new List<int>();
        foreach (var item in stack)
        {
            collected.Add(item);
        }
        stack.Dispose();

        // Assert
        await Assert.That(collected).IsEquivalentTo([Top, Middle, Bottom], CollectionOrdering.Matching);
    }

    [Test]
    public async Task GetEnumerator_Empty_YieldsNothing()
    {
        // Arrange
        var stack = new StackOnlyPooledStack<int>(4);

        // Act
        var iterations = 0;
        foreach (var _ in stack)
        {
            iterations++;
        }
        stack.Dispose();

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
        var stack = new StackOnlyPooledStack<int>(4);
        stack.Push(Bottom);
        stack.Push(Top);

        // Act
        foreach (ref var item in stack)
        {
            item *= Factor;
        }

        var items = stack.Span.ToArray();
        stack.Dispose();

        // Assert
        await Assert.That(items).IsEquivalentTo([Bottom * Factor, Top * Factor], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Clear_WithItems_ResetsCountAndKeepsCapacity()
    {
        // Arrange
        var stack = new StackOnlyPooledStack<string>(16);
        stack.Push("a");
        stack.Push("b");
        var capacityBefore = stack.Capacity;

        // Act
        stack.Clear();
        var count = stack.Count;
        var capacityAfter = stack.Capacity;
        var hasTop = stack.TryPeek(out _);
        stack.Dispose();

        // Assert
        await Assert.That(count).IsEqualTo(0);
        await Assert.That(capacityAfter).IsEqualTo(capacityBefore);
        await Assert.That(hasTop).IsFalse();
    }

    [Test]
    public async Task Clear_Empty_CountStaysZero()
    {
        // Arrange
        var stack = new StackOnlyPooledStack<int>(4);

        // Act
        stack.Clear();
        var count = stack.Count;
        stack.Dispose();

        // Assert
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_WithItems_ResetsCountAndCapacity()
    {
        // Arrange
        var stack = new StackOnlyPooledStack<int>(4, clearOnReturn: true);
        stack.Push(1);
        stack.Push(2);

        // Act
        stack.Dispose();
        var count = stack.Count;
        var capacity = stack.Capacity;

        // Assert
        await Assert.That(count).IsEqualTo(0);
        await Assert.That(capacity).IsEqualTo(0);
    }

    [Test]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var stack = new StackOnlyPooledStack<int>(4);
        stack.Push(1);

        // Act & Assert
        stack.Dispose();
        stack.Dispose();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference PushAndPopNewObject(ref StackOnlyPooledStack<object> stack)
    {
        var item = new object();
        stack.Push(item);
        _ = stack.Pop();
        return new WeakReference(item);
    }
}
