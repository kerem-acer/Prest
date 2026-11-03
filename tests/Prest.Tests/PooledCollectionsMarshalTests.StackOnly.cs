using System.Runtime.CompilerServices;
using System.Text;
using ChainedAlgo = Prest.ChainedAlgorithm<Prest.KeyValueSlot<int, int>, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>;
using ComparerChainedAlgo = Prest.ChainedAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerLinearAlgo = Prest.LinearProbingAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerRobinHoodAlgo = Prest.RobinHoodAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerSwissAlgo = Prest.SwissTableAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using LinearAlgo = Prest.LinearProbingAlgorithm<Prest.KeyValueSlot<int, int>, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>;
using MemoryChainedAlgo = Prest.ChainedAlgorithm<Prest.KeyValueSlot<System.ReadOnlyMemory<byte>, int>, System.ReadOnlyMemory<byte>, Prest.ComparerHasher<System.ReadOnlyMemory<byte>>, Prest.NoOpHashFinalizer>;
using MemoryLinearAlgo = Prest.LinearProbingAlgorithm<Prest.KeyValueSlot<System.ReadOnlyMemory<byte>, int>, System.ReadOnlyMemory<byte>, Prest.ComparerHasher<System.ReadOnlyMemory<byte>>, Prest.NoOpHashFinalizer>;
using MemoryRobinHoodAlgo = Prest.RobinHoodAlgorithm<Prest.KeyValueSlot<System.ReadOnlyMemory<byte>, int>, System.ReadOnlyMemory<byte>, Prest.ComparerHasher<System.ReadOnlyMemory<byte>>, Prest.NoOpHashFinalizer>;
using MemorySwissAlgo = Prest.SwissTableAlgorithm<Prest.KeyValueSlot<System.ReadOnlyMemory<byte>, int>, System.ReadOnlyMemory<byte>, Prest.ComparerHasher<System.ReadOnlyMemory<byte>>, Prest.NoOpHashFinalizer>;
using RobinHoodAlgo = Prest.RobinHoodAlgorithm<Prest.KeyValueSlot<int, int>, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>;
using StringChainedAlgo = Prest.ChainedAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.EqualityDefaultHasher<string>, Prest.NoOpHashFinalizer>;
using StringLinearAlgo = Prest.LinearProbingAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.EqualityDefaultHasher<string>, Prest.NoOpHashFinalizer>;
using StringRobinHoodAlgo = Prest.RobinHoodAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.EqualityDefaultHasher<string>, Prest.NoOpHashFinalizer>;
using StringSwissAlgo = Prest.SwissTableAlgorithm<Prest.KeyValueSlot<string, int>, string, Prest.EqualityDefaultHasher<string>, Prest.NoOpHashFinalizer>;
using SwissAlgo = Prest.SwissTableAlgorithm<Prest.KeyValueSlot<int, int>, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>;

namespace Prest.Tests;

