using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using TUnit.Assertions.Enums;

namespace Prest.Tests;

public partial class PooledHashSetTests
{
    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_AddAndRemoveBySpanAcrossGrowth_FindsRemainingItems(HashAlgorithmKind algorithm)
    {
        // Arrange
        var items = StringItems("item-", ItemCount);
        var expectedPresence = Enumerable.Range(0, ItemCount).Select(i => i % 2 == 1).ToArray();

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThenRemoveEvenItemsBySpan(() => SwissHashSet<string>.Create(4), items),
            HashAlgorithmKind.RobinHood => AddThenRemoveEvenItemsBySpan(() => RobinHoodHashSet<string>.Create(4), items),
            HashAlgorithmKind.Linear => AddThenRemoveEvenItemsBySpan(() => LinearHashSet<string>.Create(4), items),
            HashAlgorithmKind.Chained => AddThenRemoveEvenItemsBySpan(() => ChainedHashSet<string>.Create(4), items),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.Added).IsEqualTo(ItemCount);
        await Assert.That(result.Grew).IsTrue();
        await Assert.That(result.Removed).IsEqualTo(ItemCount / 2);
        await Assert.That(result.Count).IsEqualTo(ItemCount / 2);
        await Assert.That(result.PresentBySpan).IsEquivalentTo(expectedPresence, CollectionOrdering.Matching);
        await Assert.That(result.PresentByItem).IsEquivalentTo(expectedPresence, CollectionOrdering.Matching);
        await Assert.That(result.StoredItem).IsEqualTo(items[1]);
        await Assert.That(result.StoredItem).IsNotSameReferenceAs(items[1]);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_IgnoreCaseComparer_MatchesAnyCasing(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string StoredItem = "Hello";
        const string LookupItem = "hELLO";
        var comparer = StringComparer.OrdinalIgnoreCase;

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => LookUpWithOtherCasing(ComparerSwissHashSet<string>.Create(comparer), StoredItem, LookupItem),
            HashAlgorithmKind.RobinHood => LookUpWithOtherCasing(ComparerRobinHoodHashSet<string>.Create(comparer), StoredItem, LookupItem),
            HashAlgorithmKind.Linear => LookUpWithOtherCasing(ComparerLinearHashSet<string>.Create(comparer), StoredItem, LookupItem),
            HashAlgorithmKind.Chained => LookUpWithOtherCasing(ComparerChainedHashSet<string>.Create(comparer), StoredItem, LookupItem),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.Contained).IsTrue();
        await Assert.That(result.ActualItem).IsEqualTo(StoredItem);
        await Assert.That(result.AddedDuplicate).IsFalse();
        await Assert.That(result.Removed).IsTrue();
        await Assert.That(result.CountAfterRemove).IsEqualTo(0);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Add_AlternateItemAlreadyPresent_DoesNotCreateItem(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string Item = "answer";
        var comparer = new CountingSpanComparer();

        // Act
        var (firstAdded, secondAdded) = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddSameSpanTwice(ComparerSwissHashSet<string>.Create(comparer), Item),
            HashAlgorithmKind.RobinHood => AddSameSpanTwice(ComparerRobinHoodHashSet<string>.Create(comparer), Item),
            HashAlgorithmKind.Linear => AddSameSpanTwice(ComparerLinearHashSet<string>.Create(comparer), Item),
            HashAlgorithmKind.Chained => AddSameSpanTwice(ComparerChainedHashSet<string>.Create(comparer), Item),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(firstAdded).IsTrue();
        await Assert.That(secondAdded).IsFalse();
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
            HashAlgorithmKind.Swiss => SupportsSpanLookup(ComparerSwissHashSet<string>.Create(comparer)),
            HashAlgorithmKind.RobinHood => SupportsSpanLookup(ComparerRobinHoodHashSet<string>.Create(comparer)),
            HashAlgorithmKind.Linear => SupportsSpanLookup(ComparerLinearHashSet<string>.Create(comparer)),
            HashAlgorithmKind.Chained => SupportsSpanLookup(ComparerChainedHashSet<string>.Create(comparer)),
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
        const string PresentItem = "present";
        const string MissingItem = "missing";

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => MeasureAlternateAllocations(SwissHashSet<string>.Create(), PresentItem, MissingItem),
            HashAlgorithmKind.RobinHood => MeasureAlternateAllocations(RobinHoodHashSet<string>.Create(), PresentItem, MissingItem),
            HashAlgorithmKind.Linear => MeasureAlternateAllocations(LinearHashSet<string>.Create(), PresentItem, MissingItem),
            HashAlgorithmKind.Chained => MeasureAlternateAllocations(ChainedHashSet<string>.Create(), PresentItem, MissingItem),
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
        var items = StringItems("clé-日本-", ItemCount);
        var expectedPresence = Enumerable.Range(0, ItemCount).Select(i => i % 2 == 1).ToArray();

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThenRemoveEvenItemsByUtf8(SwissHashSet<string>.Create(4), items),
            HashAlgorithmKind.RobinHood => AddThenRemoveEvenItemsByUtf8(RobinHoodHashSet<string>.Create(4), items),
            HashAlgorithmKind.Linear => AddThenRemoveEvenItemsByUtf8(LinearHashSet<string>.Create(4), items),
            HashAlgorithmKind.Chained => AddThenRemoveEvenItemsByUtf8(ChainedHashSet<string>.Create(4), items),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.Added).IsEqualTo(ItemCount);
        await Assert.That(result.AddedDuplicate).IsFalse();
        await Assert.That(result.Removed).IsEqualTo(ItemCount / 2);
        await Assert.That(result.Count).IsEqualTo(ItemCount / 2);
        await Assert.That(result.PresentByBytes).IsEquivalentTo(expectedPresence, CollectionOrdering.Matching);
        await Assert.That(result.PresentByItem).IsEquivalentTo(expectedPresence, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_SetWithoutBuffers_MissesThenAddAllocatesBuffers(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string Item = "first";

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => UseUnallocatedSetBySpan(() => SwissHashSet<string>.Create(0), Item),
            HashAlgorithmKind.RobinHood => UseUnallocatedSetBySpan(() => RobinHoodHashSet<string>.Create(0), Item),
            HashAlgorithmKind.Linear => UseUnallocatedSetBySpan(() => LinearHashSet<string>.Create(0), Item),
            HashAlgorithmKind.Chained => UseUnallocatedSetBySpan(() => ChainedHashSet<string>.Create(0), Item),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.ContainedBeforeAdd).IsFalse();
        await Assert.That(result.FoundBeforeAdd).IsFalse();
        await Assert.That(result.RemovedBeforeAdd).IsFalse();
        await Assert.That(result.Added).IsTrue();
        await Assert.That(result.ContainedAfterAdd).IsTrue();
    }

