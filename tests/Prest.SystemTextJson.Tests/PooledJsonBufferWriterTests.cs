using Prest.SystemTextJson;
using System.Text;

namespace Prest.SystemTextJson.Tests;

public class PooledJsonBufferWriterTests
{
    [Test]
    public async Task Write_Compact_ProducesJson()
    {
        using var writer = new PooledJsonBufferWriter();
        writer.Writer.WriteStartObject();
        writer.Writer.WriteString("name", "kerem");
        writer.Writer.WriteEndObject();

        var memory = writer.FlushAndGetWrittenMemory();
        var json = Encoding.UTF8.GetString(memory.Span);

        await Assert.That(json).IsEqualTo("{\"name\":\"kerem\"}");
    }

    [Test]
    public async Task Write_Indented_ProducesIndentedJson()
    {
        using var writer = new PooledJsonBufferWriter(indented: true);
        writer.Writer.WriteStartObject();
        writer.Writer.WriteString("name", "kerem");
        writer.Writer.WriteEndObject();

        var memory = writer.FlushAndGetWrittenMemory();
        var json = Encoding.UTF8.GetString(memory.Span);

        await Assert.That(json.Contains('\n')).IsTrue();
    }

    [Test]
    public async Task Dispose_IsResetOnly_WriterRemainsUsable()
    {
        var writer = new PooledJsonBufferWriter();
        writer.Writer.WriteNumberValue(1);
        writer.Dispose();

        // Dispose must not recycle `this`; the writer state should be reset,
        // and a fresh write should work.
        writer.Writer.WriteNumberValue(42);
        var mem = writer.FlushAndGetWrittenMemory();
        await Assert.That(Encoding.UTF8.GetString(mem.Span)).IsEqualTo("42");
    }

    [Test]
    public async Task FlushAndDetachBuffer_HandsOwnership()
    {
        var writer = new PooledJsonBufferWriter();
        writer.Writer.WriteStringValue("hello");

        var segment = writer.FlushAndDetachBuffer();
        try
        {
            var json = Encoding.UTF8.GetString(segment.Array!, segment.Offset, segment.Count);
            await Assert.That(json).IsEqualTo("\"hello\"");
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(segment.Array!);
            writer.Dispose();
        }
    }
}

public class PooledJsonBufferWriterCacheTests
{
    [Test]
    public async Task RentReturn_Compact_ReusesInstance()
    {
        var first = PooledJsonBufferWriterCache.Rent(indented: false);
        PooledJsonBufferWriterCache.Return(first);

        var second = PooledJsonBufferWriterCache.Rent(indented: false);
        try
        {
            await Assert.That(ReferenceEquals(first, second)).IsTrue();
        }
        finally
        {
            PooledJsonBufferWriterCache.Return(second);
        }
    }

    [Test]
    public async Task RentReturn_Indented_ReusesInstance()
    {
        var first = PooledJsonBufferWriterCache.Rent(indented: true);
        PooledJsonBufferWriterCache.Return(first);

        var second = PooledJsonBufferWriterCache.Rent(indented: true);
        try
        {
            await Assert.That(ReferenceEquals(first, second)).IsTrue();
            await Assert.That(second.IsIndented).IsTrue();
        }
        finally
        {
            PooledJsonBufferWriterCache.Return(second);
        }
    }

    [Test]
    public async Task CompactAndIndented_HaveSeparateSlots()
    {
        var compact = PooledJsonBufferWriterCache.Rent(indented: false);
        var indented = PooledJsonBufferWriterCache.Rent(indented: true);
        PooledJsonBufferWriterCache.Return(compact);
        PooledJsonBufferWriterCache.Return(indented);

        var compact2 = PooledJsonBufferWriterCache.Rent(indented: false);
        var indented2 = PooledJsonBufferWriterCache.Rent(indented: true);
        try
        {
            await Assert.That(ReferenceEquals(compact, compact2)).IsTrue();
            await Assert.That(ReferenceEquals(indented, indented2)).IsTrue();
            await Assert.That(ReferenceEquals(compact, indented)).IsFalse();
        }
        finally
        {
            PooledJsonBufferWriterCache.Return(compact2);
            PooledJsonBufferWriterCache.Return(indented2);
        }
    }
}
