namespace Prest.Tests;

/// <summary>
/// Smoke tests verifying that every pluggable hashtable algorithm honors the
/// same functional contract (add, lookup, remove, grow, churn with collisions).
/// </summary>
public partial class AlgorithmContractTests
{
    // ---- Robin Hood ----

    [Test]
    public async Task RobinHood_BasicAdd_LookupRoundtrip()
    {
        using var map = RobinHoodHashMap<int, int>.Create(4);
        for (var i = 0; i < 100; i++)
        {
            map.Add(i, i * 3);
        }
        for (var i = 0; i < 100; i++)
        {
            await Assert.That(map.TryGetValue(i, out var v)).IsTrue();
            await Assert.That(v).IsEqualTo(i * 3);
        }
        await Assert.That(map.Count).IsEqualTo(100);
    }

    [Test]
    public async Task RobinHood_Remove_BackwardShift_DoesntLoseEntries()
    {
        using var map = RobinHoodHashMap<int, int>.Create(32);
        for (var i = 0; i < 50; i++)
        {
            map.Add(i, i);
        }
        for (var i = 0; i < 50; i += 2)
        {
            await Assert.That(map.Remove(i)).IsTrue();
        }
        for (var i = 1; i < 50; i += 2)
        {
            await Assert.That(map.TryGetValue(i, out var v)).IsTrue();
            await Assert.That(v).IsEqualTo(i);
        }
        for (var i = 0; i < 50; i += 2)
        {
            await Assert.That(map.ContainsKey(i)).IsFalse();
        }
        await Assert.That(map.Count).IsEqualTo(25);
    }

    [Test]
    public async Task RobinHood_ChurnAtStableSize_NoTombstoneBloat()
    {
        // Robin Hood's advantage — no tombstones, probe chains stay short under churn.
        using var map = RobinHoodHashMap<int, int>.Create(128);
        for (var i = 0; i < 100; i++)
        {
            map.Add(i, i);
        }
        for (var cycle = 0; cycle < 5; cycle++)
        {
            for (var i = 0; i < 100; i++)
            {
                await Assert.That(map.Remove(i)).IsTrue();
                await Assert.That(map.TryAdd(i, i + cycle * 1000)).IsTrue();
            }
        }
        for (var i = 0; i < 100; i++)
        {
            await Assert.That(map[i]).IsEqualTo(i + 4 * 1000);
        }
    }

    [Test]
    public async Task RobinHood_Duplicate_ReturnsFalse()
    {
        using var map = RobinHoodHashMap<string, int>.Create(4);
        await Assert.That(map.TryAdd("a", 1)).IsTrue();
        await Assert.That(map.TryAdd("a", 2)).IsFalse();
        await Assert.That(map["a"]).IsEqualTo(1);
    }

    // ---- Linear Probing ----

    [Test]
    public async Task Linear_BasicAdd_LookupRoundtrip()
    {
        using var map = LinearHashMap<int, int>.Create(4);
        for (var i = 0; i < 100; i++)
        {
            map.Add(i, i * 7);
        }
        for (var i = 0; i < 100; i++)
        {
            await Assert.That(map.TryGetValue(i, out var v)).IsTrue();
            await Assert.That(v).IsEqualTo(i * 7);
        }
    }

    [Test]
    public async Task Linear_RemoveAndReinsert_Works()
    {
        using var map = LinearHashMap<int, int>.Create(16);
        for (var i = 0; i < 30; i++)
        {
            map.Add(i, i);
        }
        for (var i = 0; i < 30; i++)
        {
            await Assert.That(map.Remove(i)).IsTrue();
        }
        await Assert.That(map.Count).IsEqualTo(0);
        for (var i = 0; i < 30; i++)
        {
            map.Add(i, i * 2);
        }
        for (var i = 0; i < 30; i++)
        {
            await Assert.That(map[i]).IsEqualTo(i * 2);
        }
    }

    // ---- PooledHashSet parity ----

