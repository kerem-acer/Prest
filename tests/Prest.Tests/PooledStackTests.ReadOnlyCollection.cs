using System.Runtime.CompilerServices;
using TUnit.Assertions.Enums;

namespace Prest.Tests;

public partial class PooledStackTests
{
    [Test]
    public async Task ReadOnlyCollection_Linq_EnumeratesTopToBottom()
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
        var items = stack.ToArray();

        // Assert
        await Assert.That(items).IsEquivalentTo([Top, Middle, Bottom], CollectionOrdering.Matching);
    }

    [Test]
    public async Task InterfaceEnumerator_Reset_RestartsFromTop()
    {
        // Arrange
        const int Bottom = 1;
        const int Top = 2;
        using var stack = new PooledStack<int>(4);
        stack.Push(Bottom);
        stack.Push(Top);
        using var enumerator = ((IEnumerable<int>)stack).GetEnumerator();
        while (enumerator.MoveNext())
        {
        }

        // Act
        enumerator.Reset();
        var moved = enumerator.MoveNext();

        // Assert
        await Assert.That(moved).IsTrue();
        await Assert.That(enumerator.Current).IsEqualTo(Top);
    }

    [Test]
    public async Task InterfaceEnumerator_CurrentAfterEnd_ThrowsInsteadOfReadingOutOfBounds()
    {
        // Arrange
        using var stack = new PooledStack<string>(4);
        stack.Push("a");
        using var enumerator = ((IEnumerable<string>)stack).GetEnumerator();
        while (enumerator.MoveNext())
        {
        }

        // Act & Assert
        await Assert.That(() => _ = enumerator.Current).ThrowsExactly<IndexOutOfRangeException>();
    }

    [Test]
    public async Task Foreach_OverStack_DoesNotAllocate()
    {
        // Arrange
        const int Bottom = 1;
        const int Top = 2;
        using var stack = new PooledStack<int>(4);
        stack.Push(Bottom);
        stack.Push(Top);
        _ = SumWithForeach(stack); // warm up so JIT work is not measured

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = SumWithForeach(stack);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(sum).IsEqualTo(Bottom + Top);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int SumWithForeach(PooledStack<int> stack)
    {
        var sum = 0;
        foreach (var item in stack)
        {
            sum += item;
        }
        return sum;
    }
}
