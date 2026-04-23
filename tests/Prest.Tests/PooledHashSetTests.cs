namespace Prest.Tests;

public partial class PooledHashSetTests
{
    [Test]
    public async Task Add_DistinctItems_ReturnsTrue()
    {
        using var set = PooledHashSet<int>.Create(8);
        await Assert.That(set.Add(1)).IsTrue();
        await Assert.That(set.Add(2)).IsTrue();
        await Assert.That(set.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Add_Duplicate_ReturnsFalse()
    {
        using var set = PooledHashSet<int>.Create(8);
        set.Add(1);
        await Assert.That(set.Add(1)).IsFalse();
        await Assert.That(set.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Contains_Works()
    {
        using var set = PooledHashSet<string>.Create(8);
        set.Add("hello");

        await Assert.That(set.Contains("hello")).IsTrue();
        await Assert.That(set.Contains("missing")).IsFalse();
    }

    [Test]
    public async Task SurvivesAcrossAwait()
    {
        using var set = PooledHashSet<int>.Create(8);
        set.Add(1);

        await Task.Yield();

        set.Add(2);
        await Assert.That(set.Count).IsEqualTo(2);
    }
}
