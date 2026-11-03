namespace Prest.Tests;

/// <summary>
/// Robin Hood caps probe distance at 254. These tests drive inserts past the cap in each of its two
/// places: while still looking for a duplicate, and while carrying a displaced entry forward. Keys
/// hash to themselves, so their home slot is <c>key &amp; (bucketCount - 1)</c>; growing the table
/// splits the clusters and lets the insert finish.
/// </summary>
public partial class AlgorithmContractTests
{
    // Create(capacity: 400) gets 512 buckets (400 * 8 / 7 rounded up to a power of two), and
    // 512 * 7 / 8 = 448 entries fit before a load-factor grow, so only the distance cap grows it.
    const int CapCapacity = 400;
    const int CapBucketCount = 512;
    const int MaxProbeDistance = 254;

    [Test]
    public async Task RobinHood_InsertPastDistanceCapWhileCheckingDuplicates_GrowsAndKeepsAllEntries()
    {
        // Arrange: MaxProbeDistance + 1 keys share home slot 1, at distances 0..254.
        const int ClusterSize = MaxProbeDistance + 1;
        using var map = ComparerRobinHoodHashMap<int, int>.Create(new IdentityComparer(), CapCapacity);
        var clusterKeys = Enumerable.Range(0, ClusterSize).Select(j => 1 + (j * CapBucketCount)).ToArray();
        foreach (var key in clusterKeys)
        {
            map.Add(key, key);
        }
        var capacityBefore = map.Capacity;
        var extraKey = 1 + (ClusterSize * CapBucketCount);

        // Act: a lookup and a remove of the missing key walk the whole cluster to the cap; then one
        // more key with home slot 1 would sit at distance 255.
        var foundBeforeAdd = map.ContainsKey(extraKey);
        var removedBeforeAdd = map.Remove(extraKey);
        var added = map.TryAdd(extraKey, extraKey);

        // Assert
        await Assert.That(foundBeforeAdd).IsFalse();
        await Assert.That(removedBeforeAdd).IsFalse();
        await Assert.That(added).IsTrue();
        await Assert.That(map.Capacity).IsGreaterThan(capacityBefore);
        await Assert.That(map.Count).IsEqualTo(ClusterSize + 1);
        await Assert.That(clusterKeys.Append(extraKey).All(k => map.TryGetValue(k, out var v) && v == k)).IsTrue();
    }

    [Test]
    public async Task RobinHood_InsertDisplacingEntryPastDistanceCap_GrowsAndKeepsAllEntries()
    {
        // Arrange: key 0 at slot 0, then MaxProbeDistance + 1 keys with home slot 1 filling
        // slots 1..255 at distances 0..254.
        const int ClusterSize = MaxProbeDistance + 1;
        using var map = ComparerRobinHoodHashMap<int, int>.Create(new IdentityComparer(), CapCapacity);
        map.Add(0, 0);
        var clusterKeys = Enumerable.Range(0, ClusterSize).Select(j => 1 + (j * CapBucketCount)).ToArray();
        foreach (var key in clusterKeys)
        {
            map.Add(key, key);
        }
        var capacityBefore = map.Capacity;

        // Act: a second key with home slot 0 takes slot 1, and the entry it displaces is pushed
        // past slot 255, to distance 255.
        var added = map.TryAdd(CapBucketCount, CapBucketCount);

        // Assert
        await Assert.That(added).IsTrue();
        await Assert.That(map.Capacity).IsGreaterThan(capacityBefore);
        await Assert.That(map.Count).IsEqualTo(ClusterSize + 2);
        await Assert.That(clusterKeys.Append(0).Append(CapBucketCount).All(k => map.TryGetValue(k, out var v) && v == k)).IsTrue();
    }

    [Test]
    public async Task RobinHood_GetValueRefOrAddDefaultDisplacingEntryPastDistanceCap_ReturnsRefToNewEntry()
    {
        // Arrange: the same layout as above, so adding CapBucketCount pushes a displaced entry past
        // the cap and the insert finishes after a grow.
        const int ClusterSize = MaxProbeDistance + 1;
        const int NewValue = -1;
        using var map = ComparerRobinHoodHashMap<int, int>.Create(new IdentityComparer(), CapCapacity);
        map.Add(0, 0);
        var clusterKeys = Enumerable.Range(0, ClusterSize).Select(j => 1 + (j * CapBucketCount)).ToArray();
        foreach (var key in clusterKeys)
        {
            map.Add(key, key);
        }
        var capacityBefore = map.Capacity;

        // Act: write through the returned ref. It must be the new entry's value, not the displaced one's.
        PooledCollectionsMarshal.GetValueRefOrAddDefault(map, CapBucketCount, out var exists) = NewValue;

        // Assert
        await Assert.That(exists).IsFalse();
        await Assert.That(map.Capacity).IsGreaterThan(capacityBefore);
        await Assert.That(map[CapBucketCount]).IsEqualTo(NewValue);
        await Assert.That(clusterKeys.Append(0).All(k => map.TryGetValue(k, out var v) && v == k)).IsTrue();
    }

    sealed class IdentityComparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x == y;

        public int GetHashCode(int obj) => obj;
    }
}
