using System.Buffers;

namespace Prest.Tests;

/// <summary>
/// Sanity checks that each named alias type (<c>PooledHashMap&lt;K,V&gt;</c>,
/// <c>SwissHashMap</c>, <c>RobinHoodHashMap</c>, …) closes over the intended
/// algorithm and behaves like the underlying generic.
/// </summary>
public class DefaultTypeAliasesTests
{
    // ---- Map aliases ----

    [Test]
    public async Task PooledHashMap_DefaultCtor_RoundTrip()
    {
        // Arrange
        using var map = PooledHashMap<int, string>.Create(4);

        // Act
        map.Add(1, "one");
        var found = map.TryGetValue(1, out var value);

        // Assert
        await Assert.That(found).IsTrue();
        await Assert.That(value).IsEqualTo("one");
    }

    [Test]
    public async Task PooledHashMap_RentedArraysCtor_Works()
    {
        // Arrange
        const int n = 2;
        var keys = ArrayPool<int>.Shared.Rent(n);
        var values = ArrayPool<string>.Shared.Rent(n);
        keys[0] = 1; keys[1] = 2;
        values[0] = "a"; values[1] = "b";

        // Act
        using var map = PooledHashMap<int, string>.Create(keys, values, n);

        // Assert
        await Assert.That(map.Count).IsEqualTo(n);
        await Assert.That(map[1]).IsEqualTo("a");
        await Assert.That(map[2]).IsEqualTo("b");
    }

    [Test]
    public async Task SwissHashMap_RoundTrip()
    {
        // Arrange
        using var map = SwissHashMap<int, int>.Create(8);

        // Act
        for (var i = 0; i < 5; i++)
        {
            map.Add(i, i * 10);
        }

        // Assert
        for (var i = 0; i < 5; i++)
        {
            await Assert.That(map[i]).IsEqualTo(i * 10);
        }
    }

    [Test]
    public async Task RobinHoodHashMap_RoundTrip()
    {
        // Arrange
        using var map = RobinHoodHashMap<int, int>.Create(8);

        // Act
        for (var i = 0; i < 5; i++)
        {
            map.Add(i, i * 10);
        }

        // Assert
        for (var i = 0; i < 5; i++)
        {
            await Assert.That(map[i]).IsEqualTo(i * 10);
        }
    }

    [Test]
    public async Task LinearHashMap_RoundTrip()
    {
        // Arrange
        using var map = LinearHashMap<int, int>.Create(8);

        // Act
        for (var i = 0; i < 5; i++)
        {
            map.Add(i, i * 10);
        }

        // Assert
        for (var i = 0; i < 5; i++)
        {
            await Assert.That(map[i]).IsEqualTo(i * 10);
        }
    }

    [Test]
    public async Task ChainedHashMap_RoundTrip()
    {
        // Arrange
        using var map = ChainedHashMap<int, int>.Create(8);

        // Act
        for (var i = 0; i < 5; i++)
        {
            map.Add(i, i * 10);
        }

        // Assert
        for (var i = 0; i < 5; i++)
        {
            await Assert.That(map[i]).IsEqualTo(i * 10);
        }
    }

    // ---- Set aliases ----

    [Test]
    public async Task PooledHashSet_DefaultCtor_RoundTrip()
    {
        // Arrange
        using var set = PooledHashSet<int>.Create(8);

        // Act
        set.Add(1);
        set.Add(2);
        set.Add(1);

        // Assert
        await Assert.That(set.Count).IsEqualTo(2);
        await Assert.That(set.Contains(1)).IsTrue();
        await Assert.That(set.Contains(3)).IsFalse();
    }

    [Test]
    public async Task SwissHashSet_RoundTrip()
    {
        // Arrange
        using var set = SwissHashSet<string>.Create(4);

        // Act
        set.Add("a");
        set.Add("b");

        // Assert
        await Assert.That(set.Count).IsEqualTo(2);
        await Assert.That(set.Contains("a")).IsTrue();
    }

    [Test]
    public async Task RobinHoodHashSet_RoundTrip()
    {
        // Arrange
        using var set = RobinHoodHashSet<int>.Create(8);

        // Act
        for (var i = 0; i < 5; i++)
        {
            set.Add(i);
        }

        // Assert
        await Assert.That(set.Count).IsEqualTo(5);
    }

    [Test]
    public async Task LinearHashSet_RoundTrip()
    {
        // Arrange
        using var set = LinearHashSet<int>.Create(8);

        // Act
        set.Add(42);

        // Assert
        await Assert.That(set.Contains(42)).IsTrue();
    }

    [Test]
    public async Task ChainedHashSet_RoundTrip()
    {
        // Arrange
        using var set = ChainedHashSet<int>.Create(8);

        // Act
        set.Add(42);

        // Assert
        await Assert.That(set.Contains(42)).IsTrue();
    }

    // ---- Inheritance check: all derive from the generic base ----

    [Test]
    public async Task PooledHashMap_DerivesFromSwissGeneric()
    {
        // Arrange
        using var derived = PooledHashMap<int, int>.Create(4);

        // Act
        PooledHashMap<int, int, SwissTableAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>> baseRef = derived;

        // Assert
        await Assert.That(baseRef).IsNotNull();
    }

