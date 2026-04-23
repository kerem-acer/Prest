using System.Buffers;

namespace Prest.Tests;

public class PooledBufferWriterTests
{
    [Test]
    public async Task Write_ThenRead_WrittenMemoryMatches()
    {
        using var writer = new PooledBufferWriter<byte>();
        var span = writer.GetSpan(8);
        "hello"u8.CopyTo(span);
        writer.Advance(5);

        var actual = writer.WrittenMemory.ToArray();
        await Assert.That(actual.AsSpan().SequenceEqual("hello"u8)).IsTrue();
    }

    [Test]
    public async Task Grow_ViaManySmallWrites_ProducesContiguousBuffer()
    {
        using var writer = new PooledBufferWriter<byte>();
        for (var i = 0; i < 32; i++)
        {
            var s = writer.GetSpan(4);
            s[0] = (byte)i;
            writer.Advance(1);
        }

        var memory = writer.WrittenMemory;
        await Assert.That(memory.Length).IsEqualTo(32);
        for (var i = 0; i < 32; i++)
        {
            await Assert.That(memory.Span[i]).IsEqualTo((byte)i);
        }
    }

    [Test]
    public async Task DetachBuffer_TransfersOwnershipAndEmptiesWriter()
    {
        var writer = new PooledBufferWriter<byte>();
        var s = writer.GetSpan(8);
        "abc"u8.CopyTo(s);
        writer.Advance(3);

        var segment = writer.DetachBuffer();
        try
        {
            await Assert.That(segment.Count).IsEqualTo(3);
            await Assert.That(segment.Array![0]).IsEqualTo((byte)'a');

            await Assert.That(writer.WrittenMemory.Length).IsEqualTo(0);
        }
        finally
        {
            if (segment.Array is not null)
            {
                ArrayPool<byte>.Shared.Return(segment.Array);
            }
            writer.Dispose();
        }
    }

    [Test]
    public async Task Reset_ReturnsBuffer_ButWriterStaysUsable()
    {
        using var writer = new PooledBufferWriter<byte>();
        var s1 = writer.GetSpan(4);
        "ab"u8.CopyTo(s1);
        writer.Advance(2);

        writer.Reset();

        var s2 = writer.GetSpan(4);
        "cd"u8.CopyTo(s2);
        writer.Advance(2);

        await Assert.That(writer.WrittenMemory.ToArray().AsSpan().SequenceEqual("cd"u8)).IsTrue();
    }

    [Test]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var writer = new PooledBufferWriter<byte>();
        writer.Dispose();
        writer.Dispose();
    }

    [Test]
    public async Task AfterDispose_Write_Throws()
    {
        var writer = new PooledBufferWriter<byte>();
        writer.Dispose();

        await Assert.That(() => writer.GetSpan(4))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task RentReturn_SameThread_ReusesInstance()
    {
        var first = PooledBufferWriter<byte>.Rent();
        var s = first.GetSpan(16);
        "x"u8.CopyTo(s);
        first.Advance(1);
        PooledBufferWriter<byte>.Return(first);

        var second = PooledBufferWriter<byte>.Rent();
        try
        {
            await Assert.That(ReferenceEquals(first, second)).IsTrue();
            await Assert.That(second.WrittenMemory.Length).IsEqualTo(0);
        }
        finally
        {
            PooledBufferWriter<byte>.Return(second);
        }
    }

    [Test]
    public async Task RentReturn_AcrossDedicatedThreads_InstancesAreIsolated()
    {
        PooledBufferWriter<byte>? threadAInstance = null;
        PooledBufferWriter<byte>? threadBInstance = null;

        var threadA = new Thread(() =>
        {
            threadAInstance = PooledBufferWriter<byte>.Rent();
            PooledBufferWriter<byte>.Return(threadAInstance);
        });
        var threadB = new Thread(() =>
        {
            threadBInstance = PooledBufferWriter<byte>.Rent();
            PooledBufferWriter<byte>.Return(threadBInstance);
        });

        threadA.Start();
        threadA.Join();
        threadB.Start();
        threadB.Join();

        await Assert.That(ReferenceEquals(threadAInstance, threadBInstance)).IsFalse();
    }

    [Test]
    public async Task Capacity_ReflectsRentedBuffer()
    {
        using var writer = new PooledBufferWriter<byte>(32);

        await Assert.That(writer.Capacity).IsGreaterThanOrEqualTo(32);
    }

    [Test]
    public async Task Capacity_ZeroBeforeFirstWrite()
    {
        using var writer = new PooledBufferWriter<byte>();

        await Assert.That(writer.Capacity).IsEqualTo(0);
    }

    [Test]
    public async Task WrittenSpan_EmptyByDefault()
    {
        using var writer = new PooledBufferWriter<byte>();

        await Assert.That(writer.WrittenSpan.Length).IsEqualTo(0);
    }

    [Test]
    public async Task WrittenSpan_MatchesContent()
    {
        using var writer = new PooledBufferWriter<byte>();
        var span = writer.GetSpan(8);
        "hello"u8.CopyTo(span);
        writer.Advance(5);

        var snapshot = writer.WrittenSpan.ToArray();
        await Assert.That(snapshot.Length).IsEqualTo(5);
        await Assert.That(snapshot.AsSpan().SequenceEqual("hello"u8)).IsTrue();
    }

    [Test]
    public async Task Clear_RewindsWithoutReleasingBuffer()
    {
        using var writer = new PooledBufferWriter<byte>();
        var s = writer.GetSpan(16);
        "hello"u8.CopyTo(s);
        writer.Advance(5);
        var capBefore = writer.Capacity;

        writer.Clear();

        await Assert.That(writer.WrittenSpan.Length).IsEqualTo(0);
        await Assert.That(writer.Capacity).IsEqualTo(capBefore);
    }

    [Test]
    public async Task Clear_ThenWrite_Works()
    {
        using var writer = new PooledBufferWriter<byte>();
        var s1 = writer.GetSpan(8);
        "abc"u8.CopyTo(s1);
        writer.Advance(3);

        writer.Clear();

        var s2 = writer.GetSpan(8);
        "xyz"u8.CopyTo(s2);
        writer.Advance(3);

        await Assert.That(writer.WrittenSpan.SequenceEqual("xyz"u8)).IsTrue();
    }
}
