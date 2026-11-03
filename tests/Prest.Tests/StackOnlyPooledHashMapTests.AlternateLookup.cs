using System.Text;
using TUnit.Assertions.Enums;
using ComparerChainedAlgo = Prest.ChainedAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerLinearAlgo = Prest.LinearProbingAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerRobinHoodAlgo = Prest.RobinHoodAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerSwissAlgo = Prest.SwissTableAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using MemoryChainedAlgo = Prest.ChainedAlgorithm<Prest.KeyValueSlot<System.ReadOnlyMemory<byte>, int>, System.ReadOnlyMemory<byte>, Prest.ComparerHasher<System.ReadOnlyMemory<byte>>, Prest.NoOpHashFinalizer>;
using MemoryLinearAlgo = Prest.LinearProbingAlgorithm<Prest.KeyValueSlot<System.ReadOnlyMemory<byte>, int>, System.ReadOnlyMemory<byte>, Prest.ComparerHasher<System.ReadOnlyMemory<byte>>, Prest.NoOpHashFinalizer>;
using MemoryRobinHoodAlgo = Prest.RobinHoodAlgorithm<Prest.KeyValueSlot<System.ReadOnlyMemory<byte>, int>, System.ReadOnlyMemory<byte>, Prest.ComparerHasher<System.ReadOnlyMemory<byte>>, Prest.NoOpHashFinalizer>;
using MemorySwissAlgo = Prest.SwissTableAlgorithm<Prest.KeyValueSlot<System.ReadOnlyMemory<byte>, int>, System.ReadOnlyMemory<byte>, Prest.ComparerHasher<System.ReadOnlyMemory<byte>>, Prest.NoOpHashFinalizer>;
using StringChainedAlgo = Prest.ChainedAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.EqualityDefaultHasher<string>, Prest.NoOpHashFinalizer>;
using StringLinearAlgo = Prest.LinearProbingAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.EqualityDefaultHasher<string>, Prest.NoOpHashFinalizer>;
using StringRobinHoodAlgo = Prest.RobinHoodAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.EqualityDefaultHasher<string>, Prest.NoOpHashFinalizer>;
using StringSwissAlgo = Prest.SwissTableAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.EqualityDefaultHasher<string>, Prest.NoOpHashFinalizer>;

namespace Prest.Tests;

