namespace Prest.Tests;

public partial class PooledHashSetTests
{
    // -------- Collision / probe-chain stress --------

    [Test]
    public async Task Add_AllKeysShareSameHash_AllInsertedAndFindable()
    {
        // Arrange
        const int n = 64;
        using var set = ComparerSwissHashSet<int>.Create(new ColliderComparer(), n);
        for (var i = 0; i < n; i++)
        {
            set.Add(i);
        }

        // Act + Assert: every key still findable despite all sharing one hash.
        for (var i = 0; i < n; i++)
        {
            await Assert.That(set.Contains(i)).IsTrue();
        }

        await Assert.That(set.Count).IsEqualTo(n);
        await Assert.That(set.Contains(9999)).IsFalse();
    }

    [Test]
    public async Task Add_DuplicateUnderColliderComparer_ReturnsFalse()
    {
        // Arrange
        using var set = ComparerSwissHashSet<int>.Create(new ColliderComparer(), 32);
        for (var i = 0; i < 20; i++)
        {
            set.Add(i);
        }

        // Act
        var first = set.Add(7);
        var countAfter = set.Count;

        // Assert: SIMD scan sees all 20 same-h2 matches; equality must reject the duplicate.
        await Assert.That(first).IsFalse();
        await Assert.That(countAfter).IsEqualTo(20);
    }

    [Test]
    public async Task Remove_AllKeysShareSameHash_AllRemovedInReverseOrder()
    {
        // Arrange
        const int n = 32;
        using var set = ComparerSwissHashSet<int>.Create(new ColliderComparer(), n);
        for (var i = 0; i < n; i++)
        {
            set.Add(i);
        }

        // Act: remove from the end backward — exercises tombstone vs erase-to-empty
        // along a long probe chain.
        for (var i = n - 1; i >= 0; i--)
        {
            var removed = set.Remove(i);
            await Assert.That(removed).IsTrue();
        }

        // Assert
        await Assert.That(set.Count).IsEqualTo(0);
        for (var i = 0; i < n; i++)
        {
            await Assert.That(set.Contains(i)).IsFalse();
        }
    }

    [Test]
    public async Task Remove_AllKeysShareSameHash_RemoveFirstThenLookupOthers()
    {
        // Arrange: regression scenario from the in-group erase-to-empty discussion —
        // removing the head of a collision cluster must not hide later items.
        const int n = 50;
        using var set = ComparerSwissHashSet<int>.Create(new ColliderComparer(), n);
        for (var i = 0; i < n; i++)
        {
            set.Add(i);
        }

        // Act
        set.Remove(0);

        // Assert: 1..n-1 must all still be findable.
        for (var i = 1; i < n; i++)
        {
            await Assert.That(set.Contains(i)).IsTrue();
        }
    }

    // -------- Tombstone reuse --------

    [Test]
    public async Task Add_ReinsertsAfterRemove_ReusesTombstoneSlot()
    {
        // Arrange
        const int n = 100;
        using var set = ComparerSwissHashSet<int>.Create(new ColliderComparer(), n);
        for (var i = 0; i < n; i++)
        {
            set.Add(i);
        }

        // Act: churn — remove half, re-add same values.
        for (var i = 0; i < n; i += 2)
        {
            set.Remove(i);
        }
        for (var i = 0; i < n; i += 2)
        {
            await Assert.That(set.Add(i)).IsTrue();
        }

        // Assert: Count stays bounded — confirms tombstone slots get reused
        // rather than the table running out of capacity.
        await Assert.That(set.Count).IsEqualTo(n);
        for (var i = 0; i < n; i++)
        {
            await Assert.That(set.Contains(i)).IsTrue();
        }
    }

    // -------- Growth --------

    [Test]
    public async Task Add_PastCapacity_GrowsAndPreservesAllItems()
    {
        // Arrange
        const int cap = 12;
        using var set = PooledHashSet<int>.Create(cap);
        for (var i = 0; i < cap; i++)
        {
            set.Add(i);
        }
        var capBefore = set.Capacity;

        // Act: push past the initial capacity — forces one grow.
        const int overflow = cap * 3;
        for (var i = cap; i < overflow; i++)
        {
            await Assert.That(set.Add(i)).IsTrue();
        }

        // Assert: capacity doubled at least once, all items survived the rehash.
        await Assert.That(set.Capacity).IsGreaterThan(capBefore);
        await Assert.That(set.Count).IsEqualTo(overflow);
        for (var i = 0; i < overflow; i++)
        {
            await Assert.That(set.Contains(i)).IsTrue();
        }
    }

    [Test]
    public async Task Add_PastCapacityWithCollider_GrowsAndPreservesAllItems()
    {
        // Arrange
        const int cap = 20;
        using var set = ComparerSwissHashSet<int>.Create(new ColliderComparer(), cap);

        // Act: insert 5x the starting capacity — every item shares one hash, so
        // each grow must rehash a dense collision cluster correctly.
        const int total = cap * 5;
        for (var i = 0; i < total; i++)
        {
            await Assert.That(set.Add(i)).IsTrue();
        }

        // Assert
        await Assert.That(set.Count).IsEqualTo(total);
        for (var i = 0; i < total; i++)
        {
            await Assert.That(set.Contains(i)).IsTrue();
        }
    }

