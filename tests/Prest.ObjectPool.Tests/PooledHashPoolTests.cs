namespace Prest.ObjectPool.Tests;

public class PooledHashPoolTests
{
    [Test]
    [NotInParallel("PooledHashMapPool<int,int>")]
    public async Task HashMapPool_RentAfterReturn_ReusesInstance()
    {
        // Act
        var first = PooledHashMapPool<int, int>.Create();
        first.Add(1, 10);
        PooledHashMapPool<int, int>.Return(first);

        var second = PooledHashMapPool<int, int>.Create();

        // Assert — DefaultObjectPool is LIFO so the most recently returned instance comes back.
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(second.Count).IsEqualTo(0);

        PooledHashMapPool<int, int>.Return(second);
    }

    [Test]
    [NotInParallel("PooledHashMapPool<int,string>")]
    public async Task HashMapPool_Dispose_ReturnsToPool()
    {
        // Act — Dispose should route the instance back to the pool (via the subclass override).
        var first = PooledHashMapPool<int, string>.Create();
        first.Add(42, "hello");
        first.Dispose();

        var second = PooledHashMapPool<int, string>.Create();

        // Assert
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(second.Count).IsEqualTo(0);

        second.Dispose();
    }

    [Test]
    [NotInParallel("PooledHashMapPool<string,int>")]
    public async Task HashMapPool_Rent_UsableAfterAdd()
    {
        // Arrange
        var map = PooledHashMapPool<string, int>.Create();

        // Act
        map.Add("x", 1);
        map.Add("y", 2);

        // Assert
        await Assert.That(map.Count).IsEqualTo(2);
        await Assert.That(map["x"]).IsEqualTo(1);

        map.Dispose();
    }

    [Test]
    [NotInParallel("PooledHashSetPool<int>")]
    public async Task HashSetPool_RentAfterReturn_ReusesInstance()
    {
        var first = PooledHashSetPool<int>.Create();
        first.Add(99);
        PooledHashSetPool<int>.Return(first);

        var second = PooledHashSetPool<int>.Create();

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(second.Count).IsEqualTo(0);

        PooledHashSetPool<int>.Return(second);
    }

    [Test]
    [NotInParallel("PooledHashSetPool<long>")]
    public async Task HashSetPool_Dispose_ReturnsToPool()
    {
        var first = PooledHashSetPool<long>.Create();
        first.Add(7);
        first.Dispose();

        var second = PooledHashSetPool<long>.Create();

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(second.Count).IsEqualTo(0);

        second.Dispose();
    }

    [Test]
    [NotInParallel("PooledHashSetPool<int>")]
    public async Task HashSetPool_Rent_UsableAfterAdd()
    {
        var set = PooledHashSetPool<int>.Create();

        set.Add(1);
        set.Add(2);
        set.Add(1);

        await Assert.That(set.Count).IsEqualTo(2);
        await Assert.That(set.Contains(1)).IsTrue();

        set.Dispose();
    }
}
