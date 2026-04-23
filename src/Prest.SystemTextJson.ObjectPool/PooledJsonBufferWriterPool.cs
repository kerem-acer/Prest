using MEOP = Microsoft.Extensions.ObjectPool;

namespace Prest.SystemTextJson.ObjectPool;

/// <summary>
/// <see cref="MEOP.DefaultObjectPool{T}" />-backed accessor for
/// <see cref="PooledJsonBufferWriter" /> instances. Separate pools for compact
/// and indented writers. Survives <c>await</c> boundaries (unlike the threadstatic
/// cache in <see cref="PooledJsonBufferWriterCache" />).
/// </summary>
public static class PooledJsonBufferWriterPool
{
    static readonly MEOP.ObjectPool<PooledJsonBufferWriter> CompactPool =
        new MEOP.DefaultObjectPool<PooledJsonBufferWriter>(new Policy(indented: false), maximumRetained: 64);

    static readonly MEOP.ObjectPool<PooledJsonBufferWriter> IndentedPool =
        new MEOP.DefaultObjectPool<PooledJsonBufferWriter>(new Policy(indented: true), maximumRetained: 16);

    /// <summary>Returns a writer from the pool matching <paramref name="indented" />.</summary>
    public static PooledJsonBufferWriter Rent(bool indented = false) =>
        indented ? IndentedPool.Get() : CompactPool.Get();

    /// <summary>Resets <paramref name="writer" /> and returns it to the pool that matches its <see cref="PooledJsonBufferWriter.IsIndented" /> value.</summary>
    public static void Return(PooledJsonBufferWriter writer) =>
        (writer.IsIndented ? IndentedPool : CompactPool).Return(writer);

    sealed class Policy(bool indented) : MEOP.PooledObjectPolicy<PooledJsonBufferWriter>
    {
        public override PooledJsonBufferWriter Create() => new(indented);
        public override bool Return(PooledJsonBufferWriter obj) { obj.Reset(); return true; }
    }
}