    [Test]
    public async Task GetAlternateLookup_ComparerWithoutSpanSupport_ThrowsInvalidOperationException()
    {
        // Arrange
        using var set = ComparerSwissHashSet<string>.Create(new NoAlternateComparer());

        // Act & Assert
        await Assert.That(() => _ = set.GetAlternateLookup<ReadOnlySpan<char>>()).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task TryGetAlternateLookup_DefaultHasherWithUnsupportedAlternate_ReturnsFalse()
    {
        // Arrange
        using var set = PooledHashSet<int>.Create();

        // Act
        var supported = set.TryGetAlternateLookup<long>(out _);

        // Assert
        await Assert.That(supported).IsFalse();
    }

    [Test]
    public async Task AlternateLookup_TryGetValueMissing_ReturnsFalseAndSetProperty_ReturnsOwningSet()
    {
        // Arrange
        using var set = PooledHashSet<string>.Create();
        set.Add("present");
        var lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
        var utf8Lookup = set.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>();

        // Act
        var found = lookup.TryGetValue("missing".AsSpan(), out var actual);
        var utf8Found = utf8Lookup.TryGetValue("missing"u8, out var utf8Actual);

        // Assert
        await Assert.That(found).IsFalse();
        await Assert.That(actual).IsNull();
        await Assert.That(utf8Found).IsFalse();
        await Assert.That(utf8Actual).IsNull();
        await Assert.That(lookup.Set).IsSameReferenceAs(set);
        await Assert.That(utf8Lookup.Set).IsSameReferenceAs(set);
    }

    // ---- helpers ----

    // Successful operations per ExerciseBySpan / ExerciseByUtf8 call.
    const int AlternateOperationCount = 6;

    static string[] StringItems(string prefix, int count) =>
        Enumerable.Range(0, count).Select(i => prefix + i.ToString(CultureInfo.InvariantCulture)).ToArray();

    readonly record struct SpanRoundTrip(int Added, bool Grew, int Removed, int Count, bool[] PresentBySpan, bool[] PresentByItem, string? StoredItem);

    // Adds every item through the span lookup into a fresh small set (forcing growth after
    // alternate adds), then removes the even items through the span lookup (exercising
    // tombstones, backward shift and swap-with-last), and checks every item both ways.
    static SpanRoundTrip AddThenRemoveEvenItemsBySpan<TAlgo>(Func<PooledHashSet<string, TAlgo>> create, string[] items)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        // The first create() takes whatever the per-thread cache holds, so the second one
        // builds a fresh, small set.
        _ = create();
        using var set = create();
        var lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
        var initialCapacity = set.Capacity;

        var added = items.Count(item => lookup.Add(item.AsSpan()));
        var grew = set.Capacity > initialCapacity;
        var removed = items.Where((_, i) => i % 2 == 0).Count(item => lookup.Remove(item.AsSpan()));
        var presentBySpan = items.Select(item => lookup.Contains(item.AsSpan())).ToArray();
        var presentByItem = items.Select(set.Contains).ToArray();
        lookup.TryGetValue(items[1].AsSpan(), out var storedItem);
        return new SpanRoundTrip(added, grew, removed, set.Count, presentBySpan, presentByItem, storedItem);
    }

