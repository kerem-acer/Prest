using MEOP = Microsoft.Extensions.ObjectPool;

namespace Prest.ObjectPool;

/// <summary>
/// <see cref="MEOP.DefaultObjectPool{T}" />-backed accessor for
/// <see cref="PooledHashSet{T}" /> instances. Concurrent-safe; survives
/// <c>await</c> boundaries. <see cref="Create" />-d instances return themselves
/// to the pool on <see cref="IDisposable.Dispose" />. For single-thread scratchpad
/// reuse use <c>PooledHashSet.Create/Return</c> in the core <c>Prest</c> package.
/// </summary>
public static class PooledHashSetPool<T> where T : notnull
{
    static readonly MEOP.ObjectPool<Pooled> Pool =
        new MEOP.DefaultObjectPool<Pooled>(new Policy(), maximumRetained: 64);

    /// <summary>Returns a set from the pool (or allocates a new one). Dispose to return.</summary>
    public static PooledHashSet<T> Create() => Pool.Get();

    /// <summary>Clears <paramref name="set" /> and returns it to the pool.</summary>
    public static void Return(PooledHashSet<T> set)
    {
        if (set is Pooled pooled)
        {
            Pool.Return(pooled);
        }
    }

    sealed class Pooled : PooledHashSet<T>
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
