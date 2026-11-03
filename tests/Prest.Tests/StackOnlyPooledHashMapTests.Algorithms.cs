using ChainedAlgo = Prest.ChainedAlgorithm<Prest.KeyValueSlot<int, int>, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>;
using ComparerChainedAlgo = Prest.ChainedAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerLinearAlgo = Prest.LinearProbingAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerRobinHoodAlgo = Prest.RobinHoodAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerSwissAlgo = Prest.SwissTableAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using LinearAlgo = Prest.LinearProbingAlgorithm<Prest.KeyValueSlot<int, int>, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>;
using RobinHoodAlgo = Prest.RobinHoodAlgorithm<Prest.KeyValueSlot<int, int>, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>;
using SwissAlgo = Prest.SwissTableAlgorithm<Prest.KeyValueSlot<int, int>, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>;

namespace Prest.Tests;

/// <summary>
/// The same bodies run against all four algorithms through the generic
/// <see cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}" />. Each helper is generic over the algorithm
/// and builds, uses and disposes the map synchronously, returning plain values to assert on.
/// </summary>
public partial class StackOnlyPooledHashMapTests
{
    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AddRemoveReAdd_FromCapacityZero_KeepsEveryKey(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int KeyCount = 5000;
        const int ReAddOffset = 1_000_000;

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => GrowAndChurn<SwissAlgo>(KeyCount, ReAddOffset),
            HashAlgorithmKind.RobinHood => GrowAndChurn<RobinHoodAlgo>(KeyCount, ReAddOffset),
            HashAlgorithmKind.Linear => GrowAndChurn<LinearAlgo>(KeyCount, ReAddOffset),
            HashAlgorithmKind.Chained => GrowAndChurn<ChainedAlgo>(KeyCount, ReAddOffset),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.CapacityBefore).IsEqualTo(0);
        await Assert.That(result.Removed).IsEqualTo(KeyCount / 2);
        await Assert.That(result.CountAfterRemove).IsEqualTo(KeyCount / 2);
        await Assert.That(result.FinalCount).IsEqualTo(KeyCount);
        await Assert.That(result.FinalCapacity).IsGreaterThanOrEqualTo(KeyCount);
        await Assert.That(result.WrongValues).IsEqualTo(0);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Enumerate_AfterRemovals_YieldsOnlyLiveEntries(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int KeyCount = 100;
        const int ValueFactor = 10;
        var expectedKeys = Enumerable.Range(0, KeyCount).Where(key => key % 3 != 0).ToArray();
        var expectedValues = expectedKeys.Select(key => key * ValueFactor).ToArray();

        // Act
        var (entryKeys, entryValues, keys, values) = algorithm switch
        {
            HashAlgorithmKind.Swiss => EnumerateAfterRemovals<SwissAlgo>(KeyCount, ValueFactor),
            HashAlgorithmKind.RobinHood => EnumerateAfterRemovals<RobinHoodAlgo>(KeyCount, ValueFactor),
            HashAlgorithmKind.Linear => EnumerateAfterRemovals<LinearAlgo>(KeyCount, ValueFactor),
            HashAlgorithmKind.Chained => EnumerateAfterRemovals<ChainedAlgo>(KeyCount, ValueFactor),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(entryKeys).IsEquivalentTo(expectedKeys);
        await Assert.That(entryValues).IsEquivalentTo(expectedValues);
        await Assert.That(keys).IsEquivalentTo(expectedKeys);
        await Assert.That(values).IsEquivalentTo(expectedValues);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task UsingVar_Add_MutatesTheLocalAndReturnsBuffersOnce(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int Key = 1;
        const int Value = 1;

        // Act
        var (count, returns) = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThroughUsingVar<SwissAlgo>(Key, Value),
            HashAlgorithmKind.RobinHood => AddThroughUsingVar<RobinHoodAlgo>(Key, Value),
            HashAlgorithmKind.Linear => AddThroughUsingVar<LinearAlgo>(Key, Value),
            HashAlgorithmKind.Chained => AddThroughUsingVar<ChainedAlgo>(Key, Value),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert — a defensive copy would leave Count at 0 and return nothing (the copy owns the buffers).
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(returns).IsEqualTo(1);
    }

    [Test]
    public async Task AddThroughInParameter_DefensiveCopy_LeavesTheCallerUnchanged()
    {
        // Arrange — the control for the using-var test above: an `in` parameter is read-only, so
        // calling Add on it mutates a hidden copy. The copy rents its own buffers and is dropped
        // (the GC collects them), so nothing is returned twice.
        const int Key = 1;
        const int Value = 1;

        // Act
        var (count, returns) = AddThroughInParameter(Key, Value);

        // Assert — the counting harness sees the copy: the caller never rented, so it returns nothing.
        await Assert.That(count).IsEqualTo(0);
        await Assert.That(returns).IsEqualTo(0);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Dispose_CalledTwice_ReturnsBuffersOnce(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int KeyCount = 50;

        // Act
        var (returnsAfterFirst, returnsAfterSecond, countAfter) = algorithm switch
        {
            HashAlgorithmKind.Swiss => DisposeTwice<SwissAlgo>(KeyCount),
            HashAlgorithmKind.RobinHood => DisposeTwice<RobinHoodAlgo>(KeyCount),
            HashAlgorithmKind.Linear => DisposeTwice<LinearAlgo>(KeyCount),
            HashAlgorithmKind.Chained => DisposeTwice<ChainedAlgo>(KeyCount),
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
    public async Task Dispose_NeverAdded_ReturnsNothing(HashAlgorithmKind algorithm)
    {
        // Act
        var returns = algorithm switch
        {
            HashAlgorithmKind.Swiss => DisposeUnused<SwissAlgo>(),
            HashAlgorithmKind.RobinHood => DisposeUnused<RobinHoodAlgo>(),
            HashAlgorithmKind.Linear => DisposeUnused<LinearAlgo>(),
            HashAlgorithmKind.Chained => DisposeUnused<ChainedAlgo>(),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(returns).IsEqualTo(0);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Constructor_WithComparerAlgorithm_UsesThatComparer(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string StoredKey = "Key";
        const string OtherCaseKey = "KEY";
        const int Value = 42;
        var hasher = new ComparerHasher<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        var (added, found, stored) = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThenLookUp(new ComparerSwissAlgo(hasher, default), StoredKey, OtherCaseKey, Value),
            HashAlgorithmKind.RobinHood => AddThenLookUp(new ComparerRobinHoodAlgo(hasher, default), StoredKey, OtherCaseKey, Value),
            HashAlgorithmKind.Linear => AddThenLookUp(new ComparerLinearAlgo(hasher, default), StoredKey, OtherCaseKey, Value),
            HashAlgorithmKind.Chained => AddThenLookUp(new ComparerChainedAlgo(hasher, default), StoredKey, OtherCaseKey, Value),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(added).IsFalse();
        await Assert.That(found).IsTrue();
        await Assert.That(stored).IsEqualTo(Value);
    }

    readonly record struct ChurnResult(int CapacityBefore, int Removed, int CountAfterRemove, int FinalCount, int FinalCapacity, int WrongValues);

    // Adds every key to a map that starts unallocated, removes the even keys, re-adds them with a new
    // value, then counts the keys whose stored value is wrong.
    static ChurnResult GrowAndChurn<TAlgo>(int keyCount, int reAddOffset)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        using var map = new StackOnlyPooledHashMap<int, int, TAlgo>(0);
        var capacityBefore = map.Capacity;
        for (var key = 0; key < keyCount; key++)
        {
            map.Add(key, key);
        }

        var removed = 0;
        for (var key = 0; key < keyCount; key += 2)
        {
            if (map.Remove(key))
            {
                removed++;
            }
        }
        var countAfterRemove = map.Count;

        for (var key = 0; key < keyCount; key += 2)
        {
            map.Add(key, key + reAddOffset);
        }

        var wrongValues = 0;
        for (var key = 0; key < keyCount; key++)
        {
            var expected = key % 2 == 0 ? key + reAddOffset : key;
            if (!map.TryGetValue(key, out var value) || value != expected)
            {
                wrongValues++;
            }
        }

        return new ChurnResult(capacityBefore, removed, countAfterRemove, map.Count, map.Capacity, wrongValues);
    }

    // Adds keys [0, keyCount) with value key * valueFactor, removes every multiple of three, then
    // enumerates entries, Keys and Values; each result is sorted by key (values by value).
    static (int[] EntryKeys, int[] EntryValues, int[] Keys, int[] Values) EnumerateAfterRemovals<TAlgo>(int keyCount, int valueFactor)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        using var map = new StackOnlyPooledHashMap<int, int, TAlgo>(0);
        for (var key = 0; key < keyCount; key++)
        {
            map.Add(key, key * valueFactor);
        }
        for (var key = 0; key < keyCount; key += 3)
        {
            map.Remove(key);
        }

        var entries = new List<KeyValuePair<int, int>>();
        foreach (var entry in map)
        {
            entries.Add(entry);
        }
        var keys = new List<int>();
        foreach (var key in map.Keys)
        {
            keys.Add(key);
        }
        var values = new List<int>();
        foreach (var value in map.Values)
        {
            values.Add(value);
        }

        var sortedEntries = entries.OrderBy(entry => entry.Key).ToArray();
        return (
            sortedEntries.Select(entry => entry.Key).ToArray(),
            sortedEntries.Select(entry => entry.Value).ToArray(),
            keys.Order().ToArray(),
            values.Order().ToArray());
    }

    static (int Count, int Returns) AddThroughUsingVar<TInner>(int key, int value)
        where TInner : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        var log = new BufferReturnLog();
        int count;
        {
            using var map = new StackOnlyPooledHashMap<int, int, ReturnCountingAlgorithm<KeyValueSlot<int, int>, int, TInner>>(0, new(log));
            map.Add(key, value);
            count = map.Count;
        }
        return (count, log.Returns);
    }

    static (int Count, int Returns) AddThroughInParameter(int key, int value)
    {
        var log = new BufferReturnLog();
        int count;
        {
            using var map = new StackOnlyPooledHashMap<int, int, ReturnCountingAlgorithm<KeyValueSlot<int, int>, int, SwissAlgo>>(0, new(log));
            AddToReadOnlyReference(in map, key, value);
            count = map.Count;
        }
        return (count, log.Returns);
    }

    // What the XML docs warn against: Add on an `in` parameter runs on a defensive copy.
    static void AddToReadOnlyReference(in StackOnlyPooledHashMap<int, int, ReturnCountingAlgorithm<KeyValueSlot<int, int>, int, SwissAlgo>> map, int key, int value) =>
        map.Add(key, value);

    static (int ReturnsAfterFirst, int ReturnsAfterSecond, int CountAfter) DisposeTwice<TInner>(int keyCount)
        where TInner : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        var log = new BufferReturnLog();
        var map = new StackOnlyPooledHashMap<int, int, ReturnCountingAlgorithm<KeyValueSlot<int, int>, int, TInner>>(0, new(log));
        for (var key = 0; key < keyCount; key++)
        {
            map.Add(key, key);
        }

        map.Dispose();
        var returnsAfterFirst = log.Returns;
        map.Dispose();
        return (returnsAfterFirst, log.Returns, map.Count);
    }

    static int DisposeUnused<TInner>()
        where TInner : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        var log = new BufferReturnLog();
        var map = new StackOnlyPooledHashMap<int, int, ReturnCountingAlgorithm<KeyValueSlot<int, int>, int, TInner>>(0, new(log));
        map.Dispose();
        return log.Returns;
    }

    // Adds storedKey, tries to add lookupKey (equal under the algorithm's comparer), then looks it up.
    static (bool Added, bool Found, int Stored) AddThenLookUp<TAlgo>(TAlgo algorithm, string storedKey, string lookupKey, int value)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        using var map = new StackOnlyPooledHashMap<string, int, TAlgo>(0, algorithm);
        map.Add(storedKey, value);
        var added = map.TryAdd(lookupKey, value + 1);
        var found = map.TryGetValue(lookupKey, out var stored);
        return (added, found, stored);
    }
}