/// <summary>
/// Alternate-key lookups on <see cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}" /> and the default
/// <see cref="StackOnlyPooledHashMap{TKey,TValue}" />. Every helper declares its map as a
/// <c>using var</c> local, gets the lookups from it, and returns plain values to assert on.
/// </summary>
public partial class StackOnlyPooledHashMapTests
{
    const int MissingValue = -1;

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_IgnoreCaseComparer_FindsAddsAndRemovesAnyCasing(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string StoredKey = "Hello";
        const string LookupKey = "hELLO";
        const string NewKey = "World";
        const string NewKeyOtherCase = "WORLD";
        const int Value = 5;
        const int NewValue = 6;
        var hasher = new ComparerHasher<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => UseIgnoreCaseLookup(new ComparerSwissAlgo(hasher, default), StoredKey, LookupKey, NewKey, NewKeyOtherCase, Value, NewValue),
            HashAlgorithmKind.RobinHood => UseIgnoreCaseLookup(new ComparerRobinHoodAlgo(hasher, default), StoredKey, LookupKey, NewKey, NewKeyOtherCase, Value, NewValue),
            HashAlgorithmKind.Linear => UseIgnoreCaseLookup(new ComparerLinearAlgo(hasher, default), StoredKey, LookupKey, NewKey, NewKeyOtherCase, Value, NewValue),
            HashAlgorithmKind.Chained => UseIgnoreCaseLookup(new ComparerChainedAlgo(hasher, default), StoredKey, LookupKey, NewKey, NewKeyOtherCase, Value, NewValue),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.Contains).IsTrue();
        await Assert.That(result.Found).IsTrue();
        await Assert.That(result.FoundValue).IsEqualTo(Value);
        await Assert.That(result.ActualKey).IsEqualTo(StoredKey);
        await Assert.That(result.Indexed).IsEqualTo(Value);
        await Assert.That(result.AddedDuplicate).IsFalse();
        await Assert.That(result.AddedNew).IsTrue();
        await Assert.That(result.CountAfterAdd).IsEqualTo(2);
        await Assert.That(result.NewValueByKey).IsEqualTo(NewValue);
        await Assert.That(result.Removed).IsTrue();
        await Assert.That(result.RemovedKey).IsEqualTo(StoredKey);
        await Assert.That(result.RemovedValue).IsEqualTo(Value);
        await Assert.That(result.RemovedNew).IsTrue();
        await Assert.That(result.CountAfterRemove).IsEqualTo(0);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_AddAndRemoveBySpanFromCapacityZero_GrowsAndKeepsRemainingEntries(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int EntryCount = 300;
        const int ValueFactor = 10;
        var keys = StringKeys("key-", EntryCount);
        var expectedValues = Enumerable.Range(0, EntryCount).Select(i => i % 2 == 1 ? i * ValueFactor : MissingValue).ToArray();
        var expectedRemovedKeys = keys.Where((_, i) => i % 2 == 0).ToArray();
        var expectedRemovedValues = Enumerable.Range(0, EntryCount).Where(i => i % 2 == 0).Select(i => i * ValueFactor).ToArray();

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThenRemoveEvenKeysBySpan<StringSwissAlgo>(keys, ValueFactor),
            HashAlgorithmKind.RobinHood => AddThenRemoveEvenKeysBySpan<StringRobinHoodAlgo>(keys, ValueFactor),
            HashAlgorithmKind.Linear => AddThenRemoveEvenKeysBySpan<StringLinearAlgo>(keys, ValueFactor),
            HashAlgorithmKind.Chained => AddThenRemoveEvenKeysBySpan<StringChainedAlgo>(keys, ValueFactor),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.CapacityBefore).IsEqualTo(0);
        await Assert.That(result.ContainedBeforeAdd).IsFalse();
        await Assert.That(result.RemovedBeforeAdd).IsFalse();
        await Assert.That(result.Added).IsEqualTo(EntryCount);
        await Assert.That(result.CapacityAfterAdd).IsGreaterThanOrEqualTo(EntryCount);
        await Assert.That(result.RemovedKeys).IsEquivalentTo(expectedRemovedKeys, CollectionOrdering.Matching);
        await Assert.That(result.RemovedValues).IsEquivalentTo(expectedRemovedValues, CollectionOrdering.Matching);
        await Assert.That(result.Count).IsEqualTo(EntryCount / 2);
        await Assert.That(result.ValuesBySpan).IsEquivalentTo(expectedValues, CollectionOrdering.Matching);
        await Assert.That(result.ValuesByKey).IsEquivalentTo(expectedValues, CollectionOrdering.Matching);
        await Assert.That(result.StoredKey).IsEqualTo(keys[1]);
        await Assert.That(result.StoredKey).IsNotSameReferenceAs(keys[1]);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_MemoryKeysWithByteComparer_FindsAddsAndRemovesBySpan(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string StoredAddress = "10.0.0.1";
        const string NewAddress = "10.0.0.2";
        const int StoredValue = 7;
        const int NewValue = 8;
        var comparer = new ByteContentComparer();
        var hasher = new ComparerHasher<ReadOnlyMemory<byte>>(comparer);

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => UseMemoryKeysBySpan(new MemorySwissAlgo(hasher, default), StoredAddress, NewAddress, StoredValue, NewValue),
            HashAlgorithmKind.RobinHood => UseMemoryKeysBySpan(new MemoryRobinHoodAlgo(hasher, default), StoredAddress, NewAddress, StoredValue, NewValue),
            HashAlgorithmKind.Linear => UseMemoryKeysBySpan(new MemoryLinearAlgo(hasher, default), StoredAddress, NewAddress, StoredValue, NewValue),
            HashAlgorithmKind.Chained => UseMemoryKeysBySpan(new MemoryChainedAlgo(hasher, default), StoredAddress, NewAddress, StoredValue, NewValue),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.Found).IsTrue();
        await Assert.That(result.FoundValue).IsEqualTo(StoredValue);
        await Assert.That(result.AddedNew).IsTrue();
        await Assert.That(result.AddedDuplicate).IsFalse();
        await Assert.That(result.NewValueByKey).IsEqualTo(NewValue);
        await Assert.That(comparer.CreateCount).IsEqualTo(1);
        await Assert.That(result.Removed).IsTrue();
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.ContainsRemoved).IsFalse();
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_Utf8StructHasher_AddsFindsAndRemovesByBytes(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int EntryCount = 300;
        const int ValueFactor = 10;
        var keys = StringKeys("clé-日本-", EntryCount);
        var expectedValues = Enumerable.Range(0, EntryCount).Select(i => i % 2 == 1 ? i * ValueFactor : MissingValue).ToArray();

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThenRemoveEvenKeysByUtf8<StringSwissAlgo>(keys, ValueFactor),
            HashAlgorithmKind.RobinHood => AddThenRemoveEvenKeysByUtf8<StringRobinHoodAlgo>(keys, ValueFactor),
            HashAlgorithmKind.Linear => AddThenRemoveEvenKeysByUtf8<StringLinearAlgo>(keys, ValueFactor),
            HashAlgorithmKind.Chained => AddThenRemoveEvenKeysByUtf8<StringChainedAlgo>(keys, ValueFactor),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.Added).IsEqualTo(EntryCount);
        await Assert.That(result.AddedDuplicate).IsFalse();
        await Assert.That(result.Contains).IsTrue();
        await Assert.That(result.Indexed).IsEqualTo(ValueFactor);
        await Assert.That(result.ActualKey).IsEqualTo(keys[1]);
        await Assert.That(result.RemovedKey).IsEqualTo(keys[0]);
        await Assert.That(result.Removed).IsEqualTo(EntryCount / 2);
        await Assert.That(result.Count).IsEqualTo(EntryCount / 2);
        await Assert.That(result.ValuesByBytes).IsEquivalentTo(expectedValues, CollectionOrdering.Matching);
        await Assert.That(result.ValuesByKey).IsEquivalentTo(expectedValues, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task TryGetAlternateLookup_ComparerWithoutSpanSupport_ReturnsFalse(HashAlgorithmKind algorithm)
    {
        // Arrange
        var hasher = new ComparerHasher<string>(new NoAlternateComparer());

        // Act
        var supported = algorithm switch
        {
            HashAlgorithmKind.Swiss => SupportsSpanLookup(new ComparerSwissAlgo(hasher, default)),
            HashAlgorithmKind.RobinHood => SupportsSpanLookup(new ComparerRobinHoodAlgo(hasher, default)),
            HashAlgorithmKind.Linear => SupportsSpanLookup(new ComparerLinearAlgo(hasher, default)),
            HashAlgorithmKind.Chained => SupportsSpanLookup(new ComparerChainedAlgo(hasher, default)),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(supported).IsFalse();
    }

    [Test]
    public async Task GetAlternateLookup_ComparerWithoutSpanSupport_ThrowsInvalidOperationException()
    {
        // Arrange
        var hasher = new ComparerHasher<string>(new NoAlternateComparer());

        // Act + Assert
        await Assert.That(() =>
        {
            using var map = new StackOnlyPooledHashMap<string, int, ComparerSwissAlgo>(0, new ComparerSwissAlgo(hasher, default));
            _ = map.GetAlternateLookup<ReadOnlySpan<char>>();
        }).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task TryAdd_AlternateKeyAlreadyPresent_DoesNotCreateKey(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string Key = "answer";
        const int FirstValue = 42;
        const int SecondValue = 43;
        var comparer = new CountingSpanComparer();
        var hasher = new ComparerHasher<string>(comparer);

        // Act
        var (firstAdded, secondAdded, value) = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddSameSpanTwice(new ComparerSwissAlgo(hasher, default), Key, FirstValue, SecondValue),
            HashAlgorithmKind.RobinHood => AddSameSpanTwice(new ComparerRobinHoodAlgo(hasher, default), Key, FirstValue, SecondValue),
            HashAlgorithmKind.Linear => AddSameSpanTwice(new ComparerLinearAlgo(hasher, default), Key, FirstValue, SecondValue),
            HashAlgorithmKind.Chained => AddSameSpanTwice(new ComparerChainedAlgo(hasher, default), Key, FirstValue, SecondValue),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(firstAdded).IsTrue();
        await Assert.That(secondAdded).IsFalse();
        await Assert.That(value).IsEqualTo(FirstValue);
        await Assert.That(comparer.CreateCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_UsingVar_WritesReachTheMapAndBuffersReturnOnce(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string SpanKey = "span";
        const string Utf8Key = "utf8";
        const string CopiedViewKey = "copy";
        const int Value = 1;

        // Act
        var (count, keysFound, returns) = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThroughLookupsOnUsingVar<StringSwissAlgo>(SpanKey, Utf8Key, CopiedViewKey, Value),
            HashAlgorithmKind.RobinHood => AddThroughLookupsOnUsingVar<StringRobinHoodAlgo>(SpanKey, Utf8Key, CopiedViewKey, Value),
            HashAlgorithmKind.Linear => AddThroughLookupsOnUsingVar<StringLinearAlgo>(SpanKey, Utf8Key, CopiedViewKey, Value),
            HashAlgorithmKind.Chained => AddThroughLookupsOnUsingVar<StringChainedAlgo>(SpanKey, Utf8Key, CopiedViewKey, Value),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert — a lookup over a copy of the map would leave Count at 0 and the original would return nothing.
        await Assert.That(count).IsEqualTo(3);
        await Assert.That(keysFound).IsEqualTo(3);
        await Assert.That(returns).IsEqualTo(1);
    }

    [Test]
    public async Task AlternateLookup_IndexerMissingKey_ThrowsKeyNotFoundException()
    {
        // Arrange
        const string PresentKey = "present";
        const string MissingKey = "missing";
        const int Value = 9;
        var missingUtf8 = Encoding.UTF8.GetBytes(MissingKey);

        // Act + Assert
        await Assert.That(() =>
        {
            using var map = new StackOnlyPooledHashMap<string, int, StringSwissAlgo>(0);
            map.Add(PresentKey, Value);
            _ = map.GetAlternateLookup<ReadOnlySpan<char>>()[MissingKey.AsSpan()];
        }).ThrowsExactly<KeyNotFoundException>();
        await Assert.That(() =>
        {
            using var map = new StackOnlyPooledHashMap<string, int, StringSwissAlgo>(0);
            map.Add(PresentKey, Value);
            _ = map.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>()[missingUtf8];
        }).ThrowsExactly<KeyNotFoundException>();
    }

    [Test]
    public async Task DefaultMap_GetAlternateLookup_FindsAndAddsBySpanAndUtf8()
    {
        // Arrange
        const string StoredKey = "apple";
        const string SpanKey = "pear";
        const string Utf8Key = "plum";
        const int StoredValue = 1;
        const int SpanValue = 2;
        const int Utf8Value = 3;
        var storedUtf8 = Encoding.UTF8.GetBytes(StoredKey);
        var utf8Key = Encoding.UTF8.GetBytes(Utf8Key);
        bool supported, foundBySpan, foundByUtf8, addedBySpan, addedByUtf8;
        int valueBySpan, valueByUtf8, count, spanValue, utf8Value;
        {
            using var map = new StackOnlyPooledHashMap<string, int>(0);
            map.Add(StoredKey, StoredValue);

            // Act
            supported = map.TryGetAlternateLookup<ReadOnlySpan<char>>(out var bySpan);
            var byUtf8 = map.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>();
            foundBySpan = bySpan.TryGetValue(StoredKey.AsSpan(), out valueBySpan);
            foundByUtf8 = byUtf8.TryGetValue(storedUtf8, out valueByUtf8);
            addedBySpan = map.GetAlternateLookup<ReadOnlySpan<char>>().TryAdd(SpanKey.AsSpan(), SpanValue);
            addedByUtf8 = byUtf8.TryAdd(utf8Key, Utf8Value);
            count = map.Count;
            spanValue = map[SpanKey];
            utf8Value = map[Utf8Key];
        }

        // Assert
        await Assert.That(supported).IsTrue();
        await Assert.That(foundBySpan).IsTrue();
        await Assert.That(valueBySpan).IsEqualTo(StoredValue);
        await Assert.That(foundByUtf8).IsTrue();
        await Assert.That(valueByUtf8).IsEqualTo(StoredValue);
        await Assert.That(addedBySpan).IsTrue();
        await Assert.That(addedByUtf8).IsTrue();
        await Assert.That(count).IsEqualTo(3);
        await Assert.That(spanValue).IsEqualTo(SpanValue);
        await Assert.That(utf8Value).IsEqualTo(Utf8Value);
    }

    [Test]
    public async Task DefaultMap_ComparerFromConstructor_AlternateLookupUsesIt()
    {
        // Arrange
        const string StoredKey = "Hello";
        const string LookupKey = "hELLO";
        const int Value = 5;
        bool found, addedDuplicate, removed;
        int foundValue, countAfterRemove;
        string? actualKey;
        {
            using var map = new StackOnlyPooledHashMap<string, int>(8, StringComparer.OrdinalIgnoreCase);
            map.Add(StoredKey, Value);

            // Act
            var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
            found = lookup.TryGetValue(LookupKey.AsSpan(), out actualKey, out foundValue);
            addedDuplicate = lookup.TryAdd(LookupKey.AsSpan(), Value + 1);
            removed = lookup.Remove(LookupKey.AsSpan());
            countAfterRemove = map.Count;
        }

        // Assert
        await Assert.That(found).IsTrue();
        await Assert.That(actualKey).IsEqualTo(StoredKey);
        await Assert.That(foundValue).IsEqualTo(Value);
        await Assert.That(addedDuplicate).IsFalse();
        await Assert.That(removed).IsTrue();
        await Assert.That(countAfterRemove).IsEqualTo(0);
    }

    [Test]
    public async Task DefaultMap_ComparerWithoutSpanSupport_TryGetAlternateLookupReturnsFalse()
    {
        // Arrange
        bool supported;
        {
            using var map = new StackOnlyPooledHashMap<string, int>(8, new NoAlternateComparer());

            // Act
            supported = map.TryGetAlternateLookup<ReadOnlySpan<char>>(out _);
        }

        // Assert
        await Assert.That(supported).IsFalse();
    }

    [Test]
    public async Task DefaultMap_TryGetAlternateLookup_UnsupportedAlternate_ReturnsFalse()
    {
        // Arrange
        bool supported;
        {
            using var map = new StackOnlyPooledHashMap<int, int>(0);

            // Act
            supported = map.TryGetAlternateLookup<long>(out _);
        }

        // Assert
        await Assert.That(supported).IsFalse();
    }

    [Test]
    public async Task DefaultMap_GetAlternateLookup_UnsupportedAlternate_ThrowsInvalidOperationException()
    {
        // Act + Assert
        await Assert.That(() =>
        {
            using var map = new StackOnlyPooledHashMap<int, int>(0);
            _ = map.GetAlternateLookup<long>();
        }).ThrowsExactly<InvalidOperationException>();
    }

    // ---- helpers ----

    static string[] StringKeys(string prefix, int count) =>
        Enumerable.Range(0, count).Select(i => prefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    readonly record struct IgnoreCaseResult(
        bool Contains,
        bool Found,
        int FoundValue,
        string? ActualKey,
        int Indexed,
        bool AddedDuplicate,
        bool AddedNew,
        int CountAfterAdd,
        int NewValueByKey,
        bool Removed,
        string? RemovedKey,
        int RemovedValue,
        bool RemovedNew,
        int CountAfterRemove);

    // Stores storedKey, then goes through every lookup member with keys that differ only in casing.
    static IgnoreCaseResult UseIgnoreCaseLookup<TAlgo>(
        TAlgo algorithm, string storedKey, string lookupKey, string newKey, string newKeyOtherCase, int value, int newValue)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        using var map = new StackOnlyPooledHashMap<string, int, TAlgo>(0, algorithm);
        map.Add(storedKey, value);
        var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();

        var contains = lookup.ContainsKey(lookupKey.AsSpan());
        var found = lookup.TryGetValue(lookupKey.AsSpan(), out var foundValue);
        lookup.TryGetValue(lookupKey.AsSpan(), out var actualKey, out _);
        var indexed = lookup[lookupKey.AsSpan()];
        var addedDuplicate = lookup.TryAdd(lookupKey.AsSpan(), value + 1);
        var addedNew = lookup.TryAdd(newKey.AsSpan(), newValue);
        var countAfterAdd = map.Count;
        var newValueByKey = map[newKeyOtherCase];
        var removed = lookup.Remove(lookupKey.AsSpan(), out var removedKey, out var removedValue);
        var removedNew = lookup.Remove(newKeyOtherCase.AsSpan());
        return new IgnoreCaseResult(
            contains, found, foundValue, actualKey, indexed, addedDuplicate, addedNew, countAfterAdd, newValueByKey,
            removed, removedKey, removedValue, removedNew, map.Count);
    }

    readonly record struct SpanRoundTrip(
        int CapacityBefore,
        bool ContainedBeforeAdd,
        bool RemovedBeforeAdd,
        int Added,
        int CapacityAfterAdd,
        string[] RemovedKeys,
        int[] RemovedValues,
        int Count,
        int[] ValuesBySpan,
        int[] ValuesByKey,
        string? StoredKey);

    // Starts from a map with no buffers, so the first TryAdd through the lookup rents them and the
    // later ones grow the table. Then removes the even keys through the lookup (tombstones, backward
    // shift, swap-with-last) and reads every key back both ways.
    static SpanRoundTrip AddThenRemoveEvenKeysBySpan<TAlgo>(string[] keys, int valueFactor)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        using var map = new StackOnlyPooledHashMap<string, int, TAlgo>(0);
        var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
        var capacityBefore = map.Capacity;
        var containedBeforeAdd = lookup.ContainsKey(keys[0].AsSpan());
        var removedBeforeAdd = lookup.Remove(keys[0].AsSpan());

        var added = 0;
        for (var i = 0; i < keys.Length; i++)
        {
            if (lookup.TryAdd(keys[i].AsSpan(), i * valueFactor))
            {
                added++;
            }
        }
        var capacityAfterAdd = map.Capacity;

        var removedKeys = new List<string>();
        var removedValues = new List<int>();
        for (var i = 0; i < keys.Length; i += 2)
        {
            if (lookup.Remove(keys[i].AsSpan(), out var removedKey, out var removedValue))
            {
                removedKeys.Add(removedKey);
                removedValues.Add(removedValue);
            }
        }

        var valuesBySpan = new int[keys.Length];
        var valuesByKey = new int[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            valuesBySpan[i] = lookup.TryGetValue(keys[i].AsSpan(), out var spanValue) ? spanValue : MissingValue;
            valuesByKey[i] = map.TryGetValue(keys[i], out var keyValue) ? keyValue : MissingValue;
        }

        lookup.TryGetValue(keys[1].AsSpan(), out var storedKey, out _);
        return new SpanRoundTrip(
            capacityBefore, containedBeforeAdd, removedBeforeAdd, added, capacityAfterAdd,
            [.. removedKeys], [.. removedValues], map.Count, valuesBySpan, valuesByKey, storedKey);
    }

    readonly record struct MemoryKeyResult(
        bool Found, int FoundValue, bool AddedNew, bool AddedDuplicate, int NewValueByKey, bool Removed, int Count, bool ContainsRemoved);

    // Stores one address as a ReadOnlyMemory<byte> key, then finds, adds and removes addresses through
    // spans over other arrays holding the same bytes.
    static MemoryKeyResult UseMemoryKeysBySpan<TAlgo>(TAlgo algorithm, string storedAddress, string newAddress, int storedValue, int newValue)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<ReadOnlyMemory<byte>, int>, ReadOnlyMemory<byte>>
    {
        ReadOnlyMemory<byte> stored = Encoding.UTF8.GetBytes(storedAddress);
        ReadOnlyMemory<byte> storedCopy = Encoding.UTF8.GetBytes(storedAddress);
        ReadOnlyMemory<byte> added = Encoding.UTF8.GetBytes(newAddress);
        ReadOnlyMemory<byte> addedCopy = Encoding.UTF8.GetBytes(newAddress);

        using var map = new StackOnlyPooledHashMap<ReadOnlyMemory<byte>, int, TAlgo>(0, algorithm);
        map.Add(stored, storedValue);
        var lookup = map.GetAlternateLookup<ReadOnlySpan<byte>>();

        var found = lookup.TryGetValue(storedCopy.Span, out var foundValue);
        var addedNew = lookup.TryAdd(added.Span, newValue);
        var addedDuplicate = lookup.TryAdd(addedCopy.Span, newValue + 1);
        var newValueByKey = map[addedCopy];
        var removed = lookup.Remove(storedCopy.Span);
        return new MemoryKeyResult(found, foundValue, addedNew, addedDuplicate, newValueByKey, removed, map.Count, map.ContainsKey(stored));
    }

    readonly record struct Utf8RoundTrip(
        int Added,
        bool AddedDuplicate,
        bool Contains,
        int Indexed,
        string? ActualKey,
        string? RemovedKey,
        int Removed,
        int Count,
        int[] ValuesByBytes,
        int[] ValuesByKey);

    static Utf8RoundTrip AddThenRemoveEvenKeysByUtf8<TAlgo>(string[] keys, int valueFactor)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        var utf8Keys = keys.Select(Encoding.UTF8.GetBytes).ToArray();
        using var map = new StackOnlyPooledHashMap<string, int, TAlgo>(0);
        var lookup = map.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>();

        var added = 0;
        for (var i = 0; i < utf8Keys.Length; i++)
        {
            if (lookup.TryAdd(utf8Keys[i], i * valueFactor))
            {
                added++;
            }
        }
        var addedDuplicate = lookup.TryAdd(utf8Keys[0], MissingValue);
        var contains = lookup.ContainsKey(utf8Keys[1]);
        var indexed = lookup[utf8Keys[1]];
        lookup.TryGetValue(utf8Keys[1], out var actualKey, out _);

        lookup.Remove(utf8Keys[0], out var removedKey, out _);
        var removed = 1;
        for (var i = 2; i < utf8Keys.Length; i += 2)
        {
            if (lookup.Remove(utf8Keys[i]))
            {
                removed++;
            }
        }

        var valuesByBytes = new int[keys.Length];
        var valuesByKey = new int[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            valuesByBytes[i] = lookup.TryGetValue(utf8Keys[i], out var bytesValue) ? bytesValue : MissingValue;
            valuesByKey[i] = map.TryGetValue(keys[i], out var keyValue) ? keyValue : MissingValue;
        }
        return new Utf8RoundTrip(added, addedDuplicate, contains, indexed, actualKey, removedKey, removed, map.Count, valuesByBytes, valuesByKey);
    }

    static bool SupportsSpanLookup<TAlgo>(TAlgo algorithm)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        using var map = new StackOnlyPooledHashMap<string, int, TAlgo>(0, algorithm);
        return map.TryGetAlternateLookup<ReadOnlySpan<char>>(out _);
    }

    static (bool FirstAdded, bool SecondAdded, int Value) AddSameSpanTwice<TAlgo>(TAlgo algorithm, string key, int firstValue, int secondValue)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        using var map = new StackOnlyPooledHashMap<string, int, TAlgo>(0, algorithm);
        var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
        var firstAdded = lookup.TryAdd(key.AsSpan(), firstValue);
        var secondAdded = lookup.TryAdd(key.AsSpan(), secondValue);
        return (firstAdded, secondAdded, map[key]);
    }

    // Adds one key through each kind of lookup on a `using var` map that starts with no buffers, the
    // third through a copy of the view handed to a helper by value.
    static (int Count, int KeysFound, int Returns) AddThroughLookupsOnUsingVar<TInner>(string spanKey, string utf8Key, string copiedViewKey, int value)
        where TInner : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        var log = new BufferReturnLog();
        int count, keysFound;
        {
            using var map = new StackOnlyPooledHashMap<string, int, ReturnCountingAlgorithm<KeyValueSlot<string, int>, string, TInner>>(0, new(log));
            var bySpan = map.GetAlternateLookup<ReadOnlySpan<char>>();
            bySpan.TryAdd(spanKey.AsSpan(), value);
            map.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>().TryAdd(Encoding.UTF8.GetBytes(utf8Key), value);
            AddThroughView(bySpan, copiedViewKey, value);
            count = map.Count;
            keysFound = (map.ContainsKey(spanKey) ? 1 : 0) + (map.ContainsKey(utf8Key) ? 1 : 0) + (map.ContainsKey(copiedViewKey) ? 1 : 0);
        }
        return (count, keysFound, log.Returns);
    }

    static void AddThroughView<TAlgo>(StackOnlyPooledHashMap<string, int, TAlgo>.AlternateLookup<ReadOnlySpan<char>> lookup, string key, int value)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
        => lookup.TryAdd(key.AsSpan(), value);
}
