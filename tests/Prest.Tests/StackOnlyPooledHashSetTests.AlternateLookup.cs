using System.Text;
using TUnit.Assertions.Enums;
using ComparerChainedAlgo = Prest.ChainedAlgorithm<string, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerLinearAlgo = Prest.LinearProbingAlgorithm<string, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerRobinHoodAlgo = Prest.RobinHoodAlgorithm<string, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using ComparerSwissAlgo = Prest.SwissTableAlgorithm<string, string, Prest.ComparerHasher<string>, Prest.NoOpHashFinalizer>;
using StringChainedAlgo = Prest.ChainedAlgorithm<string, string, Prest.EqualityDefaultHasher<string>, Prest.NoOpHashFinalizer>;
using StringLinearAlgo = Prest.LinearProbingAlgorithm<string, string, Prest.EqualityDefaultHasher<string>, Prest.NoOpHashFinalizer>;
using StringRobinHoodAlgo = Prest.RobinHoodAlgorithm<string, string, Prest.EqualityDefaultHasher<string>, Prest.NoOpHashFinalizer>;
using StringSwissAlgo = Prest.SwissTableAlgorithm<string, string, Prest.EqualityDefaultHasher<string>, Prest.NoOpHashFinalizer>;

namespace Prest.Tests;

/// <summary>
/// Alternate-item lookups on <see cref="StackOnlyPooledHashSet{T,TAlgo}" /> and the default
/// <see cref="StackOnlyPooledHashSet{T}" />. Every helper declares its set as a <c>using var</c>
/// local, gets the lookups from it, and returns plain values to assert on.
/// </summary>
public partial class StackOnlyPooledHashSetTests
{
    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_IgnoreCaseComparer_AddsFindsAndRemovesAnyCasing(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string StoredItem = "Hello";
        const string LookupItem = "hELLO";
        const string NewItem = "World";
        const string NewItemOtherCase = "WORLD";
        var hasher = new ComparerHasher<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => UseIgnoreCaseLookup(new ComparerSwissAlgo(hasher, default), StoredItem, LookupItem, NewItem, NewItemOtherCase),
            HashAlgorithmKind.RobinHood => UseIgnoreCaseLookup(new ComparerRobinHoodAlgo(hasher, default), StoredItem, LookupItem, NewItem, NewItemOtherCase),
            HashAlgorithmKind.Linear => UseIgnoreCaseLookup(new ComparerLinearAlgo(hasher, default), StoredItem, LookupItem, NewItem, NewItemOtherCase),
            HashAlgorithmKind.Chained => UseIgnoreCaseLookup(new ComparerChainedAlgo(hasher, default), StoredItem, LookupItem, NewItem, NewItemOtherCase),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.Contains).IsTrue();
        await Assert.That(result.Found).IsTrue();
        await Assert.That(result.ActualValue).IsEqualTo(StoredItem);
        await Assert.That(result.AddedDuplicate).IsFalse();
        await Assert.That(result.AddedNew).IsTrue();
        await Assert.That(result.CountAfterAdd).IsEqualTo(2);
        await Assert.That(result.ContainsNewByItem).IsTrue();
        await Assert.That(result.Removed).IsTrue();
        await Assert.That(result.RemovedNew).IsTrue();
        await Assert.That(result.CountAfterRemove).IsEqualTo(0);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_AddAndRemoveBySpanFromCapacityZero_GrowsAndKeepsRemainingItems(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int ItemCount = 300;
        var items = StringItems("item-", ItemCount);
        var expectedContained = Enumerable.Range(0, ItemCount).Select(i => i % 2 == 1).ToArray();

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThenRemoveEvenItemsBySpan<StringSwissAlgo>(items),
            HashAlgorithmKind.RobinHood => AddThenRemoveEvenItemsBySpan<StringRobinHoodAlgo>(items),
            HashAlgorithmKind.Linear => AddThenRemoveEvenItemsBySpan<StringLinearAlgo>(items),
            HashAlgorithmKind.Chained => AddThenRemoveEvenItemsBySpan<StringChainedAlgo>(items),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.CapacityBefore).IsEqualTo(0);
        await Assert.That(result.ContainedBeforeAdd).IsFalse();
        await Assert.That(result.RemovedBeforeAdd).IsFalse();
        await Assert.That(result.Added).IsEqualTo(ItemCount);
        await Assert.That(result.CapacityAfterAdd).IsGreaterThanOrEqualTo(ItemCount);
        await Assert.That(result.Removed).IsEqualTo(ItemCount / 2);
        await Assert.That(result.Count).IsEqualTo(ItemCount / 2);
        await Assert.That(result.ContainedBySpan).IsEquivalentTo(expectedContained, CollectionOrdering.Matching);
        await Assert.That(result.ContainedByItem).IsEquivalentTo(expectedContained, CollectionOrdering.Matching);
        await Assert.That(result.StoredItem).IsEqualTo(items[1]);
        await Assert.That(result.StoredItem).IsNotSameReferenceAs(items[1]);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_Utf8StructHasher_AddsFindsAndRemovesByBytes(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int ItemCount = 300;
        var items = StringItems("clé-日本-", ItemCount);
        var expectedContained = Enumerable.Range(0, ItemCount).Select(i => i % 2 == 1).ToArray();

        // Act
        var result = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThenRemoveEvenItemsByUtf8<StringSwissAlgo>(items),
            HashAlgorithmKind.RobinHood => AddThenRemoveEvenItemsByUtf8<StringRobinHoodAlgo>(items),
            HashAlgorithmKind.Linear => AddThenRemoveEvenItemsByUtf8<StringLinearAlgo>(items),
            HashAlgorithmKind.Chained => AddThenRemoveEvenItemsByUtf8<StringChainedAlgo>(items),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(result.Added).IsEqualTo(ItemCount);
        await Assert.That(result.AddedDuplicate).IsFalse();
        await Assert.That(result.ActualValue).IsEqualTo(items[1]);
        await Assert.That(result.Removed).IsEqualTo(ItemCount / 2);
        await Assert.That(result.Count).IsEqualTo(ItemCount / 2);
        await Assert.That(result.ContainedByBytes).IsEquivalentTo(expectedContained, CollectionOrdering.Matching);
        await Assert.That(result.ContainedByItem).IsEquivalentTo(expectedContained, CollectionOrdering.Matching);
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
            using var set = new StackOnlyPooledHashSet<string, ComparerSwissAlgo>(0, new ComparerSwissAlgo(hasher, default));
            _ = set.GetAlternateLookup<ReadOnlySpan<char>>();
        }).ThrowsExactly<InvalidOperationException>();
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
        var hasher = new ComparerHasher<string>(comparer);

        // Act
        var (firstAdded, secondAdded, count) = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddSameSpanTwice(new ComparerSwissAlgo(hasher, default), Item),
            HashAlgorithmKind.RobinHood => AddSameSpanTwice(new ComparerRobinHoodAlgo(hasher, default), Item),
            HashAlgorithmKind.Linear => AddSameSpanTwice(new ComparerLinearAlgo(hasher, default), Item),
            HashAlgorithmKind.Chained => AddSameSpanTwice(new ComparerChainedAlgo(hasher, default), Item),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(firstAdded).IsTrue();
        await Assert.That(secondAdded).IsFalse();
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(comparer.CreateCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task AlternateLookup_UsingVar_WritesReachTheSetAndBuffersReturnOnce(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string SpanItem = "span";
        const string Utf8Item = "utf8";
        const string CopiedViewItem = "copy";

        // Act
        var (count, itemsFound, returns) = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddThroughLookupsOnUsingVar<StringSwissAlgo>(SpanItem, Utf8Item, CopiedViewItem),
            HashAlgorithmKind.RobinHood => AddThroughLookupsOnUsingVar<StringRobinHoodAlgo>(SpanItem, Utf8Item, CopiedViewItem),
            HashAlgorithmKind.Linear => AddThroughLookupsOnUsingVar<StringLinearAlgo>(SpanItem, Utf8Item, CopiedViewItem),
            HashAlgorithmKind.Chained => AddThroughLookupsOnUsingVar<StringChainedAlgo>(SpanItem, Utf8Item, CopiedViewItem),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert — a lookup over a copy of the set would leave Count at 0 and the original would return nothing.
        await Assert.That(count).IsEqualTo(3);
        await Assert.That(itemsFound).IsEqualTo(3);
        await Assert.That(returns).IsEqualTo(1);
    }

    [Test]
    public async Task DefaultSet_GetAlternateLookup_FindsAndAddsBySpanAndUtf8()
    {
        // Arrange
        const string StoredItem = "apple";
        const string SpanItem = "pear";
        const string Utf8Item = "plum";
        var storedUtf8 = Encoding.UTF8.GetBytes(StoredItem);
        var utf8Item = Encoding.UTF8.GetBytes(Utf8Item);
        bool supported, foundBySpan, foundByUtf8, addedBySpan, addedByUtf8, containsSpanItem, containsUtf8Item;
        int count;
        {
            using var set = new StackOnlyPooledHashSet<string>(0);
            set.Add(StoredItem);

            // Act
            supported = set.TryGetAlternateLookup<ReadOnlySpan<char>>(out var bySpan);
            var byUtf8 = set.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>();
            foundBySpan = bySpan.Contains(StoredItem.AsSpan());
            foundByUtf8 = byUtf8.Contains(storedUtf8);
            addedBySpan = set.GetAlternateLookup<ReadOnlySpan<char>>().Add(SpanItem.AsSpan());
            addedByUtf8 = byUtf8.Add(utf8Item);
            count = set.Count;
            containsSpanItem = set.Contains(SpanItem);
            containsUtf8Item = set.Contains(Utf8Item);
        }

        // Assert
        await Assert.That(supported).IsTrue();
        await Assert.That(foundBySpan).IsTrue();
        await Assert.That(foundByUtf8).IsTrue();
        await Assert.That(addedBySpan).IsTrue();
        await Assert.That(addedByUtf8).IsTrue();
        await Assert.That(count).IsEqualTo(3);
        await Assert.That(containsSpanItem).IsTrue();
        await Assert.That(containsUtf8Item).IsTrue();
    }

    [Test]
    public async Task DefaultSet_ComparerFromConstructor_AlternateLookupUsesIt()
    {
        // Arrange
        const string StoredItem = "Hello";
        const string LookupItem = "hELLO";
        bool found, addedDuplicate, removed;
        string? actualValue;
        int countAfterRemove;
        {
            using var set = new StackOnlyPooledHashSet<string>(8, StringComparer.OrdinalIgnoreCase);
            set.Add(StoredItem);

            // Act
            var lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
            found = lookup.TryGetValue(LookupItem.AsSpan(), out actualValue);
            addedDuplicate = lookup.Add(LookupItem.AsSpan());
            removed = lookup.Remove(LookupItem.AsSpan());
            countAfterRemove = set.Count;
        }

        // Assert
        await Assert.That(found).IsTrue();
        await Assert.That(actualValue).IsEqualTo(StoredItem);
        await Assert.That(addedDuplicate).IsFalse();
        await Assert.That(removed).IsTrue();
        await Assert.That(countAfterRemove).IsEqualTo(0);
    }

    [Test]
    public async Task DefaultSet_TryGetAlternateLookup_UnsupportedAlternate_ReturnsFalse()
    {
        // Arrange
        bool supported;
        {
            using var set = new StackOnlyPooledHashSet<int>(0);

            // Act
            supported = set.TryGetAlternateLookup<long>(out _);
        }

        // Assert
        await Assert.That(supported).IsFalse();
    }

    [Test]
    public async Task DefaultSet_GetAlternateLookup_UnsupportedAlternate_ThrowsInvalidOperationException()
    {
        // Act + Assert
        await Assert.That(() =>
        {
            using var set = new StackOnlyPooledHashSet<int>(0);
            _ = set.GetAlternateLookup<long>();
        }).ThrowsExactly<InvalidOperationException>();
    }

    // ---- helpers ----

    static string[] StringItems(string prefix, int count) =>
        Enumerable.Range(0, count).Select(i => prefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    readonly record struct IgnoreCaseResult(
        bool Contains,
        bool Found,
        string? ActualValue,
        bool AddedDuplicate,
        bool AddedNew,
        int CountAfterAdd,
        bool ContainsNewByItem,
        bool Removed,
        bool RemovedNew,
        int CountAfterRemove);

    // Stores storedItem, then goes through every lookup member with items that differ only in casing.
    static IgnoreCaseResult UseIgnoreCaseLookup<TAlgo>(TAlgo algorithm, string storedItem, string lookupItem, string newItem, string newItemOtherCase)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        using var set = new StackOnlyPooledHashSet<string, TAlgo>(0, algorithm);
        set.Add(storedItem);
        var lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();

        var contains = lookup.Contains(lookupItem.AsSpan());
        var found = lookup.TryGetValue(lookupItem.AsSpan(), out var actualValue);
        var addedDuplicate = lookup.Add(lookupItem.AsSpan());
        var addedNew = lookup.Add(newItem.AsSpan());
        var countAfterAdd = set.Count;
        var containsNewByItem = set.Contains(newItemOtherCase);
        var removed = lookup.Remove(lookupItem.AsSpan());
        var removedNew = lookup.Remove(newItemOtherCase.AsSpan());
        return new IgnoreCaseResult(
            contains, found, actualValue, addedDuplicate, addedNew, countAfterAdd, containsNewByItem, removed, removedNew, set.Count);
    }

    readonly record struct SpanRoundTrip(
        int CapacityBefore,
        bool ContainedBeforeAdd,
        bool RemovedBeforeAdd,
        int Added,
        int CapacityAfterAdd,
        int Removed,
        int Count,
        bool[] ContainedBySpan,
        bool[] ContainedByItem,
        string? StoredItem);

    // Starts from a set with no buffers, so the first Add through the lookup rents them and the later
    // ones grow the table. Then removes the even items through the lookup and checks every item both ways.
    static SpanRoundTrip AddThenRemoveEvenItemsBySpan<TAlgo>(string[] items)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        using var set = new StackOnlyPooledHashSet<string, TAlgo>(0);
        var lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
        var capacityBefore = set.Capacity;
        var containedBeforeAdd = lookup.Contains(items[0].AsSpan());
        var removedBeforeAdd = lookup.Remove(items[0].AsSpan());

        var added = 0;
        foreach (var item in items)
        {
            if (lookup.Add(item.AsSpan()))
            {
                added++;
            }
        }
        var capacityAfterAdd = set.Capacity;

        var removed = 0;
        for (var i = 0; i < items.Length; i += 2)
        {
            if (lookup.Remove(items[i].AsSpan()))
            {
                removed++;
            }
        }

        var containedBySpan = new bool[items.Length];
        var containedByItem = new bool[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            containedBySpan[i] = lookup.Contains(items[i].AsSpan());
            containedByItem[i] = set.Contains(items[i]);
        }

        lookup.TryGetValue(items[1].AsSpan(), out var storedItem);
        return new SpanRoundTrip(
            capacityBefore, containedBeforeAdd, removedBeforeAdd, added, capacityAfterAdd, removed, set.Count,
            containedBySpan, containedByItem, storedItem);
    }

    readonly record struct Utf8RoundTrip(
        int Added, bool AddedDuplicate, string? ActualValue, int Removed, int Count, bool[] ContainedByBytes, bool[] ContainedByItem);

    static Utf8RoundTrip AddThenRemoveEvenItemsByUtf8<TAlgo>(string[] items)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        var utf8Items = items.Select(Encoding.UTF8.GetBytes).ToArray();
        using var set = new StackOnlyPooledHashSet<string, TAlgo>(0);
        var lookup = set.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>();

        var added = 0;
        foreach (var item in utf8Items)
        {
            if (lookup.Add(item))
            {
                added++;
            }
        }
        var addedDuplicate = lookup.Add(utf8Items[0]);
        lookup.TryGetValue(utf8Items[1], out var actualValue);

        var removed = 0;
        for (var i = 0; i < utf8Items.Length; i += 2)
        {
            if (lookup.Remove(utf8Items[i]))
            {
                removed++;
            }
        }

        var containedByBytes = new bool[items.Length];
        var containedByItem = new bool[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            containedByBytes[i] = lookup.Contains(utf8Items[i]);
            containedByItem[i] = set.Contains(items[i]);
        }
        return new Utf8RoundTrip(added, addedDuplicate, actualValue, removed, set.Count, containedByBytes, containedByItem);
    }

    static bool SupportsSpanLookup<TAlgo>(TAlgo algorithm)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        using var set = new StackOnlyPooledHashSet<string, TAlgo>(0, algorithm);
        return set.TryGetAlternateLookup<ReadOnlySpan<char>>(out _);
    }

    static (bool FirstAdded, bool SecondAdded, int Count) AddSameSpanTwice<TAlgo>(TAlgo algorithm, string item)
        where TAlgo : struct, IHashAlgorithm<string, string>
    {
        using var set = new StackOnlyPooledHashSet<string, TAlgo>(0, algorithm);
        var lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
        var firstAdded = lookup.Add(item.AsSpan());
        var secondAdded = lookup.Add(item.AsSpan());
        return (firstAdded, secondAdded, set.Count);
    }

    // Adds one item through each kind of lookup on a `using var` set that starts with no buffers, the
    // third through a copy of the view handed to a helper by value.
    static (int Count, int ItemsFound, int Returns) AddThroughLookupsOnUsingVar<TInner>(string spanItem, string utf8Item, string copiedViewItem)
        where TInner : struct, IHashAlgorithm<string, string>
    {
        var log = new BufferReturnLog();
        int count, itemsFound;
        {
            using var set = new StackOnlyPooledHashSet<string, ReturnCountingAlgorithm<string, string, TInner>>(0, new(log));
            var bySpan = set.GetAlternateLookup<ReadOnlySpan<char>>();
            bySpan.Add(spanItem.AsSpan());
            set.GetAlternateLookup<ReadOnlySpan<byte>, Utf8StringHasher>().Add(Encoding.UTF8.GetBytes(utf8Item));
            AddThroughView(bySpan, copiedViewItem);
            count = set.Count;
            itemsFound = (set.Contains(spanItem) ? 1 : 0) + (set.Contains(utf8Item) ? 1 : 0) + (set.Contains(copiedViewItem) ? 1 : 0);
        }
        return (count, itemsFound, log.Returns);
    }

    static void AddThroughView<TAlgo>(StackOnlyPooledHashSet<string, TAlgo>.AlternateLookup<ReadOnlySpan<char>> lookup, string item)
        where TAlgo : struct, IHashAlgorithm<string, string>
        => lookup.Add(item.AsSpan());
}