/// <summary>
/// <see cref="PooledCollectionsMarshal" /> on <see cref="StackOnlyPooledHashMap{TKey,TValue,TAlgo}" />
/// and <see cref="StackOnlyPooledHashMap{TKey,TValue}" />. Every map is a <c>using var</c> local,
/// the case the <see langword="in" /> plain-key overloads and the by-value lookup overloads exist for.
/// </summary>
public partial class PooledCollectionsMarshalTests
{
    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task GetValueRefOrNullRef_StackOnlyKeyPresent_WritesThroughToTheMap(HashAlgorithmKind algorithm)
    {
        // Act
        var (readThroughRef, valueAfterWrite) = algorithm switch
        {
            HashAlgorithmKind.Swiss => ReadAndWriteThroughRefOnUsingVar<SwissAlgo>(),
            HashAlgorithmKind.RobinHood => ReadAndWriteThroughRefOnUsingVar<RobinHoodAlgo>(),
            HashAlgorithmKind.Linear => ReadAndWriteThroughRefOnUsingVar<LinearAlgo>(),
            HashAlgorithmKind.Chained => ReadAndWriteThroughRefOnUsingVar<ChainedAlgo>(),
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
    public async Task GetValueRefOrNullRef_StackOnlyKeyMissing_ReturnsNullRef(HashAlgorithmKind algorithm)
    {
        // Act
        var (nullOnEmptyMap, nullOnMiss) = algorithm switch
        {
            HashAlgorithmKind.Swiss => LookUpMissingKeyOnUsingVar<SwissAlgo>(),
            HashAlgorithmKind.RobinHood => LookUpMissingKeyOnUsingVar<RobinHoodAlgo>(),
            HashAlgorithmKind.Linear => LookUpMissingKeyOnUsingVar<LinearAlgo>(),
            HashAlgorithmKind.Chained => LookUpMissingKeyOnUsingVar<ChainedAlgo>(),
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
    public async Task GetValueRefOrAddDefault_StackOnlyUsingVar_AddsToTheLocalOnceAndReturnsBuffersOnce(HashAlgorithmKind algorithm)
    {
        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddDefaultTwiceOnUsingVar<SwissAlgo>(),
            HashAlgorithmKind.RobinHood => AddDefaultTwiceOnUsingVar<RobinHoodAlgo>(),
            HashAlgorithmKind.Linear => AddDefaultTwiceOnUsingVar<LinearAlgo>(),
            HashAlgorithmKind.Chained => AddDefaultTwiceOnUsingVar<ChainedAlgo>(),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert — had the `in` parameter been copied, the add (and the rented buffers) would have gone
        // to the copy: Count would be 0, the second call would add again, and the local would return nothing.
        await Assert.That(result.FirstExists).IsFalse();
        await Assert.That(result.InitialValue).IsEqualTo(0);
        await Assert.That(result.SecondExists).IsTrue();
        await Assert.That(result.ValueOnSecondCall).IsEqualTo(WrittenValue);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.ValueInMap).IsEqualTo(WrittenValue);
        await Assert.That(result.Returns).IsEqualTo(1);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task GetValueRefOrAddDefault_StackOnlyCountOccurrencesFromCapacityZero_CountsEveryKey(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int KeyCount = 2000;
        const int Repeats = 3;

        // Act
        var (capacityBefore, count, counts, capacityAfter) = algorithm switch
        {
            HashAlgorithmKind.Swiss => CountOccurrencesOnUsingVar<SwissAlgo>(KeyCount, Repeats),
            HashAlgorithmKind.RobinHood => CountOccurrencesOnUsingVar<RobinHoodAlgo>(KeyCount, Repeats),
            HashAlgorithmKind.Linear => CountOccurrencesOnUsingVar<LinearAlgo>(KeyCount, Repeats),
            HashAlgorithmKind.Chained => CountOccurrencesOnUsingVar<ChainedAlgo>(KeyCount, Repeats),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(capacityBefore).IsEqualTo(0);
        await Assert.That(capacityAfter).IsGreaterThanOrEqualTo(KeyCount);
        await Assert.That(count).IsEqualTo(KeyCount);
        await Assert.That(counts.All(c => c == Repeats)).IsTrue();
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task GetValueRefOrAddDefault_StackOnlyComparerAlternateLookup_CreatesKeyOnlyWhenAdding(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string Word = "token";
        const string MissingWord = "missing";
        var comparer = new CountingSpanComparer();
        var hasher = new ComparerHasher<string>(comparer);

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => CountWordTwiceBySpanOnUsingVar(new ComparerSwissAlgo(hasher, default), Word, MissingWord),
            HashAlgorithmKind.RobinHood => CountWordTwiceBySpanOnUsingVar(new ComparerRobinHoodAlgo(hasher, default), Word, MissingWord),
            HashAlgorithmKind.Linear => CountWordTwiceBySpanOnUsingVar(new ComparerLinearAlgo(hasher, default), Word, MissingWord),
            HashAlgorithmKind.Chained => CountWordTwiceBySpanOnUsingVar(new ComparerChainedAlgo(hasher, default), Word, MissingWord),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.FirstExists).IsFalse();
        await Assert.That(result.SecondExists).IsTrue();
        await Assert.That(comparer.CreateCount).IsEqualTo(1);
        await Assert.That(result.CountByKey).IsEqualTo(2);
        await Assert.That(result.NullForMissingSpan).IsTrue();
        await Assert.That(result.ValueBySpan).IsEqualTo(2);
        await Assert.That(result.Returns).IsEqualTo(1);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task GetValueRefOrNullRef_StackOnlyMemoryKeyBySpan_FindsAddsAndWritesThrough(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string StoredAddress = "10.0.0.1";
        const string NewAddress = "10.0.0.2";
        var comparer = new ByteContentComparer();
        var hasher = new ComparerHasher<ReadOnlyMemory<byte>>(comparer);

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => LookUpSymbolsBySpan(new MemorySwissAlgo(hasher, default), StoredAddress, NewAddress),
            HashAlgorithmKind.RobinHood => LookUpSymbolsBySpan(new MemoryRobinHoodAlgo(hasher, default), StoredAddress, NewAddress),
            HashAlgorithmKind.Linear => LookUpSymbolsBySpan(new MemoryLinearAlgo(hasher, default), StoredAddress, NewAddress),
            HashAlgorithmKind.Chained => LookUpSymbolsBySpan(new MemoryChainedAlgo(hasher, default), StoredAddress, NewAddress),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.Found).IsTrue();
        await Assert.That(result.ReadThroughRef).IsEqualTo(StoredValue);
        await Assert.That(result.StoredAfterWrite).IsEqualTo(WrittenValue);
        await Assert.That(result.NullForNew).IsTrue();
        await Assert.That(result.FirstExists).IsFalse();
        await Assert.That(result.SecondExists).IsTrue();
        await Assert.That(result.NewValue).IsEqualTo(2);
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(comparer.CreateCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task GetValueRefOrAddDefault_StackOnlyStructHasherAlternateLookup_FindsAndAddsByUtf8(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string Existing = "alpha";
        const string Added = "beta";
        const int ExistingValue = 1;

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => FindAndAddByUtf8OnUsingVar<StringSwissAlgo>(Existing, Added, ExistingValue),
            HashAlgorithmKind.RobinHood => FindAndAddByUtf8OnUsingVar<StringRobinHoodAlgo>(Existing, Added, ExistingValue),
            HashAlgorithmKind.Linear => FindAndAddByUtf8OnUsingVar<StringLinearAlgo>(Existing, Added, ExistingValue),
            HashAlgorithmKind.Chained => FindAndAddByUtf8OnUsingVar<StringChainedAlgo>(Existing, Added, ExistingValue),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.NullForAdded).IsTrue();
        await Assert.That(result.AddedExisted).IsFalse();
        await Assert.That(result.ExistingValue).IsEqualTo(ExistingValue + WrittenValue);
        await Assert.That(result.AddedValue).IsEqualTo(WrittenValue);
        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetValueRefOrNullRef_DefaultStackOnlyMapWithByteComparer_FindsAndAddsBySpan()
    {
        // Arrange — the short form: the default stack-only map with a comparer, looked up by address.Span.
        const string StoredAddress = "10.0.0.1";
        const string NewAddress = "10.0.0.2";
        const int NewValue = 7;
        var comparer = new ByteContentComparer();
        ReadOnlyMemory<byte> stored = Encoding.UTF8.GetBytes(StoredAddress);
        ReadOnlyMemory<byte> address = Encoding.UTF8.GetBytes(StoredAddress);
        ReadOnlyMemory<byte> newAddress = Encoding.UTF8.GetBytes(NewAddress);
        bool found, nullForNew, firstExists, secondExists;
        int readThroughRef, storedAfterWrite, newValue, count;
        {
            using var map = new StackOnlyPooledHashMap<ReadOnlyMemory<byte>, int>(8, comparer);
            map.Add(stored, StoredValue);

            // Act
            var lookup = map.GetAlternateLookup<ReadOnlySpan<byte>>();
            ref var symbol = ref PooledCollectionsMarshal.GetValueRefOrNullRef(lookup, address.Span);
            found = !Unsafe.IsNullRef(ref symbol);
            readThroughRef = symbol;
            symbol = WrittenValue;
            nullForNew = Unsafe.IsNullRef(ref PooledCollectionsMarshal.GetValueRefOrNullRef(lookup, newAddress.Span));
            PooledCollectionsMarshal.GetValueRefOrAddDefault(lookup, newAddress.Span, out firstExists) = NewValue;
            PooledCollectionsMarshal.GetValueRefOrAddDefault(lookup, newAddress.Span, out secondExists)++;
            storedAfterWrite = map[address];
            newValue = map[newAddress];
            count = map.Count;
        }

        // Assert
        await Assert.That(found).IsTrue();
        await Assert.That(readThroughRef).IsEqualTo(StoredValue);
        await Assert.That(storedAfterWrite).IsEqualTo(WrittenValue);
        await Assert.That(nullForNew).IsTrue();
        await Assert.That(firstExists).IsFalse();
        await Assert.That(secondExists).IsTrue();
        await Assert.That(newValue).IsEqualTo(NewValue + 1);
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(comparer.CreateCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetValueRefOrAddDefault_DefaultStackOnlyMap_CountsWordsAndWritesThrough()
    {
        // Arrange
        const string Apple = "apple";
        const string Pear = "pear";
        const string Fig = "fig";
        const string Missing = "missing";
        const int FigValue = 9;
        string[] words = [Apple, Pear, Apple, Apple, Pear];
        var figUtf8 = Encoding.UTF8.GetBytes(Fig);
        bool nullForMissing, figExisted;
        int apples, pears, applesAfterReset, figs, count;
        {
            using var counts = new StackOnlyPooledHashMap<string, int>(0);

            // Act
            foreach (var word in words)
            {
                PooledCollectionsMarshal.GetValueRefOrAddDefault(counts, word, out _)++;
            }
            apples = counts[Apple];
            pears = counts[Pear];
            ref var apple = ref PooledCollectionsMarshal.GetValueRefOrNullRef(counts, Apple);
            apple = 0;
            applesAfterReset = counts[Apple];
            nullForMissing = Unsafe.IsNullRef(ref PooledCollectionsMarshal.GetValueRefOrNullRef(counts, Missing));
            var byUtf8 = counts.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>();
            PooledCollectionsMarshal.GetValueRefOrAddDefault(byUtf8, figUtf8, out figExisted) = FigValue;
            figs = counts[Fig];
            count = counts.Count;
        }

        // Assert
        await Assert.That(apples).IsEqualTo(3);
        await Assert.That(pears).IsEqualTo(2);
        await Assert.That(applesAfterReset).IsEqualTo(0);
        await Assert.That(nullForMissing).IsTrue();
        await Assert.That(figExisted).IsFalse();
        await Assert.That(figs).IsEqualTo(FigValue);
        await Assert.That(count).IsEqualTo(3);
    }

    // ---- stack-only helpers ----

    static (int ReadThroughRef, int ValueAfterWrite) ReadAndWriteThroughRefOnUsingVar<TAlgo>()
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        using var map = new StackOnlyPooledHashMap<int, int, TAlgo>(0);
        map.Add(Key, StoredValue);
        ref var value = ref PooledCollectionsMarshal.GetValueRefOrNullRef(map, Key);
        var read = value;
        value = WrittenValue;
        return (read, map[Key]);
    }

    static (bool NullOnEmptyMap, bool NullOnMiss) LookUpMissingKeyOnUsingVar<TAlgo>()
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        using var map = new StackOnlyPooledHashMap<int, int, TAlgo>(0);
        var nullOnEmptyMap = Unsafe.IsNullRef(ref PooledCollectionsMarshal.GetValueRefOrNullRef(map, MissingKey));
        map.Add(Key, StoredValue);
        var nullOnMiss = Unsafe.IsNullRef(ref PooledCollectionsMarshal.GetValueRefOrNullRef(map, MissingKey));
        return (nullOnEmptyMap, nullOnMiss);
    }

    readonly record struct AddDefaultResult(
        bool FirstExists, int InitialValue, bool SecondExists, int ValueOnSecondCall, int Count, int ValueInMap, int Returns);

    // A map with no buffers yet, so the first GetValueRefOrAddDefault rents them. The counting
    // algorithm shows whether that rental landed in the `using var` local (one return on dispose)
    // or in a hidden copy (none).
    static AddDefaultResult AddDefaultTwiceOnUsingVar<TInner>()
        where TInner : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        var log = new BufferReturnLog();
        bool firstExists, secondExists;
        int initial, onSecondCall, count, stored;
        {
            using var map = new StackOnlyPooledHashMap<int, int, ReturnCountingAlgorithm<KeyValueSlot<int, int>, int, TInner>>(0, new(log));
            ref var value = ref PooledCollectionsMarshal.GetValueRefOrAddDefault(map, Key, out firstExists);
            initial = value;
            value = WrittenValue;
            onSecondCall = PooledCollectionsMarshal.GetValueRefOrAddDefault(map, Key, out secondExists);
            count = map.Count;
            stored = map[Key];
        }
        return new AddDefaultResult(firstExists, initial, secondExists, onSecondCall, count, stored, log.Returns);
    }

    static (int CapacityBefore, int Count, int[] Counts, int CapacityAfter) CountOccurrencesOnUsingVar<TAlgo>(int keyCount, int repeats)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        using var map = new StackOnlyPooledHashMap<int, int, TAlgo>(0);
        var capacityBefore = map.Capacity;
        for (var r = 0; r < repeats; r++)
        {
            for (var i = 0; i < keyCount; i++)
            {
                PooledCollectionsMarshal.GetValueRefOrAddDefault(map, i * 7, out _)++;
            }
        }
        var counts = new int[keyCount];
        for (var i = 0; i < keyCount; i++)
        {
            counts[i] = map[i * 7];
        }
        return (capacityBefore, map.Count, counts, map.Capacity);
    }

    readonly record struct CountWordResult(bool FirstExists, bool SecondExists, int CountByKey, bool NullForMissingSpan, int ValueBySpan, int Returns);

    static CountWordResult CountWordTwiceBySpanOnUsingVar<TInner>(TInner algorithm, string word, string missingWord)
        where TInner : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        var log = new BufferReturnLog();
        bool firstExists, secondExists, nullForMissing;
        int countByKey, valueBySpan;
        {
            using var map = new StackOnlyPooledHashMap<string, int, ReturnCountingAlgorithm<KeyValueSlot<string, int>, string, TInner>>(0, new(log, algorithm));
            var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
            PooledCollectionsMarshal.GetValueRefOrAddDefault(lookup, word.AsSpan(), out firstExists)++;
            PooledCollectionsMarshal.GetValueRefOrAddDefault(lookup, word.AsSpan(), out secondExists)++;
            nullForMissing = Unsafe.IsNullRef(ref PooledCollectionsMarshal.GetValueRefOrNullRef(lookup, missingWord.AsSpan()));
            valueBySpan = PooledCollectionsMarshal.GetValueRefOrNullRef(lookup, word.AsSpan());
            countByKey = map[word];
        }
        return new CountWordResult(firstExists, secondExists, countByKey, nullForMissing, valueBySpan, log.Returns);
    }

    readonly record struct SymbolResult(
        bool Found, int ReadThroughRef, int StoredAfterWrite, bool NullForNew, bool FirstExists, bool SecondExists, int NewValue, int Count);

    // The motivating case: ReadOnlyMemory<byte> keys found by the span of another buffer holding the
    // same bytes, read and written in place through the marshal overloads.
    static SymbolResult LookUpSymbolsBySpan<TAlgo>(TAlgo algorithm, string storedAddress, string newAddress)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<ReadOnlyMemory<byte>, int>, ReadOnlyMemory<byte>>
    {
        ReadOnlyMemory<byte> stored = Encoding.UTF8.GetBytes(storedAddress);
        ReadOnlyMemory<byte> address = Encoding.UTF8.GetBytes(storedAddress);
        ReadOnlyMemory<byte> added = Encoding.UTF8.GetBytes(newAddress);

        using var map = new StackOnlyPooledHashMap<ReadOnlyMemory<byte>, int, TAlgo>(0, algorithm);
        map.Add(stored, StoredValue);
        var lookup = map.GetAlternateLookup<ReadOnlySpan<byte>>();

        ref var symbol = ref PooledCollectionsMarshal.GetValueRefOrNullRef(lookup, address.Span);
        var found = !Unsafe.IsNullRef(ref symbol);
        var read = symbol;
        symbol = WrittenValue;
        var nullForNew = Unsafe.IsNullRef(ref PooledCollectionsMarshal.GetValueRefOrNullRef(lookup, added.Span));
        PooledCollectionsMarshal.GetValueRefOrAddDefault(lookup, added.Span, out var firstExists)++;
        PooledCollectionsMarshal.GetValueRefOrAddDefault(lookup, added.Span, out var secondExists)++;
        return new SymbolResult(found, read, map[stored], nullForNew, firstExists, secondExists, map[added], map.Count);
    }

    readonly record struct Utf8MarshalResult(bool NullForAdded, bool AddedExisted, int ExistingValue, int AddedValue, int Count);

    static Utf8MarshalResult FindAndAddByUtf8OnUsingVar<TAlgo>(string existing, string added, int existingValue)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        var existingUtf8 = Encoding.UTF8.GetBytes(existing);
        var addedUtf8 = Encoding.UTF8.GetBytes(added);
        using var map = new StackOnlyPooledHashMap<string, int, TAlgo>(0);
        map.Add(existing, existingValue);
        var lookup = map.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>();

        PooledCollectionsMarshal.GetValueRefOrNullRef(lookup, existingUtf8) += WrittenValue;
        var nullForAdded = Unsafe.IsNullRef(ref PooledCollectionsMarshal.GetValueRefOrNullRef(lookup, addedUtf8));
        PooledCollectionsMarshal.GetValueRefOrAddDefault(lookup, addedUtf8, out var addedExisted) = WrittenValue;
        return new Utf8MarshalResult(nullForAdded, addedExisted, map[existing], map[added], map.Count);
    }
}
