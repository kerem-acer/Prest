namespace Prest.Tests;

public partial class PooledHashMapTests
{
    [Test]
    public async Task ZeroCapacity_IsEmpty()
    {
        using var map = PooledHashMap<string, int>.Create(0);

        await Assert.That(map.IsEmpty).IsTrue();
        await Assert.That(map.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TryGetValue_PresentKey_ReturnsTrue()
    {
        using var map = PooledHashMap<string, int>.Create(4);
        map.Add("a", 1);
        map.Add("b", 2);
        map.Add("c", 3);

        var found = map.TryGetValue("b", out var value);

        await Assert.That(found).IsTrue();
        await Assert.That(value).IsEqualTo(2);
    }

    [Test]
    public async Task TryGetValue_MissingKey_ReturnsFalse()
    {
        using var map = PooledHashMap<string, int>.Create(4);
        map.Add("a", 1);

        var found = map.TryGetValue("missing", out _);

        await Assert.That(found).IsFalse();
    }

    [Test]
    public async Task Indexer_ThrowsForMissingKey()
    {
        using var map = PooledHashMap<string, int>.Create(4);
        map.Add("a", 1);

        await Assert.That(() =>
        {
            _ = map["missing"];
        }).Throws<KeyNotFoundException>();
    }

    [Test]
    public async Task ContainsKey_Works()
    {
        using var map = PooledHashMap<string, int>.Create(4);
        map.Add("a", 1);
        map.Add("b", 2);

        await Assert.That(map.ContainsKey("a")).IsTrue();
        await Assert.That(map.ContainsKey("missing")).IsFalse();
    }

    [Test]
    public async Task Add_DuplicateKey_Throws()
    {
        using var map = PooledHashMap<string, int>.Create(4);
        map.Add("a", 1);

        await Assert.That(() => map.Add("a", 2)).Throws<ArgumentException>();
    }

    [Test]
    public async Task TryAdd_DuplicateKey_ReturnsFalse()
    {
        using var map = PooledHashMap<string, int>.Create(4);
        await Assert.That(map.TryAdd("a", 1)).IsTrue();
        await Assert.That(map.TryAdd("a", 2)).IsFalse();
        await Assert.That(map["a"]).IsEqualTo(1);
    }

    [Test]
    public async Task Add_BeyondCapacity_Grows()
    {
        // Capacity is a growth hint, not a hard limit — the table rehashes.
        using var map = PooledHashMap<int, int>.Create(2);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30);

        await Assert.That(map.Count).IsEqualTo(3);
        await Assert.That(map[1]).IsEqualTo(10);
        await Assert.That(map[2]).IsEqualTo(20);
        await Assert.That(map[3]).IsEqualTo(30);
    }

    [Test]
    public async Task LargeMap_AllLookupsSucceed()
    {
        const int n = 1000;
        using var map = PooledHashMap<int, int>.Create(n);
        for (var i = 0; i < n; i++)
        {
            map.Add(i, i * 2);
        }

        for (var i = 0; i < n; i++)
        {
            var found = map.TryGetValue(i, out var v);
            await Assert.That(found).IsTrue();
            await Assert.That(v).IsEqualTo(i * 2);
        }

        await Assert.That(map.Count).IsEqualTo(n);
    }

    [Test]
    public async Task HashCollisions_HandledCorrectly()
    {
        using var map = ComparerSwissHashMap<int, string>.Create(new ConstantHashComparer(), 4);
        map.Add(1, "one");
        map.Add(2, "two");
        map.Add(3, "three");

        await Assert.That(map[1]).IsEqualTo("one");
        await Assert.That(map[2]).IsEqualTo("two");
        await Assert.That(map[3]).IsEqualTo("three");
    }

    [Test]
    public async Task Enumerator_YieldsAllEntries()
    {
        using var map = PooledHashMap<string, int>.Create(4);
        map.Add("a", 10);
        map.Add("b", 20);

        var collected = new Dictionary<string, int>();
        foreach (var kv in map)
        {
            collected[kv.Key] = kv.Value;
        }

        await Assert.That(collected.Count).IsEqualTo(2);
        await Assert.That(collected["a"]).IsEqualTo(10);
        await Assert.That(collected["b"]).IsEqualTo(20);
    }

    [Test]
    public async Task Remove_PresentKey_ReturnsTrue()
    {
        using var map = PooledHashMap<string, int>.Create(4);
        map.Add("a", 1);
        map.Add("b", 2);

        await Assert.That(map.Remove("a")).IsTrue();
        await Assert.That(map.ContainsKey("a")).IsFalse();
        await Assert.That(map.ContainsKey("b")).IsTrue();
        await Assert.That(map.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Remove_MissingKey_ReturnsFalse()
    {
        using var map = PooledHashMap<string, int>.Create(4);
        map.Add("a", 1);

        await Assert.That(map.Remove("missing")).IsFalse();
        await Assert.That(map.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Clear_ResetsToEmpty()
    {
        using var map = PooledHashMap<string, int>.Create(4);
        map.Add("a", 1);
        map.Add("b", 2);

        map.Clear();

        await Assert.That(map.Count).IsEqualTo(0);
        await Assert.That(map.IsEmpty).IsTrue();
        await Assert.That(map.ContainsKey("a")).IsFalse();

        map.Add("c", 3);
        await Assert.That(map["c"]).IsEqualTo(3);
    }

    [Test]
    public async Task Keys_EnumeratesAllKeys()
    {
        using var map = PooledHashMap<string, int>.Create(4);
        map.Add("a", 1);
        map.Add("b", 2);

        var collected = new HashSet<string>();
        foreach (var k in map.Keys)
        {
            collected.Add(k);
        }

        await Assert.That(collected.Count).IsEqualTo(2);
        await Assert.That(collected.Contains("a")).IsTrue();
        await Assert.That(collected.Contains("b")).IsTrue();
    }

    [Test]
    public async Task Values_EnumeratesAllValues()
    {
        using var map = PooledHashMap<string, int>.Create(4);
        map.Add("a", 1);
        map.Add("b", 2);

        var collected = new HashSet<int>();
        foreach (var v in map.Values)
        {
            collected.Add(v);
        }

        await Assert.That(collected.Count).IsEqualTo(2);
        await Assert.That(collected.Contains(1)).IsTrue();
        await Assert.That(collected.Contains(2)).IsTrue();
    }

    [Test]
    public async Task Dispose_IsIdempotent()
    {
        var map = PooledHashMap<string, int>.Create(4);
        map.Add("a", 1);
        map.Dispose();
        map.Dispose();

        await Assert.That(map.Count).IsEqualTo(0);
    }

    sealed class ConstantHashComparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x == y;
        public int GetHashCode(int obj) => 42;
    }
}