    readonly record struct CasingResult(bool Contained, string? ActualItem, bool AddedDuplicate, bool Removed, int CountAfterRemove);

    static CasingResult LookUpWithOtherCasing<TAlgo>(PooledHashSet<string, TAlgo> set, string storedItem, string lookupItem)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        using (set)
        {
            set.Add(storedItem);
            var lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
            var contained = lookup.Contains(lookupItem.AsSpan());
            lookup.TryGetValue(lookupItem.AsSpan(), out var actualItem);
            var addedDuplicate = lookup.Add(lookupItem.AsSpan());
            var removed = lookup.Remove(lookupItem.AsSpan());
            return new CasingResult(contained, actualItem, addedDuplicate, removed, set.Count);
        }
    }

    static (bool FirstAdded, bool SecondAdded) AddSameSpanTwice<TAlgo>(PooledHashSet<string, TAlgo> set, string item)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        using (set)
        {
            var lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
            return (lookup.Add(item.AsSpan()), lookup.Add(item.AsSpan()));
        }
    }

    static bool SupportsSpanLookup<TAlgo>(PooledHashSet<string, TAlgo> set)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        using (set)
        {
            return set.TryGetAlternateLookup<ReadOnlySpan<char>>(out _);
        }
    }

    readonly record struct AllocationResult(long ComparerRouteBytes, long HasherRouteBytes, int Succeeded);

    static AllocationResult MeasureAlternateAllocations<TAlgo>(PooledHashSet<string, TAlgo> set, string presentItem, string missingItem)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        using (set)
        {
            set.Add(presentItem);
            var presentUtf8 = Encoding.UTF8.GetBytes(presentItem);
            var missingUtf8 = Encoding.UTF8.GetBytes(missingItem);
            _ = ExerciseBySpan(set, presentItem, missingItem); // warm up so JIT work is not measured
            _ = ExerciseByUtf8(set, presentUtf8, missingUtf8);

            var before = GC.GetAllocatedBytesForCurrentThread();
            var succeeded = ExerciseBySpan(set, presentItem, missingItem);
            var comparerRouteBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            before = GC.GetAllocatedBytesForCurrentThread();
            succeeded += ExerciseByUtf8(set, presentUtf8, missingUtf8);
            var hasherRouteBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            return new AllocationResult(comparerRouteBytes, hasherRouteBytes, succeeded);
        }
    }

    // Every operation except creating a new item: lookups (hit and miss), a duplicate Add, and a
    // Remove whose stored item is put back.
    [MethodImpl(MethodImplOptions.NoInlining)]
    static int ExerciseBySpan<TAlgo>(PooledHashSet<string, TAlgo> set, string presentItem, string missingItem)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        var lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
        var succeeded = 0;
        succeeded += lookup.Contains(presentItem.AsSpan()) ? 1 : 0;
        succeeded += lookup.TryGetValue(presentItem.AsSpan(), out var stored) ? 1 : 0;
        succeeded += lookup.Add(presentItem.AsSpan()) ? 0 : 1;
        succeeded += lookup.Contains(missingItem.AsSpan()) ? 0 : 1;
        succeeded += lookup.Remove(missingItem.AsSpan()) ? 0 : 1;
        if (lookup.Remove(presentItem.AsSpan()))
        {
            set.Add(stored!);
            succeeded++;
        }
        return succeeded;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int ExerciseByUtf8<TAlgo>(PooledHashSet<string, TAlgo> set, byte[] presentItem, byte[] missingItem)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        var lookup = set.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>();
        var succeeded = 0;
        succeeded += lookup.Contains(presentItem) ? 1 : 0;
        succeeded += lookup.TryGetValue(presentItem, out var stored) ? 1 : 0;
        succeeded += lookup.Add(presentItem) ? 0 : 1;
        succeeded += lookup.Contains(missingItem) ? 0 : 1;
        succeeded += lookup.Remove(missingItem) ? 0 : 1;
        if (lookup.Remove(presentItem))
        {
            set.Add(stored!);
            succeeded++;
        }
        return succeeded;
    }

    readonly record struct Utf8RoundTrip(int Added, bool AddedDuplicate, int Removed, int Count, bool[] PresentByBytes, bool[] PresentByItem);

    static Utf8RoundTrip AddThenRemoveEvenItemsByUtf8<TAlgo>(PooledHashSet<string, TAlgo> set, string[] items)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        using (set)
        {
            var utf8Items = items.Select(Encoding.UTF8.GetBytes).ToArray();
            var lookup = set.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>();
            var added = utf8Items.Count(item => lookup.Add(item));
            var addedDuplicate = lookup.Add(utf8Items[0]);
            var removed = utf8Items.Where((_, i) => i % 2 == 0).Count(item => lookup.Remove(item));
            var presentByBytes = utf8Items.Select(item => lookup.Contains(item)).ToArray();
            var presentByItem = items.Select(set.Contains).ToArray();
            return new Utf8RoundTrip(added, addedDuplicate, removed, set.Count, presentByBytes, presentByItem);
        }
    }

    readonly record struct UnallocatedResult(bool ContainedBeforeAdd, bool FoundBeforeAdd, bool RemovedBeforeAdd, bool Added, bool ContainedAfterAdd);

    static UnallocatedResult UseUnallocatedSetBySpan<TAlgo>(Func<PooledHashSet<string, TAlgo>> create, string item)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        // The first create() takes whatever the per-thread cache holds, so the second one
        // builds a set with no buffers yet.
        _ = create();
        using var set = create();
        var lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
        var contained = lookup.Contains(item.AsSpan());
        var found = lookup.TryGetValue(item.AsSpan(), out _);
        var removed = lookup.Remove(item.AsSpan());
        var added = lookup.Add(item.AsSpan());
        return new UnallocatedResult(contained, found, removed, added, lookup.Contains(item.AsSpan()));
    }
}