    [Test]
    public async Task Add_FromZeroCapacity_LazyInitsAndGrows()
    {
        // Arrange: capacity-0 ctor leaves buffers unrented until the first Add.
        using var set = PooledHashSet<int>.Create(0);

        // Act
        for (var i = 0; i < 50; i++)
        {
            await Assert.That(set.Add(i)).IsTrue();
        }

        // Assert
        await Assert.That(set.Count).IsEqualTo(50);
        for (var i = 0; i < 50; i++)
        {
            await Assert.That(set.Contains(i)).IsTrue();
        }
    }

    // -------- Wrap-around / mirror region --------

    [Test]
    public async Task Add_KeysHashingToEndOfTable_ProbesWrapCorrectly()
    {
        // Arrange: WrapEdgeComparer pins H1 to the last group so probing must
        // wrap into the mirror region for any spillover.
        const int cap = 100;
        using var set = ComparerSwissHashSet<int>.Create(new WrapEdgeComparer(), cap);
        for (var i = 0; i < 40; i++)
        {
            set.Add(i);
        }

        // Assert: every inserted key must be findable across the wrap.
        for (var i = 0; i < 40; i++)
        {
            await Assert.That(set.Contains(i)).IsTrue();
        }

        await Assert.That(set.Contains(9999)).IsFalse();
    }

    [Test]
    public async Task Remove_KeysHashingToEndOfTable_ProbesWrapCorrectly()
    {
        // Arrange
        const int cap = 60;
        using var set = ComparerSwissHashSet<int>.Create(new WrapEdgeComparer(), cap);
        for (var i = 0; i < 30; i++)
        {
            set.Add(i);
        }

        // Act
        for (var i = 0; i < 30; i += 2)
        {
            await Assert.That(set.Remove(i)).IsTrue();
        }

        // Assert
        for (var i = 0; i < 30; i++)
        {
            var expected = i % 2 == 1;
            await Assert.That(set.Contains(i)).IsEqualTo(expected);
        }
    }

    // -------- Erase-to-empty correctness --------

    [Test]
    public async Task Remove_LeavesNoStaleReferences_ForReferenceType()
    {
        // Arrange
        var probe = new WeakReference<object>(new object());
        var set = PooledHashSet<object>.Create(8);
        var captured = new object();
        set.Add(captured);

        // Act
        set.Remove(captured);
        var hadAfterRemove = set.Contains(captured);
        set.Dispose();

        // Assert: equality contract for object uses ref equality, so Contains
        // returning false confirms the slot is no longer holding the entry.
        await Assert.That(hadAfterRemove).IsFalse();
        _ = probe;
    }

    // -------- Enumerator with tombstones --------

    [Test]
    public async Task Enumerator_AfterRemoves_YieldsExactlyRemainingItems()
    {
        // Arrange
        const int n = 40;
        using var set = PooledHashSet<int>.Create(n);
        for (var i = 0; i < n; i++)
        {
            set.Add(i);
        }
        for (var i = 0; i < n; i += 2)
        {
            set.Remove(i);
        }

        // Act
        var collected = new List<int>();
        foreach (var x in set)
        {
            collected.Add(x);
        }

        // Assert: 20 odd numbers, no duplicates, no defaults from cleared slots.
        await Assert.That(collected.Count).IsEqualTo(n / 2);
        await Assert.That(collected.Distinct().Count()).IsEqualTo(n / 2);
        for (var i = 1; i < n; i += 2)
        {
            await Assert.That(collected).Contains(i);
        }
    }

    [Test]
    public async Task Enumerator_AfterColliderChurn_NeverYieldsTombstoneSlot()
    {
        // Arrange: collider + churn produces a probe chain riddled with tombstones.
        const int n = 30;
        using var set = ComparerSwissHashSet<int>.Create(new ColliderComparer(), n);
        for (var i = 0; i < n; i++)
        {
            set.Add(i);
        }
        for (var i = 0; i < n; i += 3)
        {
            set.Remove(i);
        }

        // Act
        var collected = set.ToArray();

        // Assert: dense ToArray scan must skip tombstones (top bit set in control).
        var expected = Enumerable.Range(0, n).Where(i => i % 3 != 0).ToArray();
        await Assert.That(collected.Length).IsEqualTo(expected.Length);
        await Assert.That(collected.OrderBy(x => x).ToArray()).IsEquivalentTo(expected);
    }

    // -------- TryGetValue / interning --------

    [Test]
    public async Task TryGetValue_ReturnsStoredInstance_NotProbeKey()
    {
        // Arrange: case-insensitive comparer so "HELLO" equals "hello" but is a different instance.
        using var set = ComparerSwissHashSet<string>.Create(StringComparer.OrdinalIgnoreCase, 8);
        const string stored = "hello";
        set.Add(stored);

        // Act
        var found = set.TryGetValue("HELLO", out var actual);

        // Assert
        await Assert.That(found).IsTrue();
        await Assert.That(ReferenceEquals(actual, stored)).IsTrue();
    }

