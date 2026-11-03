using System.Runtime.CompilerServices;
using System.Text;

namespace Prest.Tests;

public class PooledCollectionsMarshalTests
{
    const int Key = 17;
    const int MissingKey = 99;
    const int StoredValue = 5;
    const int WrittenValue = 42;

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task GetValueRefOrNullRef_KeyPresent_WritesThroughToTheMap(HashAlgorithmKind algorithm)
    {
        // Arrange & Act: the helper builds a fresh map of that algorithm and goes through the ref API.
        var (readThroughRef, valueAfterWrite) = algorithm switch
        {
            HashAlgorithmKind.Swiss => ReadAndWriteThroughRef(SwissHashMap<int, int>.Create()),
            HashAlgorithmKind.RobinHood => ReadAndWriteThroughRef(RobinHoodHashMap<int, int>.Create()),
            HashAlgorithmKind.Linear => ReadAndWriteThroughRef(LinearHashMap<int, int>.Create()),
            HashAlgorithmKind.Chained => ReadAndWriteThroughRef(ChainedHashMap<int, int>.Create()),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(readThroughRef).IsEqualTo(StoredValue);
        await Assert.That(valueAfterWrite).IsEqualTo(WrittenValue);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task GetValueRefOrNullRef_KeyMissing_ReturnsNullRef(HashAlgorithmKind algorithm)
    {
        // Arrange & Act: the helper builds a fresh map of that algorithm and goes through the ref API.
        var (nullOnEmptyMap, nullOnMiss) = algorithm switch
        {
            HashAlgorithmKind.Swiss => LookUpMissingKey(SwissHashMap<int, int>.Create()),
            HashAlgorithmKind.RobinHood => LookUpMissingKey(RobinHoodHashMap<int, int>.Create()),
            HashAlgorithmKind.Linear => LookUpMissingKey(LinearHashMap<int, int>.Create()),
            HashAlgorithmKind.Chained => LookUpMissingKey(ChainedHashMap<int, int>.Create()),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(nullOnEmptyMap).IsTrue();
        await Assert.That(nullOnMiss).IsTrue();
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task GetValueRefOrAddDefault_KeyMissing_AddsDefaultEntry(HashAlgorithmKind algorithm)
    {
        // Arrange & Act: the helper builds a fresh map of that algorithm and goes through the ref API.
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThroughRef(SwissHashMap<int, int>.Create()),
            HashAlgorithmKind.RobinHood => AddThroughRef(RobinHoodHashMap<int, int>.Create()),
            HashAlgorithmKind.Linear => AddThroughRef(LinearHashMap<int, int>.Create()),
            HashAlgorithmKind.Chained => AddThroughRef(ChainedHashMap<int, int>.Create()),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.Exists).IsFalse();
        await Assert.That(result.InitialValue).IsEqualTo(0);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.StoredValue).IsEqualTo(WrittenValue);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task GetValueRefOrAddDefault_KeyPresent_ReturnsExistingValue(HashAlgorithmKind algorithm)
    {
        // Arrange & Act: the helper builds a fresh map of that algorithm and goes through the ref API.
        var (exists, valueThroughRef, count) = algorithm switch
        {
            HashAlgorithmKind.Swiss => GetExistingThroughAddDefault(SwissHashMap<int, int>.Create()),
            HashAlgorithmKind.RobinHood => GetExistingThroughAddDefault(RobinHoodHashMap<int, int>.Create()),
            HashAlgorithmKind.Linear => GetExistingThroughAddDefault(LinearHashMap<int, int>.Create()),
            HashAlgorithmKind.Chained => GetExistingThroughAddDefault(ChainedHashMap<int, int>.Create()),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(exists).IsTrue();
        await Assert.That(valueThroughRef).IsEqualTo(StoredValue);
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task GetValueRefOrAddDefault_CountOccurrencesAcrossGrowth_CountsEveryKey(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int KeyCount = 2000;
        const int Repeats = 3;

        // Act
        var (count, counts, grew) = algorithm switch
        {
            HashAlgorithmKind.Swiss => CountOccurrences(SwissHashMap<int, int>.Create(4), KeyCount, Repeats),
            HashAlgorithmKind.RobinHood => CountOccurrences(RobinHoodHashMap<int, int>.Create(4), KeyCount, Repeats),
            HashAlgorithmKind.Linear => CountOccurrences(LinearHashMap<int, int>.Create(4), KeyCount, Repeats),
            HashAlgorithmKind.Chained => CountOccurrences(ChainedHashMap<int, int>.Create(4), KeyCount, Repeats),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(grew).IsTrue();
        await Assert.That(count).IsEqualTo(KeyCount);
        await Assert.That(counts.All(c => c == Repeats)).IsTrue();
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task GetValueRefOrAddDefault_ComparerAlternateLookup_CreatesKeyOnlyWhenAdding(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string Word = "token";
        var comparer = new CountingSpanComparer();

        // Act
        var (firstExists, secondExists, countByKey, nullForMissingSpan, valueBySpan) = algorithm switch
        {
            HashAlgorithmKind.Swiss => CountWordTwiceBySpan(ComparerSwissHashMap<string, int>.Create(comparer), Word),
            HashAlgorithmKind.RobinHood => CountWordTwiceBySpan(ComparerRobinHoodHashMap<string, int>.Create(comparer), Word),
            HashAlgorithmKind.Linear => CountWordTwiceBySpan(ComparerLinearHashMap<string, int>.Create(comparer), Word),
            HashAlgorithmKind.Chained => CountWordTwiceBySpan(ComparerChainedHashMap<string, int>.Create(comparer), Word),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(firstExists).IsFalse();
        await Assert.That(secondExists).IsTrue();
        await Assert.That(comparer.CreateCount).IsEqualTo(1);
        await Assert.That(countByKey).IsEqualTo(2);
        await Assert.That(nullForMissingSpan).IsTrue();
        await Assert.That(valueBySpan).IsEqualTo(2);
    }

    [Test]
    public async Task GetValueRefOrAddDefault_StructHasherAlternateLookup_FindsAndAddsByUtf8()
    {
        // Arrange
        const string Existing = "alpha";
        const string Added = "beta";
        const int ExistingValue = 1;
        using var map = SwissHashMap<string, int, OrdinalStringHasher, NoOpHashFinalizer>.Create();
        map.Add(Existing, ExistingValue);
        var existingUtf8 = Encoding.UTF8.GetBytes(Existing);
        var addedUtf8 = Encoding.UTF8.GetBytes(Added);
        var lookup = map.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>();

        // Act
        ref var existingRef = ref PooledCollectionsMarshal.GetValueRefOrNullRef(lookup, existingUtf8);
        existingRef += WrittenValue;
        var nullForAdded = Unsafe.IsNullRef(ref PooledCollectionsMarshal.GetValueRefOrNullRef(lookup, addedUtf8));
        PooledCollectionsMarshal.GetValueRefOrAddDefault(lookup, addedUtf8, out var addedExisted) = WrittenValue;
        var existingValue = map[Existing];
        var addedValue = map[Added];

        // Assert
        await Assert.That(nullForAdded).IsTrue();
        await Assert.That(addedExisted).IsFalse();
        await Assert.That(existingValue).IsEqualTo(ExistingValue + WrittenValue);
        await Assert.That(addedValue).IsEqualTo(WrittenValue);
        await Assert.That(map.Count).IsEqualTo(2);
    }

    static (int ReadThroughRef, int ValueAfterWrite) ReadAndWriteThroughRef<TAlgo>(PooledHashMap<int, int, TAlgo> map)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        using (map)
        {
            map.Add(Key, StoredValue);
            ref var value = ref PooledCollectionsMarshal.GetValueRefOrNullRef(map, Key);
            var read = value;
            value = WrittenValue;
            return (read, map[Key]);
        }
    }

    static (bool NullOnEmptyMap, bool NullOnMiss) LookUpMissingKey<TAlgo>(PooledHashMap<int, int, TAlgo> map)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        using (map)
        {
            var nullOnEmptyMap = Unsafe.IsNullRef(ref PooledCollectionsMarshal.GetValueRefOrNullRef(map, MissingKey));
            map.Add(Key, StoredValue);
            var nullOnMiss = Unsafe.IsNullRef(ref PooledCollectionsMarshal.GetValueRefOrNullRef(map, MissingKey));
            return (nullOnEmptyMap, nullOnMiss);
        }
    }

    static (bool Exists, int InitialValue, int Count, int StoredValue) AddThroughRef<TAlgo>(PooledHashMap<int, int, TAlgo> map)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        using (map)
        {
            ref var value = ref PooledCollectionsMarshal.GetValueRefOrAddDefault(map, Key, out var exists);
            var initial = value;
            value = WrittenValue;
            return (exists, initial, map.Count, map[Key]);
        }
    }

    static (bool Exists, int ValueThroughRef, int Count) GetExistingThroughAddDefault<TAlgo>(PooledHashMap<int, int, TAlgo> map)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        using (map)
        {
            map.Add(Key, StoredValue);
            var value = PooledCollectionsMarshal.GetValueRefOrAddDefault(map, Key, out var exists);
            return (exists, value, map.Count);
        }
    }

    // Counts each key Repeats times through the ref, the way a word-count loop would, starting
    // from a tiny map so the adds grow it several times.
    static (int Count, int[] Counts, bool Grew) CountOccurrences<TAlgo>(PooledHashMap<int, int, TAlgo> map, int keyCount, int repeats)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        using (map)
        {
            var initialCapacity = map.Capacity;
            for (var r = 0; r < repeats; r++)
            {
                for (var i = 0; i < keyCount; i++)
                {
                    PooledCollectionsMarshal.GetValueRefOrAddDefault(map, i * 7, out _)++;
                }
            }
            var counts = Enumerable.Range(0, keyCount).Select(i => map[i * 7]).ToArray();
            return (map.Count, counts, map.Capacity > initialCapacity);
        }
    }

    static (bool FirstExists, bool SecondExists, int CountByKey, bool NullForMissingSpan, int ValueBySpan) CountWordTwiceBySpan<TAlgo>(
        PooledHashMap<string, int, TAlgo> map, string word)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        using (map)
        {
            var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
            PooledCollectionsMarshal.GetValueRefOrAddDefault(lookup, word.AsSpan(), out var firstExists)++;
            PooledCollectionsMarshal.GetValueRefOrAddDefault(lookup, word.AsSpan(), out var secondExists)++;
            var nullForMissing = Unsafe.IsNullRef(ref PooledCollectionsMarshal.GetValueRefOrNullRef(lookup, "missing".AsSpan()));
            var valueBySpan = PooledCollectionsMarshal.GetValueRefOrNullRef(lookup, word.AsSpan());
            return (firstExists, secondExists, map[word], nullForMissing, valueBySpan);
        }
    }
}
