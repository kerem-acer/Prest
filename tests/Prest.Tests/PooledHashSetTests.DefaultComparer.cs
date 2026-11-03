namespace Prest.Tests;

/// <summary>
/// <see cref="PooledHashSet{T}" /> takes an optional comparer, like <see cref="HashSet{T}" />.
/// </summary>
public partial class PooledHashSetTests
{
    [Test]
    public async Task Create_WithComparer_UsesItForEquality()
    {
        // Arrange
        const string Item = "Hello";
        const string OtherCaseItem = "HELLO";
        var comparer = StringComparer.OrdinalIgnoreCase;
        using var set = PooledHashSet<string>.Create(comparer, capacity: 8);

        // Act
        var added = set.Add(Item);
        var addedAgain = set.Add(OtherCaseItem);
        var contains = set.Contains(OtherCaseItem);

        // Assert
        await Assert.That(added).IsTrue();
        await Assert.That(addedAgain).IsFalse();
        await Assert.That(contains).IsTrue();
        await Assert.That(set.Count).IsEqualTo(1);
        await Assert.That(set.Comparer).IsSameReferenceAs(comparer);
    }

    [Test]
    public async Task Create_NullOrDefaultComparer_UsesDefaultComparer()
    {
        // Arrange
        using var withoutComparer = PooledHashSet<string>.Create();
        using var withNull = PooledHashSet<string>.Create(null);
        using var withDefault = PooledHashSet<string>.Create(EqualityComparer<string>.Default);

        // Act
        var comparers = new[] { withoutComparer.Comparer, withNull.Comparer, withDefault.Comparer };

        // Assert
        foreach (var comparer in comparers)
        {
            await Assert.That(comparer).IsSameReferenceAs(EqualityComparer<string>.Default);
        }
    }

    [Test]
    public async Task Create_AfterDispose_ReusesCachedSetOnlyForTheSameComparer()
    {
        // Arrange
        var comparer = StringComparer.OrdinalIgnoreCase;
        PooledHashSet<string>.Create(comparer).Dispose(); // takes whatever another test left in the cache

        // Act
        var first = PooledHashSet<string>.Create(comparer);
        first.Dispose();
        var sameComparer = PooledHashSet<string>.Create(comparer);
        sameComparer.Dispose();
        var defaultComparerSet = PooledHashSet<string>.Create();
        defaultComparerSet.Dispose();

        // Assert
        await Assert.That(sameComparer).IsSameReferenceAs(first);
        await Assert.That(defaultComparerSet).IsNotSameReferenceAs(sameComparer);
        await Assert.That(defaultComparerSet.Comparer).IsSameReferenceAs(EqualityComparer<string>.Default);
    }

    [Test]
    public async Task CollectionExpression_WithComparer_UsesComparer()
    {
        // Arrange
        const string Item = "a";
        const string OtherCaseItem = "A";
        const int Capacity = 64;

        // Act
        PooledHashSet<string> set = [with(StringComparer.OrdinalIgnoreCase), Item, OtherCaseItem];
        PooledHashSet<string> sized = [with(StringComparer.OrdinalIgnoreCase, Capacity)];
        using (set)
        using (sized)
        {
            // Assert
            await Assert.That(set.Count).IsEqualTo(1);
            await Assert.That(set.Contains(OtherCaseItem)).IsTrue();
            await Assert.That(sized.Capacity).IsGreaterThanOrEqualTo(Capacity);
            await Assert.That(sized.Comparer).IsSameReferenceAs(StringComparer.OrdinalIgnoreCase);
        }
    }

    [Test]
    public async Task GetAlternateLookup_DefaultAliasWithSpanComparer_FindsBySpan()
    {
        // Arrange
        const string Item = "token";
        var comparer = new CountingSpanComparer();
        using var set = PooledHashSet<string>.Create(comparer);
        var lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();

        // Act
        var added = lookup.Add(Item.AsSpan());
        var addedAgain = lookup.Add(Item.AsSpan());
        var contains = lookup.Contains(Item.AsSpan());

        // Assert
        await Assert.That(added).IsTrue();
        await Assert.That(addedAgain).IsFalse();
        await Assert.That(contains).IsTrue();
        await Assert.That(comparer.CreateCount).IsEqualTo(1);
    }

    [Test]
    public async Task SwissHashSet_NotDerivedFromDefaultAlias_KeepsTheComparerFreeHasher()
    {
        // Arrange — SwissHashSet<T> stays on EqualityDefaultHasher so it pays nothing for comparer support.
        var swiss = typeof(SwissHashSet<int>);

        // Act
        var derivesFromDefault = typeof(PooledHashSet<int>).IsAssignableFrom(swiss);
        var swissBase = swiss.BaseType;

        // Assert
        await Assert.That(derivesFromDefault).IsFalse();
        await Assert.That(swissBase).IsEqualTo(
            typeof(PooledHashSet<int, SwissTableAlgorithm<int, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>));
    }
}
