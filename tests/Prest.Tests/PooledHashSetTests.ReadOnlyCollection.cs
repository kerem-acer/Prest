using System.Runtime.CompilerServices;
using TUnit.Assertions.Enums;

namespace Prest.Tests;

public partial class PooledHashSetTests
{
    const int ItemCount = 64;

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task ReadOnlyCollection_EnumerationAfterRemovals_YieldsRemainingItems(HashAlgorithmKind algorithm)
    {
        // Arrange
        var expected = Enumerable.Range(0, ItemCount).Where(i => i % 2 == 1).ToArray();
        var set = CreateSetWithOddItems(algorithm);
        using var owner = (IDisposable)set;

        // Act
        var count = set.Count;
        var items = set.Order().ToArray();

        // Assert
        await Assert.That(count).IsEqualTo(expected.Length);
        await Assert.That(items).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    [Test]
    public async Task InterfaceEnumerator_Reset_RestartsIteration()
    {
        // Arrange
        const int Item = 5;
        using var set = PooledHashSet<int>.Create();
        set.Add(Item);
        using var enumerator = ((IEnumerable<int>)set).GetEnumerator();
        while (enumerator.MoveNext())
        {
        }

        // Act
        enumerator.Reset();
        var moved = enumerator.MoveNext();

        // Assert
        await Assert.That(moved).IsTrue();
        await Assert.That(enumerator.Current).IsEqualTo(Item);
    }

    [Test]
    public async Task InterfaceEnumerator_CurrentBeforeMoveNext_ThrowsInsteadOfReadingOutOfBounds()
    {
        // Arrange
        using var set = PooledHashSet<int>.Create();
        set.Add(1);
        using var enumerator = ((IEnumerable<int>)set).GetEnumerator();

        // Act & Assert
        await Assert.That(() => _ = enumerator.Current).ThrowsExactly<IndexOutOfRangeException>();
    }

    [Test]
    public async Task Foreach_OverSet_DoesNotAllocate()
    {
        // Arrange
        const int First = 2;
        const int Second = 3;
        using var set = PooledHashSet<int>.Create();
        set.Add(First);
        set.Add(Second);
        _ = SumWithForeach(set); // warm up so JIT work is not measured

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = SumWithForeach(set);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(sum).IsEqualTo(First + Second);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int SumWithForeach<TAlgo>(PooledHashSet<int, TAlgo> set)
        where TAlgo : struct, IHashAlgorithm<int, int>
    {
        var sum = 0;
        foreach (var item in set)
        {
            sum += item;
        }
        return sum;
    }

    // Adds 0..ItemCount-1 starting from a small capacity (forcing growth), then removes the
    // even items to exercise each algorithm's removal path before enumeration.
    static IReadOnlyCollection<int> CreateSetWithOddItems(HashAlgorithmKind algorithm) => algorithm switch
    {
        HashAlgorithmKind.Swiss => FillThenRemoveEvenItems(SwissHashSet<int>.Create(4)),
        HashAlgorithmKind.RobinHood => FillThenRemoveEvenItems(RobinHoodHashSet<int>.Create(4)),
        HashAlgorithmKind.Linear => FillThenRemoveEvenItems(LinearHashSet<int>.Create(4)),
        HashAlgorithmKind.Chained => FillThenRemoveEvenItems(ChainedHashSet<int>.Create(4)),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
    };

    static PooledHashSet<int, TAlgo> FillThenRemoveEvenItems<TAlgo>(PooledHashSet<int, TAlgo> set)
        where TAlgo : struct, IHashAlgorithm<int, int>
    {
        for (var i = 0; i < ItemCount; i++)
        {
            set.Add(i);
        }
        for (var i = 0; i < ItemCount; i += 2)
        {
            set.Remove(i);
        }
        return set;
    }
}
