using Prest.ObjectPool;

namespace Prest.ObjectPool.Tests;

public class PooledBufferWriterPoolTests
{
    [Test]
    public async Task RentReturn_RoundTrip_ReusesInstance()
    {
        var first = PooledBufferWriterPool<byte>.Rent();
        var s = first.GetSpan(8);
        "ab"u8.CopyTo(s);
        first.Advance(2);
        PooledBufferWriterPool<byte>.Return(first);

        var second = PooledBufferWriterPool<byte>.Rent();
        try
        {
            await Assert.That(ReferenceEquals(first, second)).IsTrue();
            await Assert.That(second.WrittenMemory.Length).IsEqualTo(0);
        }
        finally
        {
            PooledBufferWriterPool<byte>.Return(second);
        }
    }

    [Test]
    public async Task SurvivesAsync_UnlikeThreadstaticCache()
    {
        var writer = PooledBufferWriterPool<byte>.Rent();
        var s = writer.GetSpan(16);
        "before-await"u8.CopyTo(s);
        writer.Advance(12);

        await Task.Yield();

        var actual = writer.WrittenMemory.ToArray();
        PooledBufferWriterPool<byte>.Return(writer);

        await Assert.That(actual.AsSpan().SequenceEqual("before-await"u8)).IsTrue();
    }
}
