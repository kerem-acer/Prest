using System.Runtime.CompilerServices;
using TUnit.Assertions.Enums;

namespace Prest.Tests;

public partial class PooledHashMapTests
{
    const int EntryCount = 64;
    const int ValueFactor = 10;

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task ReadOnlyDictionary_LookupsAfterRemovals_MatchRemainingEntries(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int PresentKey = 7;
        const int RemovedKey = 8;
        var map = CreateMapWithOddKeys(algorithm);
        using var owner = (IDisposable)map;

        // Act
        var count = map.Count;
        var indexed = map[PresentKey];
        var foundPresent = map.TryGetValue(PresentKey, out var presentValue);
        var foundRemoved = map.TryGetValue(RemovedKey, out _);
        var containsRemoved = map.ContainsKey(RemovedKey);

        // Assert
        await Assert.That(count).IsEqualTo(EntryCount / 2);
        await Assert.That(indexed).IsEqualTo(PresentKey * ValueFactor);
        await Assert.That(foundPresent).IsTrue();
        await Assert.That(presentValue).IsEqualTo(PresentKey * ValueFactor);
        await Assert.That(foundRemoved).IsFalse();
        await Assert.That(containsRemoved).IsFalse();
        await Assert.That(() => _ = map[RemovedKey]).ThrowsExactly<KeyNotFoundException>();
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task ReadOnlyDictionary_EnumerationAfterRemovals_YieldsRemainingEntries(HashAlgorithmKind algorithm)
    {
        // Arrange
        var expectedKeys = OddKeys();
        var expectedValues = expectedKeys.Select(k => k * ValueFactor).ToArray();
        var map = CreateMapWithOddKeys(algorithm);
        using var owner = (IDisposable)map;

        // Act
        var entryKeys = map.Where(kv => kv.Value == kv.Key * ValueFactor).Select(kv => kv.Key).Order().ToArray();
        var keys = map.Keys.Order().ToArray();
        var values = map.Values.Order().ToArray();

        // Assert
        await Assert.That(entryKeys).IsEquivalentTo(expectedKeys, CollectionOrdering.Matching);
        await Assert.That(keys).IsEquivalentTo(expectedKeys, CollectionOrdering.Matching);
        await Assert.That(values).IsEquivalentTo(expectedValues, CollectionOrdering.Matching);
    }

    [Test]
    public async Task KeyCollection_Linq_EnumeratesKeysDirectly()
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        using var map = PooledHashMap<int, int>.Create();
        map.Add(First, 0);
        map.Add(Second, 0);

        // Act
        var keys = map.Keys.Order().ToArray();

        // Assert
        await Assert.That(keys).IsEquivalentTo([First, Second], CollectionOrdering.Matching);
    }

    [Test]
    public async Task InterfaceEnumerator_Reset_RestartsIteration()
    {
        // Arrange
        const int Key = 1;
        const int Value = 2;
        using var map = PooledHashMap<int, int>.Create();
        map.Add(Key, Value);
        using var enumerator = ((IEnumerable<KeyValuePair<int, int>>)map).GetEnumerator();
        while (enumerator.MoveNext())
        {
        }

        // Act
        enumerator.Reset();
        var moved = enumerator.MoveNext();

        // Assert
        await Assert.That(moved).IsTrue();
        await Assert.That(enumerator.Current).IsEqualTo(new KeyValuePair<int, int>(Key, Value));
    }

    [Test]
    public async Task InterfaceEnumerator_CurrentBeforeMoveNext_ThrowsInsteadOfReadingOutOfBounds()
    {
        // Arrange
        using var map = PooledHashMap<int, int>.Create();
        map.Add(1, 1);
        using var enumerator = ((IEnumerable<KeyValuePair<int, int>>)map).GetEnumerator();

        // Act & Assert
        await Assert.That(() => _ = enumerator.Current).ThrowsExactly<IndexOutOfRangeException>();
    }

    [Test]
    public async Task Foreach_OverMapKeysAndValues_DoesNotAllocate()
    {
        // Arrange
        const int Key = 3;
        const int Value = 4;
        using var map = PooledHashMap<int, int>.Create();
        map.Add(Key, Value);
        _ = SumWithForeach(map); // warm up so JIT work is not measured

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = SumWithForeach(map);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(sum).IsEqualTo((Key + Value) * 2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int SumWithForeach<TAlgo>(PooledHashMap<int, int, TAlgo> map)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        var sum = 0;
        foreach (var entry in map)
        {
            sum += entry.Key + entry.Value;
        }
        foreach (var key in map.Keys)
        {
            sum += key;
        }
        foreach (var value in map.Values)
        {
            sum += value;
        }
        return sum;
    }

    static int[] OddKeys() => Enumerable.Range(0, EntryCount).Where(k => k % 2 == 1).ToArray();

    // Builds a map of key → key * ValueFactor for 0..EntryCount-1 starting from a small
    // capacity (forcing growth), then removes the even keys to exercise each algorithm's
    // removal path (tombstones, backward shift, swap-with-last) before enumeration.
    static IReadOnlyDictionary<int, int> CreateMapWithOddKeys(HashAlgorithmKind algorithm) => algorithm switch
    {
        HashAlgorithmKind.Swiss => FillThenRemoveEvenKeys(SwissHashMap<int, int>.Create(4)),
        HashAlgorithmKind.RobinHood => FillThenRemoveEvenKeys(RobinHoodHashMap<int, int>.Create(4)),
        HashAlgorithmKind.Linear => FillThenRemoveEvenKeys(LinearHashMap<int, int>.Create(4)),
        HashAlgorithmKind.Chained => FillThenRemoveEvenKeys(ChainedHashMap<int, int>.Create(4)),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
    };

    static PooledHashMap<int, int, TAlgo> FillThenRemoveEvenKeys<TAlgo>(PooledHashMap<int, int, TAlgo> map)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        for (var key = 0; key < EntryCount; key++)
        {
            map.Add(key, key * ValueFactor);
        }
        for (var key = 0; key < EntryCount; key += 2)
        {
            map.Remove(key);
        }
        return map;
    }
}
