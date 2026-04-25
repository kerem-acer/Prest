using System.Buffers;

namespace Prest.Tests;

public class ValuePooledBufferWriterTests
{
    [Test]
    public async Task Write_ThenRead_WrittenSpanMatches()
    {
        var writer = new ValuePooledBufferWriter<byte>(stackalloc byte[16]);
        var span = writer.GetSpan(8);
        "hello"u8.CopyTo(span);
        writer.Advance(5);

        var snapshot = writer.WrittenSpan.ToArray();
        writer.Dispose();

        await Assert.That(snapshot.AsSpan().SequenceEqual("hello"u8)).IsTrue();
    }

    [Test]
    public async Task Inline_NoOverflow_DoesNotPromote()
    {
        var writer = new ValuePooledBufferWriter<byte>(stackalloc byte[64]);
        var span = writer.GetSpan(8);
        "hello"u8.CopyTo(span);
        writer.Advance(5);

        var promoted = writer.UsesPooledBacking;
        writer.Dispose();

        await Assert.That(promoted).IsFalse();
    }

    [Test]
    public async Task Inline_Overflow_PromotesToPool()
    {
        var writer = new ValuePooledBufferWriter<byte>(stackalloc byte[4]);
        var s1 = writer.GetSpan(2);
        "ab"u8.CopyTo(s1);
        writer.Advance(2);
        var promotedBefore = writer.UsesPooledBacking;

        var s2 = writer.GetSpan(16);
        "cdefghij"u8.CopyTo(s2);
        writer.Advance(8);
        var promotedAfter = writer.UsesPooledBacking;

        var snapshot = writer.WrittenSpan.ToArray();
        var matches = snapshot.AsSpan().SequenceEqual("abcdefghij"u8);
        writer.Dispose();

        await Assert.That(promotedBefore).IsFalse();
        await Assert.That(promotedAfter).IsTrue();
        await Assert.That(matches).IsTrue();
    }

    [Test]
    public async Task Grow_ViaManySmallWrites_ProducesContiguousBuffer()
    {
        var writer = new ValuePooledBufferWriter<byte>(8);
        for (var i = 0; i < 32; i++)
        {
            var s = writer.GetSpan(4);
            s[0] = (byte)i;
            writer.Advance(1);
        }

        var snapshot = writer.WrittenSpan.ToArray();
        writer.Dispose();

        await Assert.That(snapshot.Length).IsEqualTo(32);
        for (var i = 0; i < 32; i++)
        {
            await Assert.That(snapshot[i]).IsEqualTo((byte)i);
        }
    }

    [Test]
    public async Task DetachBuffer_Rented_TransfersOwnershipAndEmptiesWriter()
    {
        var writer = new ValuePooledBufferWriter<byte>(8);
        var s = writer.GetSpan(8);
        "abc"u8.CopyTo(s);
        writer.Advance(3);

        var segment = writer.DetachBuffer();
        var count = segment.Count;
        var first = segment.Array![segment.Offset];
        var writerLen = writer.WrittenSpan.Length;

        if (segment.Array is not null)
        {
            ArrayPool<byte>.Shared.Return(segment.Array);
        }
        writer.Dispose();

        await Assert.That(count).IsEqualTo(3);
        await Assert.That(first).IsEqualTo((byte)'a');
        await Assert.That(writerLen).IsEqualTo(0);
    }

    [Test]
    public async Task DetachBuffer_Inline_RentsCopyForCaller()
    {
        var writer = new ValuePooledBufferWriter<byte>(stackalloc byte[16]);
        var s = writer.GetSpan(4);
        "hi"u8.CopyTo(s);
        writer.Advance(2);

        var segment = writer.DetachBuffer();
        var count = segment.Count;
        var matches = segment.AsSpan().SequenceEqual("hi"u8);

        if (segment.Array is not null)
        {
            ArrayPool<byte>.Shared.Return(segment.Array);
        }
        writer.Dispose();

        await Assert.That(count).IsEqualTo(2);
        await Assert.That(matches).IsTrue();
    }

    [Test]
    public async Task DetachBuffer_NoBuffer_ReturnsEmpty()
    {
        var writer = new ValuePooledBufferWriter<byte>();

        var segment = writer.DetachBuffer();
        var count = segment.Count;
        writer.Dispose();

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Reset_ReturnsBuffer_ButWriterStaysUsable()
    {
        var writer = new ValuePooledBufferWriter<byte>(4);
        var s1 = writer.GetSpan(4);
        "ab"u8.CopyTo(s1);
        writer.Advance(2);

        writer.Reset();
        var promotedAfterReset = writer.UsesPooledBacking;

        var s2 = writer.GetSpan(4);
        "cd"u8.CopyTo(s2);
        writer.Advance(2);

        var snapshot = writer.WrittenSpan.ToArray();
        writer.Dispose();

        await Assert.That(promotedAfterReset).IsFalse();
        await Assert.That(snapshot.AsSpan().SequenceEqual("cd"u8)).IsTrue();
    }

    [Test]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var writer = new ValuePooledBufferWriter<byte>(8);
        writer.Dispose();
        writer.Dispose();
    }

