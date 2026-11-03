using System.Buffers;

namespace Prest.Tests;

/// <summary>
/// <see cref="PooledHashMap{TKey,TValue}" /> takes an optional comparer, like <see cref="Dictionary{TKey,TValue}" />.
/// </summary>
public partial class PooledHashMapTests
{
    [Test]
    public async Task Create_WithComparer_UsesItForKeyEquality()
    {
        // Arrange
        const string Key = "Hello";
        const string OtherCaseKey = "HELLO";
        const int Value = 1;
        var comparer = StringComparer.OrdinalIgnoreCase;
        using var map = PooledHashMap<string, int>.Create(comparer, capacity: 8);

        // Act
        map.Add(Key, Value);
        var found = map.TryGetValue(OtherCaseKey, out var value);
        var addedAgain = map.TryAdd(OtherCaseKey, Value);

        // Assert
        await Assert.That(found).IsTrue();
        await Assert.That(value).IsEqualTo(Value);
        await Assert.That(addedAgain).IsFalse();
        await Assert.That(map.Count).IsEqualTo(1);
        await Assert.That(map.Comparer).IsSameReferenceAs(comparer);
    }

    [Test]
    public async Task Create_NullOrDefaultComparer_UsesDefaultComparer()
    {
        // Arrange
        using var withoutComparer = PooledHashMap<string, int>.Create();
        using var withNull = PooledHashMap<string, int>.Create(null);
        using var withDefault = PooledHashMap<string, int>.Create(EqualityComparer<string>.Default);

        // Act
        var comparers = new[] { withoutComparer.Comparer, withNull.Comparer, withDefault.Comparer };

        // Assert
        foreach (var comparer in comparers)
        {
            await Assert.That(comparer).IsSameReferenceAs(EqualityComparer<string>.Default);
        }
    }

    [Test]
    public async Task Create_AfterDispose_ReusesCachedMapOnlyForTheSameComparer()
    {
        // Arrange
        var comparer = StringComparer.OrdinalIgnoreCase;
        var otherComparer = StringComparer.Ordinal;
        PooledHashMap<string, int>.Create(comparer).Dispose(); // takes whatever another test left in the cache

        // Act
        var first = PooledHashMap<string, int>.Create(comparer);
        first.Dispose();
        var sameComparer = PooledHashMap<string, int>.Create(comparer);
        sameComparer.Dispose();
        var otherComparerMap = PooledHashMap<string, int>.Create(otherComparer);
        otherComparerMap.Dispose();
        var defaultComparerMap = PooledHashMap<string, int>.Create();
        defaultComparerMap.Dispose();
        var defaultAgain = PooledHashMap<string, int>.Create(EqualityComparer<string>.Default);
        defaultAgain.Dispose();

        // Assert
        await Assert.That(sameComparer).IsSameReferenceAs(first);
        await Assert.That(otherComparerMap).IsNotSameReferenceAs(sameComparer);
        await Assert.That(otherComparerMap.Comparer).IsSameReferenceAs(otherComparer);
        await Assert.That(defaultComparerMap).IsNotSameReferenceAs(otherComparerMap);
        await Assert.That(defaultAgain).IsSameReferenceAs(defaultComparerMap);
    }

    [Test]
    public async Task Create_ComparerAndRentedArrays_UsesComparerForLookups()
    {
        // Arrange
        const string Key = "Key";
        const string OtherCaseKey = "KEY";
        const int Value = 3;
        var keys = ArrayPool<string>.Shared.Rent(1);
        var values = ArrayPool<int>.Shared.Rent(1);
        keys[0] = Key;
        values[0] = Value;

        // Act
        using var map = PooledHashMap<string, int>.Create(StringComparer.OrdinalIgnoreCase, keys, values, 1);
        var found = map.TryGetValue(OtherCaseKey, out var value);

        // Assert
        await Assert.That(found).IsTrue();
        await Assert.That(value).IsEqualTo(Value);
        await Assert.That(map.Comparer).IsSameReferenceAs(StringComparer.OrdinalIgnoreCase);
    }

    [Test]
    public async Task CollectionExpression_WithComparer_UsesComparerAndKeepsLastValue()
    {
        // Arrange
        const string Key = "a";
        const string OtherCaseKey = "A";
        const int FirstValue = 1;
        const int LastValue = 2;
        const int Capacity = 64;

        // Act
        PooledHashMap<string, int> map = [with(StringComparer.OrdinalIgnoreCase), new(Key, FirstValue), new(OtherCaseKey, LastValue)];
        PooledHashMap<string, int> sized = [with(StringComparer.OrdinalIgnoreCase, Capacity)];
        using (map)
        using (sized)
        {
            // Assert
            await Assert.That(map.Count).IsEqualTo(1);
            await Assert.That(map[Key]).IsEqualTo(LastValue);
            await Assert.That(map.Comparer).IsSameReferenceAs(StringComparer.OrdinalIgnoreCase);
            await Assert.That(sized.Capacity).IsGreaterThanOrEqualTo(Capacity);
            await Assert.That(sized.Comparer).IsSameReferenceAs(StringComparer.OrdinalIgnoreCase);
        }
    }

    [Test]
    public async Task GetAlternateLookup_DefaultAliasWithSpanComparer_LooksUpBySpanAndCreatesKeyOnce()
    {
        // Arrange
        const string Key = "token";
        const int Value = 9;
        var comparer = new CountingSpanComparer();
        using var map = PooledHashMap<string, int>.Create(comparer);
        var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();

        // Act
        var added = lookup.TryAdd(Key.AsSpan(), Value);
        var addedAgain = lookup.TryAdd(Key.AsSpan(), Value);
        var found = lookup.TryGetValue(Key.AsSpan(), out var value);

        // Assert
        await Assert.That(added).IsTrue();
        await Assert.That(addedAgain).IsFalse();
        await Assert.That(found).IsTrue();
        await Assert.That(value).IsEqualTo(Value);
        await Assert.That(comparer.CreateCount).IsEqualTo(1);
    }

    [Test]
    public async Task TryGetAlternateLookup_DefaultAliasWithComparerWithoutSpanSupport_ReturnsFalse()
    {
        // Arrange
        using var map = PooledHashMap<string, int>.Create(new NoAlternateComparer());

        // Act
        var supported = map.TryGetAlternateLookup<ReadOnlySpan<char>>(out _);

        // Assert
        await Assert.That(supported).IsFalse();
    }

    [Test]
    public async Task SwissHashMap_NotDerivedFromDefaultAlias_KeepsTheComparerFreeHasher()
    {
        // Arrange — SwissHashMap<K,V> stays on EqualityDefaultHasher so it pays nothing for comparer support.
        var swiss = typeof(SwissHashMap<int, int>);

        // Act
        var derivesFromDefault = typeof(PooledHashMap<int, int>).IsAssignableFrom(swiss);
        var swissBase = swiss.BaseType;

        // Assert
        await Assert.That(derivesFromDefault).IsFalse();
        await Assert.That(swissBase).IsEqualTo(
            typeof(PooledHashMap<int, int, SwissTableAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>));
    }
}
