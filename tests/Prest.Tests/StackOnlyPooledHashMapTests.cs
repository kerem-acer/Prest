namespace Prest.Tests;

/// <summary>
/// Tests for the default <see cref="StackOnlyPooledHashMap{TKey,TValue}" />. Every map is created,
/// used and disposed before the first <c>await</c>: a ref struct cannot live across one.
/// </summary>
public partial class StackOnlyPooledHashMapTests
{
    [Test]
    public async Task Add_NewKeys_IndexerReturnsEachValue()
    {
        // Arrange
        const string FirstKey = "one";
        const string SecondKey = "two";
        const int FirstValue = 1;
        const int SecondValue = 2;
        int count, first, second;
        {
            using var map = new StackOnlyPooledHashMap<string, int>(4);

            // Act
            map.Add(FirstKey, FirstValue);
            map.Add(SecondKey, SecondValue);
            count = map.Count;
            first = map[FirstKey];
            second = map[SecondKey];
        }

        // Assert
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(first).IsEqualTo(FirstValue);
        await Assert.That(second).IsEqualTo(SecondValue);
    }

    [Test]
    public async Task Add_ExistingKey_ThrowsArgumentException()
    {
        // Arrange
        const int Key = 5;

        // Act + Assert
        await Assert.That(() =>
        {
            using var map = new StackOnlyPooledHashMap<int, int>(4);
            map.Add(Key, 1);
            map.Add(Key, 2);
        }).Throws<ArgumentException>();
    }

    [Test]
    public async Task TryAdd_ExistingKey_ReturnsFalseAndKeepsValue()
    {
        // Arrange
        const string Key = "key";
        const int OriginalValue = 1;
        const int RejectedValue = 2;
        bool added;
        int count, stored;
        {
            using var map = new StackOnlyPooledHashMap<string, int>(4);
            map.Add(Key, OriginalValue);

            // Act
            added = map.TryAdd(Key, RejectedValue);
            count = map.Count;
            stored = map[Key];
        }

        // Assert
        await Assert.That(added).IsFalse();
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(stored).IsEqualTo(OriginalValue);
    }

