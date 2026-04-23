using System.Buffers;

namespace Prest.Tests;

/// <summary>
/// Additional coverage for less-frequented paths: constructors taking
/// rented arrays, Capacity/IsEmpty properties, the disposed/lazy-init
/// branches of TryAdd, KeyCollection/ValueCollection <c>Count</c>,
/// and the IDisposable cascading Dispose path.
/// </summary>
public partial class PooledHashMapTests
{
    [Test]
    public async Task Capacity_ReflectsAlgorithmCapacity()
    {
        // Arrange
        using var map = PooledHashMap<int, int>.Create(capacity: 64);

        // Act
        var capacity = map.Capacity;

        // Assert
        await Assert.That(capacity).IsGreaterThan(0);
    }

    [Test]
    public async Task Capacity_OnUnallocatedMap_IsZero()
    {
        // Arrange — consume the cache slot without returning, so Create(0) allocates a fresh map.
        _ = PooledHashMap<int, int>.Create(0);
        using var map = PooledHashMap<int, int>.Create(0);

        // Act
        var capacity = map.Capacity;

        // Assert
        await Assert.That(capacity).IsEqualTo(0);
    }

    [Test]
    public async Task TryAdd_OnZeroCapacityMap_LazyInitializes()
    {
        // Arrange — capacity 0 means no allocation yet; first TryAdd must init.
        using var map = PooledHashMap<int, int>.Create(0);

        // Act
        var added = map.TryAdd(1, 10);
        var count = map.Count;

        // Assert
        await Assert.That(added).IsTrue();
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Clear_ResetsCountWithoutFreeingBuffers()
    {
        // Arrange
        using var map = PooledHashMap<int, int>.Create(8);
        map.Add(1, 1);
        map.Add(2, 2);

        // Act
        map.Clear();
        var countAfterClear = map.Count;
        map.Add(3, 3);
        var countAfterReuse = map.Count;

        // Assert
        await Assert.That(countAfterClear).IsEqualTo(0);
        await Assert.That(countAfterReuse).IsEqualTo(1);
    }

    [Test]
    public async Task KeyCollection_Count_MatchesMapCount()
    {
        // Arrange
        const int expected = 3;
        using var map = PooledHashMap<int, int>.Create(8);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30);

        // Act
        var count = map.Keys.Count;

        // Assert
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task ValueCollection_Count_MatchesMapCount()
    {
        // Arrange
        const int expected = 3;
        using var map = PooledHashMap<int, int>.Create(8);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30);

        // Act
        var count = map.Values.Count;

        // Assert
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task RentedArraysCtor_BuildsMapFromRentedData()
    {
        // Arrange
        const int n = 3;
        var keys = ArrayPool<int>.Shared.Rent(n);
        var values = ArrayPool<int>.Shared.Rent(n);
        keys[0] = 1; keys[1] = 2; keys[2] = 3;
        values[0] = 10; values[1] = 20; values[2] = 30;

        // Act
        using var map = PooledHashMap<int, int>.Create(keys, values, n);

        // Assert
        await Assert.That(map.Count).IsEqualTo(n);
        await Assert.That(map[1]).IsEqualTo(10);
        await Assert.That(map[2]).IsEqualTo(20);
        await Assert.That(map[3]).IsEqualTo(30);
    }

    [Test]
    public async Task RentedArraysCtor_WithZeroCount_ReturnsArraysAndProducesEmptyMap()
    {
        // Arrange
        var keys = ArrayPool<int>.Shared.Rent(4);
        var values = ArrayPool<int>.Shared.Rent(4);

        // Act
        using var map = PooledHashMap<int, int>.Create(keys, values, count: 0);

        // Assert
        await Assert.That(map.Count).IsEqualTo(0);
    }

    [Test]
    public void RentedArraysCtor_DuplicateKeys_Throws()
    {
        // Arrange: two entries share key "1" — the ctor's Insert path must throw.
        const int n = 2;
        var keys = ArrayPool<int>.Shared.Rent(n);
        var values = ArrayPool<int>.Shared.Rent(n);
        keys[0] = 1; keys[1] = 1;
        values[0] = 10; values[1] = 20;

        // Act + Assert
        Assert.Throws<ArgumentException>(() => _ = PooledHashMap<int, int>.Create(keys, values, n));
    }

    [Test]
    public async Task Dispose_WithDisposableValues_InvokesChildDispose()
    {
        // Arrange
        var v1 = new DisposableCounter();
        var v2 = new DisposableCounter();
        var map = PooledHashMap<int, DisposableCounter>.Create(4);
        map.Add(1, v1);
        map.Add(2, v2);

        // Act
        map.Dispose();

        // Assert
        await Assert.That(v1.DisposedCount).IsEqualTo(1);
        await Assert.That(v2.DisposedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Dispose_WithDisposableKeys_InvokesChildDispose()
    {
        // Arrange
        var k1 = new DisposableKey(1);
        var k2 = new DisposableKey(2);
        var map = PooledHashMap<DisposableKey, int>.Create(4);
        map.Add(k1, 10);
        map.Add(k2, 20);

        // Act
        map.Dispose();

        // Assert
        await Assert.That(k1.Counter.DisposedCount).IsEqualTo(1);
        await Assert.That(k2.Counter.DisposedCount).IsEqualTo(1);
    }

    sealed class DisposableCounter : IDisposable
    {
        public int DisposedCount { get; private set; }
        public void Dispose() => DisposedCount++;
    }

    sealed class DisposableKey : IDisposable, IEquatable<DisposableKey>
    {
        public int Id { get; }
        public DisposableCounter Counter { get; } = new();

        public DisposableKey(int id) => Id = id;

        public void Dispose() => Counter.Dispose();

        public bool Equals(DisposableKey? other) => other is not null && other.Id == Id;
        public override bool Equals(object? obj) => obj is DisposableKey dk && Equals(dk);
        public override int GetHashCode() => Id;
    }
}
