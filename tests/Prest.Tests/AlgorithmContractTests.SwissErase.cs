namespace Prest.Tests;

/// <summary>
/// SwissTable turns an erased slot back into an empty slot only when no group-wide run of
/// non-empty slots passes through it. Keys hash to themselves here, so a key's first probe group
/// starts at slot <c>key &amp; 31</c>.
/// </summary>
public partial class AlgorithmContractTests
{
    // Create(capacity: 20) gets 32 buckets (20 * 8 / 7 rounded up to a power of two); 28 fit
    // before a grow, so the layout below stays put.
    const int EraseCapacity = 20;
    const int EraseBucketCount = 32;
    const int GroupWidth = 16;

    [Test]
    public async Task Swiss_RemoveFromFullGroupOfAnotherKey_KeepsThatKeyReachable()
    {
        // Arrange: key 10 sits in slot 10. Fifteen keys with home slot 0 fill the rest of slots
        // 0..15, so the sixteenth home-0 key finds group 0..15 full and lands in slot 16.
        const int MiddleKey = 10;
        var homeZeroKeys = Enumerable.Range(0, GroupWidth).Select(j => j * EraseBucketCount).ToArray();
        var overflowKey = homeZeroKeys[^1];
        using var map = ComparerSwissHashMap<int, int>.Create(new IdentityComparer(), EraseCapacity);
        map.Add(MiddleKey, MiddleKey);
        foreach (var key in homeZeroKeys)
        {
            map.Add(key, key);
        }

        // Act: key 10's own probe group (slots 10..25) has empty slots, but group 0..15, which
        // the overflow key probed through, has none.
        var removed = map.Remove(MiddleKey);

        // Assert
        await Assert.That(removed).IsTrue();
        await Assert.That(map.ContainsKey(overflowKey)).IsTrue();
        await Assert.That(map.Remove(overflowKey)).IsTrue();
        await Assert.That(map.Count).IsEqualTo(GroupWidth - 1);
        await Assert.That(homeZeroKeys.SkipLast(1).All(map.ContainsKey)).IsTrue();
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task RemoveAndReAddEveryKey_ThenRemoveAll_LeavesNothingBehind(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int KeyCount = 65536;
        var keys = Enumerable.Range(0, KeyCount).Select(i => "key-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();

        // Act
        var (removed, countAfter) = algorithm switch
        {
            HashAlgorithmKind.Swiss => ChurnThenRemoveAll(SwissHashMap<string, int>.Create(KeyCount), keys),
            HashAlgorithmKind.RobinHood => ChurnThenRemoveAll(RobinHoodHashMap<string, int>.Create(KeyCount), keys),
            HashAlgorithmKind.Linear => ChurnThenRemoveAll(LinearHashMap<string, int>.Create(KeyCount), keys),
            HashAlgorithmKind.Chained => ChurnThenRemoveAll(ChainedHashMap<string, int>.Create(KeyCount), keys),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(removed).IsEqualTo(KeyCount);
        await Assert.That(countAfter).IsEqualTo(0);
    }

    // Adds every key, removes and re-adds each one, then removes them all.
    static (int Removed, int CountAfter) ChurnThenRemoveAll<TAlgo>(PooledHashMap<string, int, TAlgo> map, string[] keys)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<string, int>, string>
    {
        using (map)
        {
            foreach (var key in keys)
            {
                map.Add(key, 0);
            }
            foreach (var key in keys)
            {
                map.Remove(key);
                map.Add(key, 1);
            }
            var removed = keys.Count(map.Remove);
            return (removed, map.Count);
        }
    }
}
