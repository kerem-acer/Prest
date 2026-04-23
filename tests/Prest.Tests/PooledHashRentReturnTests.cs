namespace Prest.Tests;

public class PooledHashRentReturnTests
{
    [Test]
    public async Task HashMap_RentAfterReturn_ReusesInstance()
    {
        // Arrange — drain any leftover from another test.
        _ = PooledHashMap<int, int>.Create();

        // Act
        var first = PooledHashMap<int, int>.Create();
        first.Add(1, 10);
        PooledHashMap<int, int>.Return(first);

        var second = PooledHashMap<int, int>.Create();

        // Assert
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(second.Count).IsEqualTo(0);

        PooledHashMap<int, int>.Return(second);
    }

    [Test]
    public async Task HashMap_Dispose_ReturnsToThreadCache()
    {
        // Arrange — drain.
        _ = PooledHashMap<string, int>.Create();

        // Act — Dispose should cache the instance.
        var first = PooledHashMap<string, int>.Create();
        first.Add("a", 1);
        first.Dispose();

        var second = PooledHashMap<string, int>.Create();

        // Assert
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(second.Count).IsEqualTo(0);

        second.Dispose();
    }

    [Test]
    public async Task HashMap_RentWhenEmpty_AllocatesFresh()
    {
        // Arrange — drain.
        _ = PooledHashMap<string, string>.Create();

        // Act
        var map = PooledHashMap<string, string>.Create();

        // Assert — usable as a fresh map.
        map.Add("a", "A");
        await Assert.That(map.Count).IsEqualTo(1);

        map.Dispose();
    }

    [Test]
    public async Task HashSet_RentAfterReturn_ReusesInstance()
    {
        // Arrange
        _ = PooledHashSet<int>.Create();

        // Act
        var first = PooledHashSet<int>.Create();
        first.Add(42);
        PooledHashSet<int>.Return(first);

        var second = PooledHashSet<int>.Create();

        // Assert
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(second.Count).IsEqualTo(0);

        PooledHashSet<int>.Return(second);
    }

    [Test]
    public async Task HashSet_Dispose_ReturnsToThreadCache()
    {
        // Arrange
        _ = PooledHashSet<long>.Create();

        // Act
        var first = PooledHashSet<long>.Create();
        first.Add(7);
        first.Dispose();

        var second = PooledHashSet<long>.Create();

        // Assert
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(second.Count).IsEqualTo(0);

        second.Dispose();
    }

    [Test]
    public async Task HashSet_RentWhenEmpty_AllocatesFresh()
    {
        // Arrange
        _ = PooledHashSet<int>.Create();

        // Act
        var set = PooledHashSet<int>.Create();

        // Assert
        set.Add(1);
        await Assert.That(set.Count).IsEqualTo(1);

        set.Dispose();
    }
}
