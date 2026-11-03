using ChainedAlgo = Prest.ChainedAlgorithm<int, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>;
using ComparerChainedAlgo = Prest.ChainedAlgorithm<string, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerLinearAlgo = Prest.LinearProbingAlgorithm<string, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerRobinHoodAlgo = Prest.RobinHoodAlgorithm<string, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerSwissAlgo = Prest.SwissTableAlgorithm<string, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using LinearAlgo = Prest.LinearProbingAlgorithm<int, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>;
using RobinHoodAlgo = Prest.RobinHoodAlgorithm<int, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>;
using SwissAlgo = Prest.SwissTableAlgorithm<int, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>;

namespace Prest.Tests;

/// <summary>
/// The same bodies run against all four algorithms through the generic
/// <see cref="StackOnlyPooledHashSet{T,TAlgo}" />. Each helper is generic over the algorithm and
/// builds, uses and disposes the set synchronously, returning plain values to assert on.
/// </summary>
public partial class StackOnlyPooledHashSetTests
{
    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AddRemoveReAdd_FromCapacityZero_KeepsEveryItem(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int ItemCount = 5000;

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => GrowAndChurn<SwissAlgo>(ItemCount),
            HashAlgorithmKind.RobinHood => GrowAndChurn<RobinHoodAlgo>(ItemCount),
            HashAlgorithmKind.Linear => GrowAndChurn<LinearAlgo>(ItemCount),
            HashAlgorithmKind.Chained => GrowAndChurn<ChainedAlgo>(ItemCount),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.CapacityBefore).IsEqualTo(0);
        await Assert.That(result.DuplicatesAdded).IsEqualTo(0);
        await Assert.That(result.Removed).IsEqualTo(ItemCount / 2);
        await Assert.That(result.CountAfterRemove).IsEqualTo(ItemCount / 2);
        await Assert.That(result.FinalCount).IsEqualTo(ItemCount);
        await Assert.That(result.Missing).IsEqualTo(0);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Enumerate_AfterRemovals_YieldsOnlyLiveItems(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int ItemCount = 100;
        var expected = Enumerable.Range(0, ItemCount).Where(item => item % 3 != 0).ToArray();

        // Act
        var (enumerated, copied) = algorithm switch
        {
            HashAlgorithmKind.Swiss => EnumerateAfterRemovals<SwissAlgo>(ItemCount),
            HashAlgorithmKind.RobinHood => EnumerateAfterRemovals<RobinHoodAlgo>(ItemCount),
            HashAlgorithmKind.Linear => EnumerateAfterRemovals<LinearAlgo>(ItemCount),
            HashAlgorithmKind.Chained => EnumerateAfterRemovals<ChainedAlgo>(ItemCount),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(enumerated).IsEquivalentTo(expected);
        await Assert.That(copied).IsEquivalentTo(expected);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task UsingVar_Add_MutatesTheLocalAndReturnsBuffersOnce(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int Item = 1;

        // Act
        var (count, returns) = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThroughUsingVar<SwissAlgo>(Item),
            HashAlgorithmKind.RobinHood => AddThroughUsingVar<RobinHoodAlgo>(Item),
            HashAlgorithmKind.Linear => AddThroughUsingVar<LinearAlgo>(Item),
            HashAlgorithmKind.Chained => AddThroughUsingVar<ChainedAlgo>(Item),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert — a defensive copy would leave Count at 0 and return nothing (the copy owns the buffers).
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(returns).IsEqualTo(1);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Dispose_CalledTwice_ReturnsBuffersOnce(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int ItemCount = 50;

        // Act
        var (returnsAfterFirst, returnsAfterSecond, countAfter) = algorithm switch
        {
            HashAlgorithmKind.Swiss => DisposeTwice<SwissAlgo>(ItemCount),
            HashAlgorithmKind.RobinHood => DisposeTwice<RobinHoodAlgo>(ItemCount),
            HashAlgorithmKind.Linear => DisposeTwice<LinearAlgo>(ItemCount),
            HashAlgorithmKind.Chained => DisposeTwice<ChainedAlgo>(ItemCount),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(returnsAfterFirst).IsEqualTo(1);
        await Assert.That(returnsAfterSecond).IsEqualTo(1);
        await Assert.That(countAfter).IsEqualTo(0);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Constructor_WithComparerAlgorithm_UsesThatComparer(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string StoredItem = "Item";
        const string OtherCaseItem = "ITEM";
        var hasher = new ComparerHasher<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        var (added, found, actual) = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThenLookUp(new ComparerSwissAlgo(hasher, default), StoredItem, OtherCaseItem),
            HashAlgorithmKind.RobinHood => AddThenLookUp(new ComparerRobinHoodAlgo(hasher, default), StoredItem, OtherCaseItem),
            HashAlgorithmKind.Linear => AddThenLookUp(new ComparerLinearAlgo(hasher, default), StoredItem, OtherCaseItem),
            HashAlgorithmKind.Chained => AddThenLookUp(new ComparerChainedAlgo(hasher, default), StoredItem, OtherCaseItem),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(added).IsFalse();
        await Assert.That(found).IsTrue();
        await Assert.That(actual).IsEqualTo(StoredItem);
    }

    readonly record struct ChurnResult(int CapacityBefore, int DuplicatesAdded, int Removed, int CountAfterRemove, int FinalCount, int Missing);

    // Adds every item twice to a set that starts unallocated, removes the even items, re-adds them,
    // then counts the items that are missing.
    static ChurnResult GrowAndChurn<TAlgo>(int itemCount)
        where TAlgo : struct, IHashAlgorithm<int, int>
    {
        using var set = new StackOnlyPooledHashSet<int, TAlgo>(0);
        var capacityBefore = set.Capacity;
        var duplicatesAdded = 0;
        for (var item = 0; item < itemCount; item++)
        {
            set.Add(item);
            if (set.Add(item))
            {
                duplicatesAdded++;
            }
        }

        var removed = 0;
        for (var item = 0; item < itemCount; item += 2)
        {
            if (set.Remove(item))
            {
                removed++;
            }
        }
        var countAfterRemove = set.Count;

        for (var item = 0; item < itemCount; item += 2)
        {
            set.Add(item);
        }

        var missing = 0;
        for (var item = 0; item < itemCount; item++)
        {
            if (!set.Contains(item))
            {
                missing++;
            }
        }

        return new ChurnResult(capacityBefore, duplicatesAdded, removed, countAfterRemove, set.Count, missing);
    }

    // Adds [0, itemCount), removes every multiple of three, then enumerates and copies the set; both
    // results are sorted.
    static (int[] Enumerated, int[] Copied) EnumerateAfterRemovals<TAlgo>(int itemCount)
        where TAlgo : struct, IHashAlgorithm<int, int>
    {
        using var set = new StackOnlyPooledHashSet<int, TAlgo>(0);
        for (var item = 0; item < itemCount; item++)
        {
            set.Add(item);
        }
        for (var item = 0; item < itemCount; item += 3)
        {
            set.Remove(item);
        }

        var enumerated = new List<int>();
        foreach (var item in set)
        {
            enumerated.Add(item);
        }
        return (enumerated.Order().ToArray(), set.ToArray().Order().ToArray());
    }

    static (int Count, int Returns) AddThroughUsingVar<TInner>(int item)
        where TInner : struct, IHashAlgorithm<int, int>
    {
        var log = new BufferReturnLog();
        int count;
        {
            using var set = new StackOnlyPooledHashSet<int, ReturnCountingAlgorithm<int, int, TInner>>(0, new(log));
            set.Add(item);
            count = set.Count;
        }
        return (count, log.Returns);
    }

    static (int ReturnsAfterFirst, int ReturnsAfterSecond, int CountAfter) DisposeTwice<TInner>(int itemCount)
        where TInner : struct, IHashAlgorithm<int, int>
    {
        var log = new BufferReturnLog();
        var set = new StackOnlyPooledHashSet<int, ReturnCountingAlgorithm<int, int, TInner>>(0, new(log));
        for (var item = 0; item < itemCount; item++)
        {
            set.Add(item);
        }

        set.Dispose();
        var returnsAfterFirst = log.Returns;
        set.Dispose();
        return (returnsAfterFirst, log.Returns, set.Count);
    }

    // Adds storedItem, tries to add lookupItem (equal under the algorithm's comparer), then looks it up.
    static (bool Added, bool Found, string? Actual) AddThenLookUp<TAlgo>(TAlgo algorithm, string storedItem, string lookupItem)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        using var set = new StackOnlyPooledHashSet<string, TAlgo>(0, algorithm);
        set.Add(storedItem);
        var added = set.Add(lookupItem);
        var found = set.TryGetValue(lookupItem, out var actual);
        return (added, found, actual);
    }
}