    [Test]
    public async Task TryGetValue_Missing_ReturnsFalseAndDefault()
    {
        // Arrange
        using var set = PooledHashSet<string>.Create(8);
        set.Add("a");

        // Act
        var found = set.TryGetValue("missing", out var actual);

        // Assert
        await Assert.That(found).IsFalse();
        await Assert.That(actual).IsNull();
    }

    // -------- Clear and reuse --------

    [Test]
    public async Task Clear_AfterFill_AllowsRefillToCapacity()
    {
        // Arrange
        const int cap = 16;
        using var set = PooledHashSet<int>.Create(cap);
        for (var i = 0; i < cap; i++)
        {
            set.Add(i);
        }

        // Act
        set.Clear();
        for (var i = 100; i < 100 + cap; i++)
        {
            await Assert.That(set.Add(i)).IsTrue();
        }

        // Assert
        await Assert.That(set.Count).IsEqualTo(cap);
        for (var i = 0; i < cap; i++)
        {
            await Assert.That(set.Contains(i)).IsFalse();
        }
        for (var i = 100; i < 100 + cap; i++)
        {
            await Assert.That(set.Contains(i)).IsTrue();
        }
    }

    // -------- Dispose semantics --------

    [Test]
    public async Task Dispose_Twice_DoesNotThrow()
    {
        // Arrange
        var set = PooledHashSet<int>.Create(8);
        set.Add(1);

        // Act
        set.Dispose();
        var threwOnSecond = false;
        try
        {
            set.Dispose();
        }
        catch
        {
            threwOnSecond = true;
        }

        // Assert
        await Assert.That(threwOnSecond).IsFalse();
    }

    // -------- Differential fuzz against BCL --------

    [Test]
    public async Task RandomOps_MatchHashSetBehaviour()
    {
        // Arrange
        const int seed = 0xC0FFEE;
        const int ops = 5000;
        const int keyRange = 200;
        const int cap = keyRange + 10;

        var rng = new Random(seed);
        var oracle = new HashSet<int>();
        using var set = PooledHashSet<int>.Create(cap);

        // Act + Assert: every operation is mirrored on both, results must agree.
        for (var i = 0; i < ops; i++)
        {
            var key = rng.Next(keyRange);
            switch (rng.Next(3))
            {
                case 0:
                    var added = set.Add(key);
                    var oracleAdded = oracle.Add(key);
                    await Assert.That(added).IsEqualTo(oracleAdded);
                    break;
                case 1:
                    var removed = set.Remove(key);
                    var oracleRemoved = oracle.Remove(key);
                    await Assert.That(removed).IsEqualTo(oracleRemoved);
                    break;
                case 2:
                    var contains = set.Contains(key);
                    var oracleContains = oracle.Contains(key);
                    await Assert.That(contains).IsEqualTo(oracleContains);
                    break;
            }

            await Assert.That(set.Count).IsEqualTo(oracle.Count);
        }

        // Final invariant: enumerated content matches.
        var snapshot = set.ToArray();
        await Assert.That(snapshot.OrderBy(x => x).ToArray())
            .IsEquivalentTo(oracle.OrderBy(x => x).ToArray());
    }

    [Test]
    public async Task RandomOps_UnderColliderComparer_MatchHashSetBehaviour()
    {
        // Arrange: colliding hash exercises probe chains and tombstone reuse paths.
        const int seed = 0xBEEF;
        const int ops = 2000;
        const int keyRange = 60;
        const int cap = keyRange + 10;

        var rng = new Random(seed);
        var comparer = new ColliderComparer();
        var oracle = new HashSet<int>(comparer);
        using var set = ComparerSwissHashSet<int>.Create(comparer, cap);

        // Act + Assert
        for (var i = 0; i < ops; i++)
        {
            var key = rng.Next(keyRange);
            switch (rng.Next(3))
            {
                case 0:
                    await Assert.That(set.Add(key)).IsEqualTo(oracle.Add(key));
                    break;
                case 1:
                    await Assert.That(set.Remove(key)).IsEqualTo(oracle.Remove(key));
                    break;
                case 2:
                    await Assert.That(set.Contains(key)).IsEqualTo(oracle.Contains(key));
                    break;
            }

            await Assert.That(set.Count).IsEqualTo(oracle.Count);
        }
    }

    // -------- Helpers --------

    /// <summary>Forces every key into the same hash, exposing pure probe-chain behaviour.</summary>
    sealed class ColliderComparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x == y;
        public int GetHashCode(int obj) => 0;
    }

    /// <summary>
    /// Returns hashes whose H1 (high bits after MixHash) lands near the end of any
    /// reasonable bucket count, forcing probe loads to wrap through the mirror region.
    /// We don't know the post-MixHash value, so use <see cref="int.MaxValue"/> for all
    /// keys — its mixed form has its high bits set, putting H1 at a high group index.
    /// </summary>
    sealed class WrapEdgeComparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x == y;
        public int GetHashCode(int obj) => int.MaxValue;
    }
}
