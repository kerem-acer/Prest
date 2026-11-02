using System.Runtime.CompilerServices;
using System.Text;
using TUnit.Assertions.Enums;

namespace Prest.Tests;

public partial class PooledHashMapTests
{
    const int NotFound = -1;

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_AddAndRemoveBySpanAcrossGrowth_FindsRemainingEntries(HashAlgorithmKind algorithm)
    {
        // Arrange
        var keys = StringKeys("key-", EntryCount);
        var expectedValues = Enumerable.Range(0, EntryCount).Select(i => i % 2 == 1 ? i * ValueFactor : NotFound).ToArray();
        var expectedRemovedValues = Enumerable.Range(0, EntryCount).Where(i => i % 2 == 0).Select(i => i * ValueFactor).ToArray();
        var expectedRemovedKeys = keys.Where((_, i) => i % 2 == 0).ToArray();

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThenRemoveEvenKeysBySpan(() => SwissHashMap<string, int>.Create(4), keys),
            HashAlgorithmKind.RobinHood => AddThenRemoveEvenKeysBySpan(() => RobinHoodHashMap<string, int>.Create(4), keys),
            HashAlgorithmKind.Linear => AddThenRemoveEvenKeysBySpan(() => LinearHashMap<string, int>.Create(4), keys),
            HashAlgorithmKind.Chained => AddThenRemoveEvenKeysBySpan(() => ChainedHashMap<string, int>.Create(4), keys),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.Added).IsEqualTo(EntryCount);
        await Assert.That(result.Grew).IsTrue();
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
    public async Task AlternateLookup_IgnoreCaseComparer_MatchesAnyCasing(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string StoredKey = "Hello";
        const string LookupKey = "hELLO";
        const int Value = 5;
        var comparer = StringComparer.OrdinalIgnoreCase;

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => LookUpWithOtherCasing(ComparerSwissHashMap<string, int>.Create(comparer), StoredKey, LookupKey, Value),
            HashAlgorithmKind.RobinHood => LookUpWithOtherCasing(ComparerRobinHoodHashMap<string, int>.Create(comparer), StoredKey, LookupKey, Value),
            HashAlgorithmKind.Linear => LookUpWithOtherCasing(ComparerLinearHashMap<string, int>.Create(comparer), StoredKey, LookupKey, Value),
            HashAlgorithmKind.Chained => LookUpWithOtherCasing(ComparerChainedHashMap<string, int>.Create(comparer), StoredKey, LookupKey, Value),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.Found).IsTrue();
        await Assert.That(result.Value).IsEqualTo(Value);
        await Assert.That(result.ActualKey).IsEqualTo(StoredKey);
        await Assert.That(result.AddedDuplicate).IsFalse();
        await Assert.That(result.Removed).IsTrue();
        await Assert.That(result.CountAfterRemove).IsEqualTo(0);
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

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddSameSpanTwice(ComparerSwissHashMap<string, int>.Create(comparer), Key, FirstValue, SecondValue),
            HashAlgorithmKind.RobinHood => AddSameSpanTwice(ComparerRobinHoodHashMap<string, int>.Create(comparer), Key, FirstValue, SecondValue),
            HashAlgorithmKind.Linear => AddSameSpanTwice(ComparerLinearHashMap<string, int>.Create(comparer), Key, FirstValue, SecondValue),
            HashAlgorithmKind.Chained => AddSameSpanTwice(ComparerChainedHashMap<string, int>.Create(comparer), Key, FirstValue, SecondValue),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.FirstAdded).IsTrue();
        await Assert.That(result.SecondAdded).IsFalse();
        await Assert.That(result.Value).IsEqualTo(FirstValue);
        await Assert.That(comparer.CreateCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task TryGetAlternateLookup_ComparerWithoutSpanSupport_ReturnsFalse(HashAlgorithmKind algorithm)
    {
        // Arrange
        var comparer = new NoAlternateComparer();

        // Act
        var supported = algorithm switch
        {
            HashAlgorithmKind.Swiss => SupportsSpanLookup(ComparerSwissHashMap<string, int>.Create(comparer)),
            HashAlgorithmKind.RobinHood => SupportsSpanLookup(ComparerRobinHoodHashMap<string, int>.Create(comparer)),
            HashAlgorithmKind.Linear => SupportsSpanLookup(ComparerLinearHashMap<string, int>.Create(comparer)),
            HashAlgorithmKind.Chained => SupportsSpanLookup(ComparerChainedHashMap<string, int>.Create(comparer)),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(supported).IsFalse();
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_Operations_DoNotAllocate(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string PresentKey = "present";
        const string MissingKey = "missing";

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => MeasureAlternateAllocations(SwissHashMap<string, int>.Create(), PresentKey, MissingKey),
            HashAlgorithmKind.RobinHood => MeasureAlternateAllocations(RobinHoodHashMap<string, int>.Create(), PresentKey, MissingKey),
            HashAlgorithmKind.Linear => MeasureAlternateAllocations(LinearHashMap<string, int>.Create(), PresentKey, MissingKey),
            HashAlgorithmKind.Chained => MeasureAlternateAllocations(ChainedHashMap<string, int>.Create(), PresentKey, MissingKey),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.ComparerRouteBytes).IsEqualTo(0);
        await Assert.That(result.HasherRouteBytes).IsEqualTo(0);
        await Assert.That(result.Succeeded).IsEqualTo(AlternateOperationCount * 2);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_Utf8StructHasher_AddsFindsAndRemovesByBytes(HashAlgorithmKind algorithm)
    {
        // Arrange
        var keys = StringKeys("clé-日本-", EntryCount);
        var expectedValues = Enumerable.Range(0, EntryCount).Select(i => i % 2 == 1 ? i * ValueFactor : NotFound).ToArray();

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThenRemoveEvenKeysByUtf8(SwissHashMap<string, int>.Create(4), keys),
            HashAlgorithmKind.RobinHood => AddThenRemoveEvenKeysByUtf8(RobinHoodHashMap<string, int>.Create(4), keys),
            HashAlgorithmKind.Linear => AddThenRemoveEvenKeysByUtf8(LinearHashMap<string, int>.Create(4), keys),
            HashAlgorithmKind.Chained => AddThenRemoveEvenKeysByUtf8(ChainedHashMap<string, int>.Create(4), keys),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.Added).IsEqualTo(EntryCount);
        await Assert.That(result.AddedDuplicate).IsFalse();
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
    public async Task AlternateLookup_MapWithoutBuffers_MissesThenTryAddAllocatesBuffers(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string Key = "first";
        const int Value = 1;

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => UseUnallocatedMapBySpan(() => SwissHashMap<string, int>.Create(0), Key, Value),
            HashAlgorithmKind.RobinHood => UseUnallocatedMapBySpan(() => RobinHoodHashMap<string, int>.Create(0), Key, Value),
            HashAlgorithmKind.Linear => UseUnallocatedMapBySpan(() => LinearHashMap<string, int>.Create(0), Key, Value),
            HashAlgorithmKind.Chained => UseUnallocatedMapBySpan(() => ChainedHashMap<string, int>.Create(0), Key, Value),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.ContainedBeforeAdd).IsFalse();
        await Assert.That(result.FoundBeforeAdd).IsFalse();
        await Assert.That(result.RemovedBeforeAdd).IsFalse();
        await Assert.That(result.Added).IsTrue();
        await Assert.That(result.ValueAfterAdd).IsEqualTo(Value);
    }

    [Test]
    public async Task GetAlternateLookup_ComparerWithoutSpanSupport_ThrowsInvalidOperationException()
    {
        // Arrange
        using var map = ComparerSwissHashMap<string, int>.Create(new NoAlternateComparer());

        // Act & Assert
        await Assert.That(() => _ = map.GetAlternateLookup<ReadOnlySpan<char>>()).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task TryGetAlternateLookup_DefaultHasherWithUnsupportedAlternate_ReturnsFalse()
    {
        // Arrange
        using var map = PooledHashMap<int, int>.Create();

        // Act
        var supported = map.TryGetAlternateLookup<long>(out _);

        // Assert
        await Assert.That(supported).IsFalse();
    }

    [Test]
    public async Task GetAlternateLookup_CustomStructHasher_SupportsOnlyTheStructHasherRoute()
    {
        // Arrange
        const string Key = "custom";
        const int Value = 3;
        using var map = PooledHashMap<string, int, SwissTableAlgorithm<KeyValueSlot<string, int>, string, OrdinalStringHasher, NoOpHashFinalizer>>.Create();
        map.Add(Key, Value);

        // Act
        var comparerRouteSupported = map.TryGetAlternateLookup<ReadOnlySpan<char>>(out _);
        var foundByUtf8 = map.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>().TryGetValue(Encoding.UTF8.GetBytes(Key), out var value);

        // Assert
        await Assert.That(comparerRouteSupported).IsFalse();
        await Assert.That(foundByUtf8).IsTrue();
        await Assert.That(value).IsEqualTo(Value);
    }

    [Test]
    public async Task AlternateLookup_Indexer_ReturnsValueOrThrowsKeyNotFound()
    {
        // Arrange
        const string Key = "present";
        const string MissingKey = "missing";
        const int Value = 9;
        using var map = PooledHashMap<string, int>.Create();
        map.Add(Key, Value);
        var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
        var utf8Lookup = map.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>();

        // Act
        var value = lookup[Key.AsSpan()];
        var utf8Value = utf8Lookup[Encoding.UTF8.GetBytes(Key)];

        // Assert
        await Assert.That(value).IsEqualTo(Value);
        await Assert.That(utf8Value).IsEqualTo(Value);
        await Assert.That(lookup.Map).IsSameReferenceAs(map);
        await Assert.That(utf8Lookup.Map).IsSameReferenceAs(map);
        await Assert.That(() => _ = lookup[MissingKey.AsSpan()]).ThrowsExactly<KeyNotFoundException>();
        await Assert.That(() => _ = utf8Lookup[Encoding.UTF8.GetBytes(MissingKey)]).ThrowsExactly<KeyNotFoundException>();
    }

    [Test]
    public async Task Remove_AlternateKeyMissing_ReturnsFalseAndDefaults()
    {
        // Arrange
        using var map = PooledHashMap<string, int>.Create();
        map.Add("present", 1);
        var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();

        // Act
        var removed = lookup.Remove("missing".AsSpan(), out var actualKey, out var value);
        var found = lookup.TryGetValue("missing".AsSpan(), out var foundKey, out var foundValue);

        // Assert
        await Assert.That(removed).IsFalse();
        await Assert.That(actualKey).IsNull();
        await Assert.That(value).IsEqualTo(0);
        await Assert.That(found).IsFalse();
        await Assert.That(foundKey).IsNull();
        await Assert.That(foundValue).IsEqualTo(0);
    }

    // ---- helpers ----

    // Successful operations per ExerciseBySpan / ExerciseByUtf8 call.
    const int AlternateOperationCount = 8;

    static string[] StringKeys(string prefix, int count) =>
        Enumerable.Range(0, count).Select(i => prefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    readonly record struct SpanRoundTrip(
        int Added,
        bool Grew,
        string[] RemovedKeys,
        int[] RemovedValues,
        int Count,
        int[] ValuesBySpan,
        int[] ValuesByKey,
        string? StoredKey);

    // Adds every key through the span lookup into a fresh small map (forcing growth after
    // alternate adds), then removes the even keys through the span lookup (exercising
    // tombstones, backward shift and swap-with-last), and reads every key back both ways.
    static SpanRoundTrip AddThenRemoveEvenKeysBySpan<TAlgo>(Func<PooledHashMap<string, int, TAlgo>> create, string[] keys)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        // The first create() takes whatever the per-thread cache holds, so the second one
        // builds a fresh, small map.
        _ = create();
        using var map = create();
        var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
        var initialCapacity = map.Capacity;

        var added = 0;
        for (var i = 0; i < keys.Length; i++)
        {
            if (lookup.TryAdd(keys[i].AsSpan(), i * ValueFactor))
            {
                added++;
            }
        }
        var grew = map.Capacity > initialCapacity;

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
            valuesBySpan[i] = lookup.TryGetValue(keys[i].AsSpan(), out var spanValue) ? spanValue : NotFound;
            valuesByKey[i] = map.TryGetValue(keys[i], out var keyValue) ? keyValue : NotFound;
        }

        lookup.TryGetValue(keys[1].AsSpan(), out var storedKey, out _);
        return new SpanRoundTrip(added, grew, [.. removedKeys], [.. removedValues], map.Count, valuesBySpan, valuesByKey, storedKey);
    }

    readonly record struct CasingResult(bool Found, int Value, string? ActualKey, bool AddedDuplicate, bool Removed, int CountAfterRemove);

    static CasingResult LookUpWithOtherCasing<TAlgo>(PooledHashMap<string, int, TAlgo> map, string storedKey, string lookupKey, int value)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        using (map)
        {
            map.Add(storedKey, value);
            var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
            var found = lookup.TryGetValue(lookupKey.AsSpan(), out var actualKey, out var foundValue);
            var addedDuplicate = lookup.TryAdd(lookupKey.AsSpan(), value + 1);
            var removed = lookup.Remove(lookupKey.AsSpan());
            return new CasingResult(found, foundValue, actualKey, addedDuplicate, removed, map.Count);
        }
    }

    readonly record struct DuplicateAddResult(bool FirstAdded, bool SecondAdded, int Value);

    static DuplicateAddResult AddSameSpanTwice<TAlgo>(PooledHashMap<string, int, TAlgo> map, string key, int firstValue, int secondValue)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        using (map)
        {
            var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
            var firstAdded = lookup.TryAdd(key.AsSpan(), firstValue);
            var secondAdded = lookup.TryAdd(key.AsSpan(), secondValue);
            return new DuplicateAddResult(firstAdded, secondAdded, map[key]);
        }
    }

    static bool SupportsSpanLookup<TAlgo>(PooledHashMap<string, int, TAlgo> map)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        using (map)
        {
            return map.TryGetAlternateLookup<ReadOnlySpan<char>>(out _);
        }
    }

    readonly record struct AllocationResult(long ComparerRouteBytes, long HasherRouteBytes, int Succeeded);

    static AllocationResult MeasureAlternateAllocations<TAlgo>(PooledHashMap<string, int, TAlgo> map, string presentKey, string missingKey)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        using (map)
        {
            map.Add(presentKey, 1);
            var presentUtf8 = Encoding.UTF8.GetBytes(presentKey);
            var missingUtf8 = Encoding.UTF8.GetBytes(missingKey);
            _ = ExerciseBySpan(map, presentKey, missingKey); // warm up so JIT work is not measured
            _ = ExerciseByUtf8(map, presentUtf8, missingUtf8);

            var before = GC.GetAllocatedBytesForCurrentThread();
            var succeeded = ExerciseBySpan(map, presentKey, missingKey);
            var comparerRouteBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            before = GC.GetAllocatedBytesForCurrentThread();
            succeeded += ExerciseByUtf8(map, presentUtf8, missingUtf8);
            var hasherRouteBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            return new AllocationResult(comparerRouteBytes, hasherRouteBytes, succeeded);
        }
    }

    // Every operation except creating a key for a new entry: lookups (hit and miss), a duplicate
    // TryAdd, and a Remove whose entry is put back under the same stored key.
    [MethodImpl(MethodImplOptions.NoInlining)]
    static int ExerciseBySpan<TAlgo>(PooledHashMap<string, int, TAlgo> map, string presentKey, string missingKey)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
        var succeeded = 0;
        succeeded += lookup.TryGetValue(presentKey.AsSpan(), out _) ? 1 : 0;
        succeeded += lookup.TryGetValue(presentKey.AsSpan(), out _, out _) ? 1 : 0;
        succeeded += lookup.ContainsKey(presentKey.AsSpan()) ? 1 : 0;
        succeeded += lookup[presentKey.AsSpan()];
        succeeded += lookup.TryAdd(presentKey.AsSpan(), 2) ? 0 : 1;
        succeeded += lookup.TryGetValue(missingKey.AsSpan(), out _) ? 0 : 1;
        succeeded += lookup.Remove(missingKey.AsSpan()) ? 0 : 1;
        if (lookup.Remove(presentKey.AsSpan(), out var key, out var value))
        {
            map.Add(key, value);
            succeeded++;
        }
        return succeeded;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int ExerciseByUtf8<TAlgo>(PooledHashMap<string, int, TAlgo> map, byte[] presentKey, byte[] missingKey)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        var lookup = map.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>();
        var succeeded = 0;
        succeeded += lookup.TryGetValue(presentKey, out _) ? 1 : 0;
        succeeded += lookup.TryGetValue(presentKey, out _, out _) ? 1 : 0;
        succeeded += lookup.ContainsKey(presentKey) ? 1 : 0;
        succeeded += lookup[presentKey];
        succeeded += lookup.TryAdd(presentKey, 2) ? 0 : 1;
        succeeded += lookup.TryGetValue(missingKey, out _) ? 0 : 1;
        succeeded += lookup.Remove(missingKey) ? 0 : 1;
        if (lookup.Remove(presentKey, out var key, out var value))
        {
            map.Add(key, value);
            succeeded++;
        }
        return succeeded;
    }

    readonly record struct Utf8RoundTrip(int Added, bool AddedDuplicate, int Removed, int Count, int[] ValuesByBytes, int[] ValuesByKey);

    static Utf8RoundTrip AddThenRemoveEvenKeysByUtf8<TAlgo>(PooledHashMap<string, int, TAlgo> map, string[] keys)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        using (map)
        {
            var utf8Keys = keys.Select(Encoding.UTF8.GetBytes).ToArray();
            var lookup = map.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>();

            var added = 0;
            for (var i = 0; i < utf8Keys.Length; i++)
            {
                if (lookup.TryAdd(utf8Keys[i], i * ValueFactor))
                {
                    added++;
                }
            }
            var addedDuplicate = lookup.TryAdd(utf8Keys[0], NotFound);

            var removed = 0;
            for (var i = 0; i < utf8Keys.Length; i += 2)
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
                valuesByBytes[i] = lookup.TryGetValue(utf8Keys[i], out var bytesValue) ? bytesValue : NotFound;
                valuesByKey[i] = map.TryGetValue(keys[i], out var keyValue) ? keyValue : NotFound;
            }
            return new Utf8RoundTrip(added, addedDuplicate, removed, map.Count, valuesByBytes, valuesByKey);
        }
    }

    readonly record struct UnallocatedResult(bool ContainedBeforeAdd, bool FoundBeforeAdd, bool RemovedBeforeAdd, bool Added, int ValueAfterAdd);

    static UnallocatedResult UseUnallocatedMapBySpan<TAlgo>(Func<PooledHashMap<string, int, TAlgo>> create, string key, int value)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        // The first create() takes whatever the per-thread cache holds, so the second one
        // builds a map with no buffers yet.
        _ = create();
        using var map = create();
        var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
        var contained = lookup.ContainsKey(key.AsSpan());
        var found = lookup.TryGetValue(key.AsSpan(), out _);
        var removed = lookup.Remove(key.AsSpan());
        var added = lookup.TryAdd(key.AsSpan(), value);
        return new UnallocatedResult(contained, found, removed, added, lookup[key.AsSpan()]);
    }
}
