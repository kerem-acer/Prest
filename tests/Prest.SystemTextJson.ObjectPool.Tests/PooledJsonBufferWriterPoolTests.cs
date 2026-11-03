using Prest.SystemTextJson;
using Prest.SystemTextJson.ObjectPool;
using System.Text;

namespace Prest.SystemTextJson.ObjectPool.Tests;

// The pools are static and shared by every test in this assembly, and TUnit runs tests in parallel.
// A reuse assertion fails if another test rents between this test's Return and Rent, so tests that
// touch the same pool share a NotInParallel key (as in Prest.ObjectPool.Tests).
public class PooledJsonBufferWriterPoolTests
{
    const string CompactPool = "PooledJsonBufferWriterPool.Compact";
    const string IndentedPool = "PooledJsonBufferWriterPool.Indented";

    [Test]
    [NotInParallel(CompactPool)]
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
    [NotInParallel(IndentedPool)]
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
    [NotInParallel(CompactPool)]
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
