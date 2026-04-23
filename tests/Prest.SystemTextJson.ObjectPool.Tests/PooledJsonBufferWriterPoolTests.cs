using Prest.SystemTextJson;
using Prest.SystemTextJson.ObjectPool;
using System.Text;

namespace Prest.SystemTextJson.ObjectPool.Tests;

public class PooledJsonBufferWriterPoolTests
{
    [Test]
    public async Task RentReturn_Compact_ReusesInstance()
    {
        var first = PooledJsonBufferWriterPool.Rent(indented: false);
        PooledJsonBufferWriterPool.Return(first);

        var second = PooledJsonBufferWriterPool.Rent(indented: false);
        try
        {
            await Assert.That(ReferenceEquals(first, second)).IsTrue();
            await Assert.That(second.IsIndented).IsFalse();
        }
        finally
        {
            PooledJsonBufferWriterPool.Return(second);
        }
    }

    [Test]
    public async Task RentReturn_Indented_UsesIndentedPool()
    {
        var writer = PooledJsonBufferWriterPool.Rent(indented: true);
        try
        {
            await Assert.That(writer.IsIndented).IsTrue();

            writer.Writer.WriteStartObject();
            writer.Writer.WriteString("k", "v");
            writer.Writer.WriteEndObject();

            var json = Encoding.UTF8.GetString(writer.FlushAndGetWrittenMemory().Span);
            await Assert.That(json.Contains('\n')).IsTrue();
        }
        finally
        {
            PooledJsonBufferWriterPool.Return(writer);
        }
    }

    [Test]
    public async Task SurvivesAsync_AcrossAwait()
    {
        var writer = PooledJsonBufferWriterPool.Rent(indented: false);
        writer.Writer.WriteStringValue("before");

        await Task.Yield();

        var json = Encoding.UTF8.GetString(writer.FlushAndGetWrittenMemory().Span);
        PooledJsonBufferWriterPool.Return(writer);

        await Assert.That(json).IsEqualTo("\"before\"");
    }
}
