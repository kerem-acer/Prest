using MEOP = Microsoft.Extensions.ObjectPool;

namespace Prest.ObjectPool;

/// <summary>
/// <see cref="MEOP.DefaultObjectPool{T}" />-backed accessor for
/// <see cref="PooledHashMap{TKey,TValue}" /> instances. Unlike the core package's
/// per-thread cache (accessed via <c>PooledHashMap.Create/Return</c>), this pool
/// is concurrent-safe and survives <c>await</c> boundaries. <see cref="Create" />-d
/// instances return themselves to the pool on <see cref="IDisposable.Dispose" />.
/// </summary>
public static class PooledHashMapPool<TKey, TValue> where TKey : notnull
{
    static readonly MEOP.ObjectPool<Pooled> Pool =
        new MEOP.DefaultObjectPool<Pooled>(new Policy(), maximumRetained: 64);

    /// <summary>Returns a map from the pool (or allocates a new one). Dispose to return.</summary>
    public static PooledHashMap<TKey, TValue> Create() => Pool.Get();

    /// <summary>Clears <paramref name="map" /> and returns it to the pool.</summary>
    public static void Return(PooledHashMap<TKey, TValue> map)
    {
        if (map is Pooled pooled)
        {
            Pool.Return(pooled);
        }
    }

    sealed class Pooled : PooledHashMap<TKey, TValue>
    {
#pragma warning disable CA2215 // Intentional: redirect to pool instead of base's per-thread cache.
        public override void Dispose() => Pool.Return(this);
#pragma warning restore CA2215
    }

    sealed class Policy : MEOP.PooledObjectPolicy<Pooled>
    {
        public override Pooled Create() => new();

        public override bool Return(Pooled obj)
        {
            obj.Clear();
            return true;
        }
    }
}
