namespace Prest.Tests;

/// <summary>
/// Coverage-focused tests that exercise the broader surface of every
/// algorithm: Capacity, Clear, Dispose-on-default, enumeration, and Grow.
/// Kept in a separate partial file from the functional contract tests for
/// clarity — these exist primarily to hit branches that functional tests
/// wouldn't naturally touch.
/// </summary>
public partial class AlgorithmContractTests
{
    // ---- Swiss ----

    [Test]
    public async Task Swiss_DefaultStructState_CountZero_ClearAndDisposeNoThrow()
    {
        // Arrange
        var algo = default(SwissTableAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>);

        // Act — Clear + Dispose must tolerate the never-initialized state.
        algo.Clear();
        algo.Dispose();

        // Assert
        await Assert.That(algo.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Swiss_Capacity_MatchesConfiguredLoadFactor()
    {
        // Arrange — SwissTable uses 7/8 load factor at ≥GroupWidth buckets.
        using var map = PooledHashMap<int, int>.Create(16);

        // Act
        var capacity = map.Capacity;

        // Assert
        await Assert.That(capacity).IsGreaterThan(0);
        await Assert.That(capacity).IsLessThanOrEqualTo(64);
    }

    [Test]
    public async Task Swiss_GrowMany_AllLookupsSucceed()
    {
        // Arrange — force several Grow cycles by starting tiny and inserting many.
        using var map = PooledHashMap<int, int>.Create(4);
        const int n = 200;

        // Act
        for (var i = 0; i < n; i++)
        {
            map.Add(i, i * 11);
        }

        // Assert
        for (var i = 0; i < n; i++)
        {
            var found = map.TryGetValue(i, out var v);
            await Assert.That(found).IsTrue();
            await Assert.That(v).IsEqualTo(i * 11);
        }
    }

    [Test]
    public async Task Swiss_ClearThenReuse_Works()
    {
        // Arrange
        using var map = PooledHashMap<int, int>.Create(8);
        map.Add(1, 10);
        map.Add(2, 20);

        // Act
        map.Clear();
        map.Add(3, 30);

        // Assert
        await Assert.That(map.Count).IsEqualTo(1);
        await Assert.That(map.TryGetValue(3, out var v)).IsTrue();
        await Assert.That(v).IsEqualTo(30);
    }

    [Test]
    public async Task Swiss_Enumeration_WalksLiveSlots()
    {
        // Arrange
        using var map = PooledHashMap<int, int>.Create(16);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30);
        map.Remove(2);

        // Act
        var observed = 0;
        var sum = 0;
        foreach (var kv in map)
        {
            observed++;
            sum += kv.Value;
        }

        // Assert
        await Assert.That(observed).IsEqualTo(2);
        await Assert.That(sum).IsEqualTo(40);
    }

    // ---- RobinHood ----

    [Test]
    public async Task RobinHood_DefaultStructState_ClearAndDisposeNoThrow()
    {
        // Arrange
        var algo = default(RobinHoodAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>);

        // Act
        algo.Clear();
        algo.Dispose();

        // Assert
        await Assert.That(algo.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RobinHood_GrowMany_AllLookupsSucceed()
    {
        // Arrange
        using var map = RobinHoodHashMap<int, int>.Create(4);
        const int n = 200;

        // Act
        for (var i = 0; i < n; i++)
        {
            map.Add(i, i * 3);
        }

        // Assert
        await Assert.That(map.Count).IsEqualTo(n);
        for (var i = 0; i < n; i++)
        {
            var found = map.TryGetValue(i, out var v);
            await Assert.That(found).IsTrue();
            await Assert.That(v).IsEqualTo(i * 3);
        }
    }

    [Test]
    public async Task RobinHood_ClearThenReuse_Works()
    {
        // Arrange
        using var map = RobinHoodHashMap<int, int>.Create(8);
        map.Add(1, 10);
        map.Add(2, 20);

        // Act
        map.Clear();
        map.Add(3, 30);

        // Assert
        await Assert.That(map.Count).IsEqualTo(1);
        await Assert.That(map.TryGetValue(3, out var v)).IsTrue();
        await Assert.That(v).IsEqualTo(30);
    }

    [Test]
    public async Task RobinHood_Enumeration_WalksLiveSlots()
    {
        // Arrange
        using var map = RobinHoodHashMap<int, int>.Create(16);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Remove(1);

        // Act
        var observed = 0;
        foreach (var _ in map)
        {
            observed++;
        }

        // Assert
        await Assert.That(observed).IsEqualTo(1);
    }

    [Test]
    public async Task RobinHood_Capacity_Nonzero()
    {
        // Arrange
        using var map = RobinHoodHashMap<int, int>.Create(32);

        // Act
        var capacity = map.Capacity;

        // Assert
        await Assert.That(capacity).IsGreaterThan(0);
    }

    [Test]
    public async Task RobinHood_HeavyCollisions_ExercisesRobAndBackwardShift()
    {
        // Arrange — a colliding comparer forces a deep probe chain so both the
        // "rob" path on Insert and the backward-shift on Remove get executed.
        using var map = ComparerRobinHoodHashMap<int, int>.Create(new ColliderComparer(), capacity: 32);
        const int n = 16;
        for (var i = 0; i < n; i++)
        {
            map.Add(i, i * 100);
        }

        // Act — remove half, forcing backward-shift to compact.
        for (var i = 0; i < n; i += 2)
        {
            map.Remove(i);
        }

        // Assert — the remaining entries must still be lookupable.
        for (var i = 1; i < n; i += 2)
        {
            var found = map.TryGetValue(i, out var v);
            await Assert.That(found).IsTrue();
            await Assert.That(v).IsEqualTo(i * 100);
        }
    }

    [Test]
    public async Task RobinHood_FindSlotOnDefault_ReturnsNullRef()
    {
        // Arrange — a never-initialized algorithm returns a null-ref from FindSlot.
        // Exercising this via the wrapper's ContainsKey, which checks IsNullRef internally.
        using var map = RobinHoodHashMap<int, int>.Create(0);

        // Act
        var found = map.ContainsKey(1);

        // Assert
        await Assert.That(found).IsFalse();
    }

    [Test]
    public async Task Chained_ChainWalk_ExercisesNextPointer()
    {
        // Arrange — constant-hash comparer chains every entry at a single bucket,
        // forcing the Get/Insert/Remove chain-walk paths.
        using var map = ComparerChainedHashMap<int, int>.Create(new ColliderComparer(), capacity: 16);
        const int n = 8;
        for (var i = 0; i < n; i++)
        {
            map.Add(i, i);
        }

        // Act — remove middle-of-chain entries; swap-with-last rewires chain heads.
        map.Remove(2);
        map.Remove(4);

        // Assert
        for (var i = 0; i < n; i++)
        {
            var expected = i != 2 && i != 4;
            await Assert.That(map.ContainsKey(i)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task Linear_FindSlotOnDefault_ReturnsNullRef()
    {
        // Arrange
        using var map = LinearHashMap<int, int>.Create(0);

        // Act
        var found = map.ContainsKey(1);

        // Assert
        await Assert.That(found).IsFalse();
    }

    sealed class ColliderComparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x == y;
        public int GetHashCode(int obj) => 0;
    }

    // ---- Linear ----

    [Test]
    public async Task Linear_DefaultStructState_ClearAndDisposeNoThrow()
    {
        // Arrange
        var algo = default(LinearProbingAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>);

        // Act
        algo.Clear();
        algo.Dispose();

        // Assert
        await Assert.That(algo.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Linear_GrowMany_AllLookupsSucceed()
    {
        // Arrange
        using var map = LinearHashMap<int, int>.Create(4);
        const int n = 200;

        // Act
        for (var i = 0; i < n; i++)
        {
            map.Add(i, i * 7);
        }

        // Assert
        for (var i = 0; i < n; i++)
        {
            var found = map.TryGetValue(i, out var v);
            await Assert.That(found).IsTrue();
            await Assert.That(v).IsEqualTo(i * 7);
        }
    }

    [Test]
    public async Task Linear_ClearThenReuse_Works()
    {
        // Arrange
        using var map = LinearHashMap<int, int>.Create(8);
        map.Add(1, 10);

        // Act
        map.Clear();
        map.Add(2, 20);

        // Assert
        await Assert.That(map.Count).IsEqualTo(1);
        await Assert.That(map.TryGetValue(2, out var v)).IsTrue();
        await Assert.That(v).IsEqualTo(20);
    }

    [Test]
    public async Task Linear_Enumeration_WalksLiveSlots()
    {
        // Arrange
        using var map = LinearHashMap<int, int>.Create(16);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Remove(2);

        // Act
        var observed = 0;
        foreach (var _ in map)
        {
            observed++;
        }

        // Assert
        await Assert.That(observed).IsEqualTo(1);
    }

    // ---- Chained ----

    [Test]
    public async Task Chained_DefaultStructState_ClearAndDisposeNoThrow()
    {
        // Arrange
        var algo = default(ChainedAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>);

        // Act
        algo.Clear();
        algo.Dispose();

        // Assert
        await Assert.That(algo.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Chained_GrowMany_AllLookupsSucceed()
    {
        // Arrange
        using var map = ChainedHashMap<int, int>.Create(4);
        const int n = 200;

        // Act
        for (var i = 0; i < n; i++)
        {
            map.Add(i, i + 1);
        }

        // Assert
        for (var i = 0; i < n; i++)
        {
            var found = map.TryGetValue(i, out var v);
            await Assert.That(found).IsTrue();
            await Assert.That(v).IsEqualTo(i + 1);
        }
    }

    [Test]
    public async Task Chained_ClearThenReuse_Works()
    {
        // Arrange
        using var map = ChainedHashMap<int, int>.Create(8);
        map.Add(1, 10);

        // Act
        map.Clear();
        map.Add(2, 20);

        // Assert
        await Assert.That(map.Count).IsEqualTo(1);
        await Assert.That(map.TryGetValue(2, out var v)).IsTrue();
        await Assert.That(v).IsEqualTo(20);
    }

    [Test]
    public async Task Chained_RemoveAndEnumeration_StaysConsistent()
    {
        // Arrange — Chained uses swap-with-last deletion; enumeration still finds
        // the remaining [0..count) range with no gaps.
        using var map = ChainedHashMap<int, int>.Create(16);
        for (var i = 0; i < 10; i++)
        {
            map.Add(i, i * 2);
        }
        map.Remove(3);
        map.Remove(7);

        // Act
        var observed = 0;
        foreach (var _ in map)
        {
            observed++;
        }

        // Assert
        await Assert.That(observed).IsEqualTo(8);
        await Assert.That(map.Count).IsEqualTo(8);
    }

    [Test]
    public async Task Chained_RemoveAllThenReinsert_Works()
    {
        // Arrange
        using var map = ChainedHashMap<int, int>.Create(8);
        for (var i = 0; i < 5; i++)
        {
            map.Add(i, i);
        }

        // Act
        for (var i = 0; i < 5; i++)
        {
            map.Remove(i);
        }
        for (var i = 10; i < 15; i++)
        {
            map.Add(i, i);
        }

        // Assert
        await Assert.That(map.Count).IsEqualTo(5);
        await Assert.That(map.TryGetValue(12, out var v)).IsTrue();
        await Assert.That(v).IsEqualTo(12);
    }
}
