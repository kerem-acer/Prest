using MEOP = Microsoft.Extensions.ObjectPool;

namespace Prest.ObjectPool;

/// <summary>
/// <see cref="MEOP.DefaultObjectPool{T}" />-backed accessor for
/// <see cref="PooledBufferWriter{T}" /> instances. Unlike the core package's
/// threadstatic cache, this pool survives <c>await</c> boundaries.
/// </summary>
public static class PooledBufferWriterPool<T>
{
    static readonly MEOP.ObjectPool<PooledBufferWriter<T>> Pool =
        new MEOP.DefaultObjectPool<PooledBufferWriter<T>>(new Policy(), maximumRetained: 64);

    /// <summary>Returns a writer from the pool (or allocates a new one).</summary>
    public static PooledBufferWriter<T> Rent() => Pool.Get();

    /// <summary>Resets <paramref name="writer" /> and returns it to the pool.</summary>
    public static void Return(PooledBufferWriter<T> writer) => Pool.Return(writer);

    sealed class Policy : MEOP.PooledObjectPolicy<PooledBufferWriter<T>>
    {
        public override PooledBufferWriter<T> Create() => new();

        public override bool Return(PooledBufferWriter<T> obj)
        {
            obj.Reset();
            return true;
        }
    }
}