    [Test]
    public async Task TryGetValue_MissingKey_ReturnsFalse()
    {
        // Arrange
        const string PresentKey = "present";
        const string MissingKey = "missing";
        bool found;
        int value;
        {
            using var map = new StackOnlyPooledHashMap<string, int>(4);
            map.Add(PresentKey, 1);

            // Act
            found = map.TryGetValue(MissingKey, out value);
        }

        // Assert
        await Assert.That(found).IsFalse();
        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task Indexer_MissingKey_ThrowsKeyNotFoundException()
    {
        // Arrange
        const string PresentKey = "present";
        const string MissingKey = "missing";

        // Act + Assert
        await Assert.That(() =>
        {
            using var map = new StackOnlyPooledHashMap<string, int>(4);
            map.Add(PresentKey, 1);
            _ = map[MissingKey];
        }).Throws<KeyNotFoundException>();
    }

    [Test]
    public async Task Remove_ExistingKey_RemovesOnlyThatKey()
    {
        // Arrange
        const string RemovedKey = "removed";
        const string KeptKey = "kept";
        bool removed, removedAgain, containsRemoved, containsKept;
        int count;
        {
            using var map = new StackOnlyPooledHashMap<string, int>(4);
            map.Add(RemovedKey, 1);
            map.Add(KeptKey, 2);

            // Act
            removed = map.Remove(RemovedKey);
            removedAgain = map.Remove(RemovedKey);
            containsRemoved = map.ContainsKey(RemovedKey);
            containsKept = map.ContainsKey(KeptKey);
            count = map.Count;
        }

        // Assert
        await Assert.That(removed).IsTrue();
        await Assert.That(removedAgain).IsFalse();
        await Assert.That(containsRemoved).IsFalse();
        await Assert.That(containsKept).IsTrue();
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Constructor_WithCapacity_PreAllocatesAtLeastThatMany()
    {
        // Arrange
        const int RequestedCapacity = 100;
        int capacity;
        bool isEmpty;

        // Act
        {
            using var map = new StackOnlyPooledHashMap<int, int>(RequestedCapacity);
            capacity = map.Capacity;
            isEmpty = map.IsEmpty;
        }

        // Assert
        await Assert.That(capacity).IsGreaterThanOrEqualTo(RequestedCapacity);
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task Default_FirstAdd_RentsBuffers()
    {
        // Arrange
        const int Key = 7;
        const int Value = 70;
        int capacityBefore, capacityAfter, count, stored;
        bool emptyBefore;
        {
            using StackOnlyPooledHashMap<int, int> map = default;
            capacityBefore = map.Capacity;
            emptyBefore = map.IsEmpty;

            // Act
            map.Add(Key, Value);
            capacityAfter = map.Capacity;
            count = map.Count;
            stored = map[Key];
        }

        // Assert
        await Assert.That(capacityBefore).IsEqualTo(0);
        await Assert.That(emptyBefore).IsTrue();
        await Assert.That(capacityAfter).IsGreaterThan(0);
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(stored).IsEqualTo(Value);
    }

    [Test]
    public async Task ParameterlessNew_FirstAdd_RentsBuffers()
    {
        // Arrange
        const string Key = "key";
        const int Value = 3;
        int capacityBefore, capacityAfter, stored;
        bool added, found;
        {
            using var map = new StackOnlyPooledHashMap<string, int>();
            capacityBefore = map.Capacity;

            // Act
            added = map.TryAdd(Key, Value);
            capacityAfter = map.Capacity;
            found = map.TryGetValue(Key, out stored);
        }

        // Assert
        await Assert.That(capacityBefore).IsEqualTo(0);
        await Assert.That(capacityAfter).IsGreaterThan(0);
        await Assert.That(added).IsTrue();
        await Assert.That(found).IsTrue();
        await Assert.That(stored).IsEqualTo(Value);
    }

    [Test]
    public async Task Remove_OnDefaultMap_ReturnsFalseWithoutRenting()
    {
        // Arrange
        const int Key = 1;
        bool removed, contains, found;
        int capacity;
        {
            using StackOnlyPooledHashMap<int, int> map = default;

            // Act
            removed = map.Remove(Key);
            contains = map.ContainsKey(Key);
            found = map.TryGetValue(Key, out _);
            map.Clear();
            capacity = map.Capacity;
        }

        // Assert
        await Assert.That(removed).IsFalse();
        await Assert.That(contains).IsFalse();
        await Assert.That(found).IsFalse();
        await Assert.That(capacity).IsEqualTo(0);
    }

    [Test]
    public async Task Clear_ThenAdd_KeepsCapacityAndStoresNewEntries()
    {
        // Arrange
        const string OldKey = "old";
        const string NewKey = "new";
        const int NewValue = 9;
        int capacityBefore, capacityAfter, countAfterClear, countAfterAdd, stored;
        bool containsOld;
        {
            using var map = new StackOnlyPooledHashMap<string, int>(16);
            map.Add(OldKey, 1);
            capacityBefore = map.Capacity;

            // Act
            map.Clear();
            countAfterClear = map.Count;
            map.Add(NewKey, NewValue);
            capacityAfter = map.Capacity;
            countAfterAdd = map.Count;
            containsOld = map.ContainsKey(OldKey);
            stored = map[NewKey];
        }

        // Assert
        await Assert.That(countAfterClear).IsEqualTo(0);
        await Assert.That(capacityAfter).IsEqualTo(capacityBefore);
        await Assert.That(countAfterAdd).IsEqualTo(1);
        await Assert.That(containsOld).IsFalse();
        await Assert.That(stored).IsEqualTo(NewValue);
    }

    [Test]
    public async Task GetEnumerator_Foreach_YieldsEveryEntry()
    {
        // Arrange
        KeyValuePair<string, int>[] expected = [new("a", 1), new("b", 2), new("c", 3)];
        var entries = new List<KeyValuePair<string, int>>();
        {
            using var map = new StackOnlyPooledHashMap<string, int>(4);
            foreach (var pair in expected)
            {
                map.Add(pair.Key, pair.Value);
            }

            // Act
            foreach (var entry in map)
            {
                entries.Add(entry);
            }
        }

        // Assert
        await Assert.That(entries.OrderBy(e => e.Key, StringComparer.Ordinal).ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Keys_Foreach_YieldsEveryKey()
    {
        // Arrange
        string[] expected = ["a", "b", "c"];
        var keys = new List<string>();
        int keyCount;
        {
            using var map = new StackOnlyPooledHashMap<string, int>(4);
            foreach (var key in expected)
            {
                map.Add(key, 0);
            }

            // Act
            keyCount = map.Keys.Count;
            foreach (var key in map.Keys)
            {
                keys.Add(key);
            }
        }

        // Assert
        await Assert.That(keyCount).IsEqualTo(expected.Length);
        await Assert.That(keys.Order(StringComparer.Ordinal).ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Values_Foreach_YieldsEveryValue()
    {
        // Arrange
        int[] expected = [10, 20, 30];
        var values = new List<int>();
        int valueCount;
        {
            using var map = new StackOnlyPooledHashMap<int, int>(4);
            foreach (var value in expected)
            {
                map.Add(value / 10, value);
            }

            // Act
            valueCount = map.Values.Count;
            foreach (var value in map.Values)
            {
                values.Add(value);
            }
        }

        // Assert
        await Assert.That(valueCount).IsEqualTo(expected.Length);
        await Assert.That(values.Order().ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task GetEnumerator_DefaultMap_YieldsNothing()
    {
        // Arrange
        var entries = 0;
        var keys = 0;
        var values = 0;
        StackOnlyPooledHashMap<int, int> map = default;

        // Act
        foreach (var _ in map)
        {
            entries++;
        }
        foreach (var _ in map.Keys)
        {
            keys++;
        }
        foreach (var _ in map.Values)
        {
            values++;
        }
        map.Dispose();

        // Assert
        await Assert.That(entries).IsEqualTo(0);
        await Assert.That(keys).IsEqualTo(0);
        await Assert.That(values).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_DisposableValues_DisposesEachValue()
    {
        // Arrange
        var first = new DisposableCounter();
        var second = new DisposableCounter();
        var map = new StackOnlyPooledHashMap<int, DisposableCounter>(4);
        map.Add(1, first);
        map.Add(2, second);

        // Act
        map.Dispose();
        var countAfter = map.Count;

        // Assert
        await Assert.That(first.DisposedCount).IsEqualTo(1);
        await Assert.That(second.DisposedCount).IsEqualTo(1);
        await Assert.That(countAfter).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_DisposableKeys_DisposesEachKey()
    {
        // Arrange
        var first = new DisposableKey(1);
        var second = new DisposableKey(2);
        var map = new StackOnlyPooledHashMap<DisposableKey, int>(4);
        map.Add(first, 10);
        map.Add(second, 20);

        // Act
        map.Dispose();

        // Assert
        await Assert.That(first.Counter.DisposedCount).IsEqualTo(1);
        await Assert.That(second.Counter.DisposedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Dispose_CalledTwice_DisposesValuesOnce()
    {
        // Arrange
        var value = new DisposableCounter();
        var map = new StackOnlyPooledHashMap<string, DisposableCounter>(4);
        map.Add("key", value);

        // Act
        map.Dispose();
        map.Dispose();
        var capacityAfter = map.Capacity;

        // Assert
        await Assert.That(value.DisposedCount).IsEqualTo(1);
        await Assert.That(capacityAfter).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_DefaultMap_LeavesEmptyMap()
    {
        // Arrange
        StackOnlyPooledHashMap<string, DisposableCounter> map = default;

        // Act
        map.Dispose();
        map.Dispose();
        var count = map.Count;
        var capacity = map.Capacity;

        // Assert
        await Assert.That(count).IsEqualTo(0);
        await Assert.That(capacity).IsEqualTo(0);
    }

    [Test]
    public async Task UsingVar_Add_MutatesTheLocal()
    {
        // Arrange
        const int Key = 1;
        const int Value = 1;
        int count;
        bool contains;
        {
            using var map = new StackOnlyPooledHashMap<int, int>();

            // Act — a defensive copy of the using variable would leave Count at 0.
            map.Add(Key, Value);
            count = map.Count;
            contains = map.ContainsKey(Key);
        }

        // Assert
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(contains).IsTrue();
    }

    [Test]
    public async Task AddThroughRefParameter_GrowsPastInitialCapacity_CallerSeesEveryEntry()
    {
        // Arrange
        const int KeyCount = 200;
        int count, capacity, missing;

        // A using variable can't be passed by ref (CS1657), so dispose in finally instead.
        var map = new StackOnlyPooledHashMap<int, int>(4);
        try
        {
            // Act
            AddKeys(ref map, KeyCount);
            count = map.Count;
            capacity = map.Capacity;
            missing = 0;
            for (var key = 0; key < KeyCount; key++)
            {
                if (!map.TryGetValue(key, out var value) || value != key)
                {
                    missing++;
                }
            }
        }
        finally
        {
            map.Dispose();
        }

        // Assert
        await Assert.That(count).IsEqualTo(KeyCount);
        await Assert.That(capacity).IsGreaterThanOrEqualTo(KeyCount);
        await Assert.That(missing).IsEqualTo(0);
    }

    // The documented way to hand a stack-only map to a helper: by ref, never by value.
    static void AddKeys(ref StackOnlyPooledHashMap<int, int> map, int keyCount)
    {
        for (var key = 0; key < keyCount; key++)
        {
            map.Add(key, key);
        }
    }

    sealed class DisposableCounter : IDisposable
    {
        public int DisposedCount { get; private set; }

        public void Dispose() => DisposedCount++;
    }

    sealed class DisposableKey(int id) : IDisposable, IEquatable<DisposableKey>
    {
        public int Id { get; } = id;

        public DisposableCounter Counter { get; } = new();

        public void Dispose() => Counter.Dispose();

        public bool Equals(DisposableKey? other) => other is not null && other.Id == Id;

        public override bool Equals(object? obj) => obj is DisposableKey key && Equals(key);

        public override int GetHashCode() => Id;
    }
}