    [Test]
    public async Task Capacity_ReflectsRentedBuffer()
    {
        var writer = new ValuePooledBufferWriter<byte>(32);
        var cap = writer.Capacity;
        writer.Dispose();

        await Assert.That(cap).IsGreaterThanOrEqualTo(32);
    }

    [Test]
    public async Task Capacity_ReflectsInlineBuffer()
    {
        var writer = new ValuePooledBufferWriter<byte>(stackalloc byte[24]);
        var cap = writer.Capacity;
        writer.Dispose();

        await Assert.That(cap).IsEqualTo(24);
    }

    [Test]
    public async Task Capacity_ZeroBeforeFirstWrite_DefaultCtor()
    {
        var writer = new ValuePooledBufferWriter<byte>();
        var cap = writer.Capacity;
        writer.Dispose();

        await Assert.That(cap).IsEqualTo(0);
    }

    [Test]
    public async Task WrittenSpan_EmptyByDefault()
    {
        var writer = new ValuePooledBufferWriter<byte>(8);
        var len = writer.WrittenSpan.Length;
        writer.Dispose();

        await Assert.That(len).IsEqualTo(0);
    }

    [Test]
    public async Task WrittenCount_TracksAdvance()
    {
        var writer = new ValuePooledBufferWriter<byte>(stackalloc byte[16]);
        var s = writer.GetSpan(5);
        "hello"u8.CopyTo(s);
        writer.Advance(5);

        var count = writer.WrittenCount;
        writer.Dispose();

        await Assert.That(count).IsEqualTo(5);
    }

    [Test]
    public async Task Clear_RewindsWithoutReleasingBuffer()
    {
        var writer = new ValuePooledBufferWriter<byte>(stackalloc byte[16]);
        var s = writer.GetSpan(16);
        "hello"u8.CopyTo(s);
        writer.Advance(5);
        var capBefore = writer.Capacity;

        writer.Clear();

        var writtenLen = writer.WrittenSpan.Length;
        var capAfter = writer.Capacity;
        writer.Dispose();

        await Assert.That(writtenLen).IsEqualTo(0);
        await Assert.That(capAfter).IsEqualTo(capBefore);
    }

    [Test]
    public async Task Clear_ThenWrite_Works()
    {
        var writer = new ValuePooledBufferWriter<byte>(stackalloc byte[16]);
        var s1 = writer.GetSpan(8);
        "abc"u8.CopyTo(s1);
        writer.Advance(3);

        writer.Clear();

        var s2 = writer.GetSpan(8);
        "xyz"u8.CopyTo(s2);
        writer.Advance(3);

        var matches = writer.WrittenSpan.SequenceEqual("xyz"u8);
        writer.Dispose();

        await Assert.That(matches).IsTrue();
    }

    [Test]
    public async Task GetSpan_GrowsToHonorSizeHint()
    {
        var writer = new ValuePooledBufferWriter<byte>(8);
        var span = writer.GetSpan(128);
        var len = span.Length;
        writer.Dispose();

        await Assert.That(len).IsGreaterThanOrEqualTo(128);
    }

    [Test]
    public async Task GetSpan_FromInline_GrowsByPromoting()
    {
        var writer = new ValuePooledBufferWriter<byte>(stackalloc byte[4]);
        var span = writer.GetSpan(128);
        var len = span.Length;
        var promoted = writer.UsesPooledBacking;
        writer.Dispose();

        await Assert.That(len).IsGreaterThanOrEqualTo(128);
        await Assert.That(promoted).IsTrue();
    }

#if NET10_0_OR_GREATER
    [Test]
    public async Task IBufferWriter_Generic_DispatchWorks()
    {
        var writer = new ValuePooledBufferWriter<byte>(8);
        var written = WriteThroughInterface(ref writer, "ping"u8);
        var matches = writer.WrittenSpan.SequenceEqual("ping"u8);
        writer.Dispose();

        await Assert.That(written).IsEqualTo(4);
        await Assert.That(matches).IsTrue();
    }

    static int WriteThroughInterface<TWriter>(ref TWriter writer, ReadOnlySpan<byte> data)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var span = writer.GetSpan(data.Length);
        data.CopyTo(span);
        writer.Advance(data.Length);
        return data.Length;
    }
#endif
}
