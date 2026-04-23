using System.Collections.Immutable;
using Prest;
using Prest.Immutable;

namespace Prest.Immutable.Tests;

public class PooledArrayExtensionsTests
{
    [Test]
    public async Task ToImmutableArray_CopiesContent()
    {
        using PooledArray<int> pooled = [1, 2, 3];

        var immutable = pooled.ToImmutableArray();

        await Assert.That(immutable.Length).IsEqualTo(3);
        await Assert.That(immutable[0]).IsEqualTo(1);
        await Assert.That(immutable[1]).IsEqualTo(2);
        await Assert.That(immutable[2]).IsEqualTo(3);
    }

    [Test]
    public async Task ToImmutableArray_Empty_ReturnsEmpty()
    {
        using PooledArray<int> pooled = [];

        var immutable = pooled.ToImmutableArray();

        await Assert.That(immutable.IsEmpty).IsTrue();
    }

    [Test]
    public async Task FromImmutableArray_CopiesContent()
    {
        var immutable = ImmutableArray.Create(10, 20, 30);

        using var pooled = PooledArray.FromImmutableArray(immutable);

        await Assert.That(pooled.Count).IsEqualTo(3);
        await Assert.That(pooled.Span.ToArray()).IsEquivalentTo([10, 20, 30]);
    }

    [Test]
    public async Task FromImmutableArray_Default_ReturnsEmpty()
    {
        var defaultImmutable = default(ImmutableArray<int>);

        using var pooled = PooledArray.FromImmutableArray(defaultImmutable);

        await Assert.That(pooled.IsEmpty).IsTrue();
    }

    [Test]
    public async Task RoundTrip_PreservesElements()
    {
        var original = ImmutableArray.Create("a", "b", "c");

        using var pooled = PooledArray.FromImmutableArray(original);
        var roundTripped = pooled.ToImmutableArray();

        await Assert.That(roundTripped.Length).IsEqualTo(3);
        await Assert.That(roundTripped[0]).IsEqualTo("a");
        await Assert.That(roundTripped[1]).IsEqualTo("b");
        await Assert.That(roundTripped[2]).IsEqualTo("c");
    }

    [Test]
    public async Task ToImmutableArray_SurvivesPooledArrayDispose()
    {
        ImmutableArray<int> immutable;
        using (PooledArray<int> pooled = [7, 8, 9])
        {
            immutable = pooled.ToImmutableArray();
        }

        await Assert.That(immutable.Length).IsEqualTo(3);
        await Assert.That(immutable[0]).IsEqualTo(7);
    }
}