    // ---- Finalizer combos (one sanity check per algorithm/finalizer matrix) ----

    [Test]
    public async Task SwissFibonacciHashMap_RoundTrip()
    {
        using var map = SwissHashMap<int, int, FibonacciFinalizer>.Create(32);
        for (var i = 0; i < 10; i++) { map.Add(i, i); }
        await Assert.That(map.Count).IsEqualTo(10);
    }

    [Test]
    public async Task SwissLowbias32HashMap_RoundTrip()
    {
        using var map = SwissHashMap<int, int, Lowbias32Finalizer>.Create(32);
        for (var i = 0; i < 10; i++) { map.Add(i, i); }
        await Assert.That(map.Count).IsEqualTo(10);
    }

    [Test]
    public async Task SwissXmxHashMap_RoundTrip()
    {
        using var map = SwissHashMap<int, int, XmxFinalizer>.Create(32);
        for (var i = 0; i < 10; i++) { map.Add(i, i); }
        await Assert.That(map.Count).IsEqualTo(10);
    }

    [Test]
    public async Task RobinHoodFibonacciHashMap_RoundTrip()
    {
        using var map = RobinHoodHashMap<int, int, FibonacciFinalizer>.Create(32);
        for (var i = 0; i < 10; i++) { map.Add(i, i); }
        await Assert.That(map.Count).IsEqualTo(10);
    }

    [Test]
    public async Task RobinHoodLowbias32HashMap_RoundTrip()
    {
        using var map = RobinHoodHashMap<int, int, Lowbias32Finalizer>.Create(32);
        for (var i = 0; i < 10; i++) { map.Add(i, i); }
        await Assert.That(map.Count).IsEqualTo(10);
    }

    [Test]
    public async Task RobinHoodXmxHashMap_RoundTrip()
    {
        using var map = RobinHoodHashMap<int, int, XmxFinalizer>.Create(32);
        for (var i = 0; i < 10; i++) { map.Add(i, i); }
        await Assert.That(map.Count).IsEqualTo(10);
    }

    [Test]
    public async Task LinearFibonacciHashMap_RoundTrip()
    {
        using var map = LinearHashMap<int, int, FibonacciFinalizer>.Create(32);
        for (var i = 0; i < 10; i++) { map.Add(i, i); }
        await Assert.That(map.Count).IsEqualTo(10);
    }

    [Test]
    public async Task LinearLowbias32HashMap_RoundTrip()
    {
        using var map = LinearHashMap<int, int, Lowbias32Finalizer>.Create(32);
        for (var i = 0; i < 10; i++) { map.Add(i, i); }
        await Assert.That(map.Count).IsEqualTo(10);
    }

    [Test]
    public async Task LinearXmxHashMap_RoundTrip()
    {
        using var map = LinearHashMap<int, int, XmxFinalizer>.Create(32);
        for (var i = 0; i < 10; i++) { map.Add(i, i); }
        await Assert.That(map.Count).IsEqualTo(10);
    }

    [Test]
    public async Task ChainedFibonacciHashMap_RoundTrip()
    {
        using var map = ChainedHashMap<int, int, FibonacciFinalizer>.Create(32);
        for (var i = 0; i < 10; i++) { map.Add(i, i); }
        await Assert.That(map.Count).IsEqualTo(10);
    }

    [Test]
    public async Task ChainedLowbias32HashMap_RoundTrip()
    {
        using var map = ChainedHashMap<int, int, Lowbias32Finalizer>.Create(32);
        for (var i = 0; i < 10; i++) { map.Add(i, i); }
        await Assert.That(map.Count).IsEqualTo(10);
    }

    [Test]
    public async Task ChainedXmxHashMap_RoundTrip()
    {
        using var map = ChainedHashMap<int, int, XmxFinalizer>.Create(32);
        for (var i = 0; i < 10; i++) { map.Add(i, i); }
        await Assert.That(map.Count).IsEqualTo(10);
    }

    // ---- Corresponding finalizer-combo sets (spot-check one per algorithm) ----

    [Test]
    public async Task SwissFibonacciHashSet_RoundTrip()
    {
        using var set = SwissHashSet<int, FibonacciFinalizer>.Create(32);
        for (var i = 0; i < 10; i++) { set.Add(i); }
        await Assert.That(set.Count).IsEqualTo(10);
    }

    [Test]
    public async Task RobinHoodXmxHashSet_RoundTrip()
    {
        using var set = RobinHoodHashSet<int, XmxFinalizer>.Create(32);
        for (var i = 0; i < 10; i++) { set.Add(i); }
        await Assert.That(set.Count).IsEqualTo(10);
    }

    [Test]
    public async Task LinearLowbias32HashSet_RoundTrip()
    {
        using var set = LinearHashSet<int, Lowbias32Finalizer>.Create(32);
        for (var i = 0; i < 10; i++) { set.Add(i); }
        await Assert.That(set.Count).IsEqualTo(10);
    }

    [Test]
    public async Task ChainedFibonacciHashSet_RoundTrip()
    {
        using var set = ChainedHashSet<int, FibonacciFinalizer>.Create(32);
        for (var i = 0; i < 10; i++) { set.Add(i); }
        await Assert.That(set.Count).IsEqualTo(10);
    }
}