    [Test]
    public async Task RobinHoodSet_AddRemove_Works()
    {
        using var set = RobinHoodHashSet<int>.Create(8);
        for (var i = 0; i < 50; i++)
        {
            await Assert.That(set.Add(i)).IsTrue();
        }
        await Assert.That(set.Add(10)).IsFalse();
        for (var i = 0; i < 50; i += 3)
        {
            await Assert.That(set.Remove(i)).IsTrue();
        }
        for (var i = 0; i < 50; i++)
        {
            var expected = (i % 3) != 0;
            await Assert.That(set.Contains(i)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task LinearSet_AddContains_Works()
    {
        using var set = LinearHashSet<string>.Create(4);
        await Assert.That(set.Add("apple")).IsTrue();
        await Assert.That(set.Add("banana")).IsTrue();
        await Assert.That(set.Add("apple")).IsFalse();
        await Assert.That(set.Contains("apple")).IsTrue();
        await Assert.That(set.Contains("cherry")).IsFalse();
    }

    // ---- Chained ----

    [Test]
    public async Task Blitz_BasicAdd_LookupRoundtrip()
    {
        using var map = ChainedHashMap<int, int>.Create(4);
        for (var i = 0; i < 100; i++)
        {
            map.Add(i, i * 5);
        }
        for (var i = 0; i < 100; i++)
        {
            await Assert.That(map.TryGetValue(i, out var v)).IsTrue();
            await Assert.That(v).IsEqualTo(i * 5);
        }
        await Assert.That(map.Count).IsEqualTo(100);
    }

    [Test]
    public async Task Blitz_Remove_SwapWithLast_PreservesChains()
    {
        // The tricky path: when removing a mid-chain entry, we swap-with-last
        // and re-point whoever was referencing the last entry.
        using var map = ChainedHashMap<int, int>.Create(32);
        for (var i = 0; i < 50; i++)
        {
            map.Add(i, i * 10);
        }
        for (var i = 0; i < 50; i += 2)
        {
            await Assert.That(map.Remove(i)).IsTrue();
        }
        for (var i = 1; i < 50; i += 2)
        {
            await Assert.That(map[i]).IsEqualTo(i * 10);
        }
        for (var i = 0; i < 50; i += 2)
        {
            await Assert.That(map.ContainsKey(i)).IsFalse();
        }
        await Assert.That(map.Count).IsEqualTo(25);
    }

    [Test]
    public async Task Blitz_ChurnHeavy_StaysConsistent()
    {
        using var map = ChainedHashMap<int, int>.Create(64);
        for (var i = 0; i < 100; i++)
        {
            map.Add(i, i);
        }
        for (var cycle = 0; cycle < 3; cycle++)
        {
            for (var i = 0; i < 100; i++)
            {
                await Assert.That(map.Remove(i)).IsTrue();
                await Assert.That(map.TryAdd(i, i + cycle * 100)).IsTrue();
            }
        }
        for (var i = 0; i < 100; i++)
        {
            await Assert.That(map[i]).IsEqualTo(i + 2 * 100);
        }
    }

    [Test]
    public async Task BlitzSet_AddRemove_Works()
    {
        using var set = ChainedHashSet<int>.Create(8);
        for (var i = 0; i < 30; i++)
        {
            await Assert.That(set.Add(i)).IsTrue();
        }
        for (var i = 0; i < 30; i += 3)
        {
            await Assert.That(set.Remove(i)).IsTrue();
        }
        for (var i = 0; i < 30; i++)
        {
            await Assert.That(set.Contains(i)).IsEqualTo((i % 3) != 0);
        }
    }

    // ---- Finalizer knob ----

    [Test]
    public async Task Swiss_WithLowbias32Finalizer_RoundtripsCorrectly()
    {
        using var map = SwissHashMap<int, int, Lowbias32Finalizer>.Create(64);
        for (var i = 0; i < 200; i++)
        {
            map.Add(i, i * 11);
        }
        for (var i = 0; i < 200; i++)
        {
            await Assert.That(map[i]).IsEqualTo(i * 11);
        }
    }

    [Test]
    public async Task RobinHood_WithFibonacciFinalizer_RoundtripsCorrectly()
    {
        using var map = RobinHoodHashMap<int, int, FibonacciFinalizer>.Create(64);
        for (var i = 0; i < 200; i++)
        {
            map.Add(i, i);
        }
        for (var i = 0; i < 100; i++)
        {
            await Assert.That(map.Remove(i)).IsTrue();
        }
        for (var i = 100; i < 200; i++)
        {
            await Assert.That(map[i]).IsEqualTo(i);
        }
        await Assert.That(map.Count).IsEqualTo(100);
    }

    [Test]
    public async Task Blitz_WithXmxFinalizer_RoundtripsCorrectly()
    {
        using var map = ChainedHashMap<int, int, XmxFinalizer>.Create(64);
        for (var i = 0; i < 200; i++)
        {
            map.Add(i, i * 3);
        }
        for (var i = 0; i < 200; i++)
        {
            await Assert.That(map[i]).IsEqualTo(i * 3);
        }
    }

    [Test]
    public async Task Set_WithCustomFinalizer_IndependentOfAlgorithm()
    {
        using var swissSet = SwissHashSet<int, Lowbias32Finalizer>.Create(32);
        using var robinSet = RobinHoodHashSet<int, FibonacciFinalizer>.Create(32);
        for (var i = 0; i < 100; i++)
        {
            swissSet.Add(i);
            robinSet.Add(i);
        }
        for (var i = 0; i < 100; i++)
        {
            await Assert.That(swissSet.Contains(i)).IsTrue();
            await Assert.That(robinSet.Contains(i)).IsTrue();
        }
    }
}
