namespace Prest.Tests;

/// <summary>
/// Tests for the default <see cref="StackOnlyPooledHashSet{T}" />. Every set is created, used and
/// disposed before the first <c>await</c>: a ref struct cannot live across one.
/// </summary>
public partial class StackOnlyPooledHashSetTests
{
    [Test]
    public async Task Add_DuplicateItem_ReturnsFalseAndKeepsOneCopy()
    {
        // Arrange
        const string Item = "apple";
        bool first, second;
        int count;
        {
            using var set = new StackOnlyPooledHashSet<string>(4);

            // Act
            first = set.Add(Item);
            second = set.Add(Item);
            count = set.Count;
        }

        // Assert
        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Contains_AddedAndMissingItems_ReportsMembership()
    {
        // Arrange
        const int Present = 3;
        const int Missing = 4;
        bool containsPresent, containsMissing;
        {
            using var set = new StackOnlyPooledHashSet<int>(4);
            set.Add(Present);

            // Act
            containsPresent = set.Contains(Present);
            containsMissing = set.Contains(Missing);
        }

        // Assert
        await Assert.That(containsPresent).IsTrue();
        await Assert.That(containsMissing).IsFalse();
    }

    [Test]
    public async Task Remove_ExistingItem_RemovesOnlyThatItem()
    {
        // Arrange
        const int Removed = 1;
        const int Kept = 2;
        bool removed, removedAgain, containsRemoved, containsKept;
        int count;
        {
            using var set = new StackOnlyPooledHashSet<int>(4);
            set.Add(Removed);
            set.Add(Kept);

            // Act
            removed = set.Remove(Removed);
            removedAgain = set.Remove(Removed);
            containsRemoved = set.Contains(Removed);
            containsKept = set.Contains(Kept);
            count = set.Count;
        }

        // Assert
        await Assert.That(removed).IsTrue();
        await Assert.That(removedAgain).IsFalse();
        await Assert.That(containsRemoved).IsFalse();
        await Assert.That(containsKept).IsTrue();
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task TryGetValue_EqualItem_ReturnsTheStoredInstance()
    {
        // Arrange
        var stored = new string('a', 3);
        var equal = new string('a', 3);
        bool found, sameAsStored, sameAsLookup;
        {
            using var set = new StackOnlyPooledHashSet<string>(4);
            set.Add(stored);

            // Act
            found = set.TryGetValue(equal, out var actual);
            sameAsStored = ReferenceEquals(actual, stored);
            sameAsLookup = ReferenceEquals(actual, equal);
        }

        // Assert
        await Assert.That(found).IsTrue();
        await Assert.That(sameAsStored).IsTrue();
        await Assert.That(sameAsLookup).IsFalse();
    }

    [Test]
    public async Task TryGetValue_MissingItem_ReturnsFalse()
    {
        // Arrange
        const string Present = "present";
        const string Missing = "missing";
        bool found;
        string? actual;
        {
            using var set = new StackOnlyPooledHashSet<string>(4);
            set.Add(Present);

            // Act
            found = set.TryGetValue(Missing, out actual);
        }

        // Assert
        await Assert.That(found).IsFalse();
        await Assert.That(actual).IsNull();
    }

    [Test]
    public async Task Default_FirstAdd_RentsBuffers()
    {
        // Arrange
        const int Item = 9;
        int capacityBefore, capacityAfter, count;
        bool emptyBefore, contains;
        {
            using StackOnlyPooledHashSet<int> set = default;
            capacityBefore = set.Capacity;
            emptyBefore = set.IsEmpty;

            // Act
            set.Add(Item);
            capacityAfter = set.Capacity;
            count = set.Count;
            contains = set.Contains(Item);
        }

        // Assert
        await Assert.That(capacityBefore).IsEqualTo(0);
        await Assert.That(emptyBefore).IsTrue();
        await Assert.That(capacityAfter).IsGreaterThan(0);
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(contains).IsTrue();
    }

    [Test]
    public async Task ParameterlessNew_LookupsBeforeFirstAdd_FindNothing()
    {
        // Arrange
        const string Item = "item";
        bool contains, removed, found;
        int capacity;
        {
            using var set = new StackOnlyPooledHashSet<string>();

            // Act
            contains = set.Contains(Item);
            removed = set.Remove(Item);
            found = set.TryGetValue(Item, out _);
            set.Clear();
            capacity = set.Capacity;
        }

        // Assert
        await Assert.That(contains).IsFalse();
        await Assert.That(removed).IsFalse();
        await Assert.That(found).IsFalse();
        await Assert.That(capacity).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_WithCapacity_PreAllocatesAtLeastThatMany()
    {
        // Arrange
        const int RequestedCapacity = 100;
        int capacity;

        // Act
        {
            using var set = new StackOnlyPooledHashSet<int>(RequestedCapacity);
            capacity = set.Capacity;
        }

        // Assert
        await Assert.That(capacity).IsGreaterThanOrEqualTo(RequestedCapacity);
    }

    [Test]
    public async Task Clear_ThenAdd_KeepsCapacityAndStoresNewItems()
    {
        // Arrange
        const int OldItem = 1;
        const int NewItem = 2;
        int capacityBefore, capacityAfter, countAfterClear, countAfterAdd;
        bool containsOld, containsNew;
        {
            using var set = new StackOnlyPooledHashSet<int>(16);
            set.Add(OldItem);
            capacityBefore = set.Capacity;

            // Act
            set.Clear();
            countAfterClear = set.Count;
            set.Add(NewItem);
            capacityAfter = set.Capacity;
            countAfterAdd = set.Count;
            containsOld = set.Contains(OldItem);
            containsNew = set.Contains(NewItem);
        }

        // Assert
        await Assert.That(countAfterClear).IsEqualTo(0);
        await Assert.That(capacityAfter).IsEqualTo(capacityBefore);
        await Assert.That(countAfterAdd).IsEqualTo(1);
        await Assert.That(containsOld).IsFalse();
        await Assert.That(containsNew).IsTrue();
    }

    [Test]
    public async Task GetEnumerator_Foreach_YieldsEveryItemOnce()
    {
        // Arrange
        string[] expected = ["a", "b", "c"];
        var items = new List<string>();
        {
            using var set = new StackOnlyPooledHashSet<string>(4);
            foreach (var item in expected)
            {
                set.Add(item);
                set.Add(item);
            }

            // Act
            foreach (var item in set)
            {
                items.Add(item);
            }
        }

        // Assert
        await Assert.That(items.Order(StringComparer.Ordinal).ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task ToArray_WithItems_CopiesEveryItem()
    {
        // Arrange
        int[] expected = [5, 6, 7];
        int[] copy;
        {
            using var set = new StackOnlyPooledHashSet<int>(4);
            foreach (var item in expected)
            {
                set.Add(item);
            }

            // Act
            copy = set.ToArray();
        }

        // Assert
        await Assert.That(copy.Order().ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task ToArray_DefaultSet_ReturnsEmptyArray()
    {
        // Arrange
        StackOnlyPooledHashSet<int> set = default;

        // Act
        var copy = set.ToArray();
        set.Dispose();

        // Assert
        await Assert.That(copy.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_CalledTwice_LeavesEmptySet()
    {
        // Arrange
        const int Item = 1;
        var set = new StackOnlyPooledHashSet<int>(4);
        set.Add(Item);

        // Act
        set.Dispose();
        set.Dispose();
        var count = set.Count;
        var capacity = set.Capacity;

        // Assert
        await Assert.That(count).IsEqualTo(0);
        await Assert.That(capacity).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_DefaultSet_LeavesEmptySet()
    {
        // Arrange
        StackOnlyPooledHashSet<string> set = default;

        // Act
        set.Dispose();
        var count = set.Count;

        // Assert
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task UsingVar_Add_MutatesTheLocal()
    {
        // Arrange
        const int Item = 1;
        int count;
        bool contains;
        {
            using var set = new StackOnlyPooledHashSet<int>();

            // Act — a defensive copy of the using variable would leave Count at 0.
            set.Add(Item);
            count = set.Count;
            contains = set.Contains(Item);
        }

        // Assert
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(contains).IsTrue();
    }

    [Test]
    public async Task AddThroughRefParameter_GrowsPastInitialCapacity_CallerSeesEveryItem()
    {
        // Arrange
        const int ItemCount = 200;
        int count, missing;

        // A using variable can't be passed by ref (CS1657), so dispose in finally instead.
        var set = new StackOnlyPooledHashSet<int>(4);
        try
        {
            // Act
            AddItems(ref set, ItemCount);
            count = set.Count;
            missing = 0;
            for (var item = 0; item < ItemCount; item++)
            {
                if (!set.Contains(item))
                {
                    missing++;
                }
            }
        }
        finally
        {
            set.Dispose();
        }

        // Assert
        await Assert.That(count).IsEqualTo(ItemCount);
        await Assert.That(missing).IsEqualTo(0);
    }

    // The documented way to hand a stack-only set to a helper: by ref, never by value.
    static void AddItems(ref StackOnlyPooledHashSet<int> set, int itemCount)
    {
        for (var item = 0; item < itemCount; item++)
        {
            set.Add(item);
        }
    }
}
