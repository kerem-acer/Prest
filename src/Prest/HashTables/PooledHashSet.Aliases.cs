using System.ComponentModel;
using System.Runtime.CompilerServices;

#pragma warning disable IDE0028 // The collection builder ends in these `new Alias(capacity)` calls; `[with(capacity)]` here would recurse.

namespace Prest;

// ---------------------------------------------------------------------------
// Concrete alias types for common PooledHashSet configurations, mirroring
// PooledHashMap.Aliases.cs: every alias has its own Create(...) returning the
// alias and its own per-thread single-slot cache.
//
// Shapes per algorithm ({Algo} = Swiss, RobinHood, Linear, Chained):
//   {Algo}HashSet<T>                     — EqualityDefaultHasher + NoOpHashFinalizer
//   {Algo}HashSet<T,TFinalizer>          — pick a finalizer
//   {Algo}HashSet<T,THasher,TFinalizer>  — plug in a stateless struct IHasher<T>
//   Comparer{Algo}HashSet<T>             — runtime IEqualityComparer<T>
//   Comparer{Algo}HashSet<T,TFinalizer>  — runtime IEqualityComparer<T> + finalizer
//
// SwissHashSet<T> derives from PooledHashSet<T> (same configuration).
//
// Every alias supports collection expressions through PooledHashSetCollectionBuilder;
// comparer misuse is a compile error, as for maps.
// ---------------------------------------------------------------------------

/// <summary>Default pooled hash set — SwissTable + default equality comparer + no finalizer.</summary>
/// <remarks>
/// Same configuration as <see cref="SwissHashSet{T}" />, which derives from this type.
/// Not sealed so that <c>Prest.ObjectPool</c> can subclass it; instances of subclasses never
/// enter this type's per-thread cache. Supports collection expressions, like every alias:
/// <c>PooledHashSet&lt;string&gt; set = ["a", "b"];</c>
/// </remarks>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.Create))]
public class PooledHashSet<T>
    : PooledHashSet<T, SwissTableAlgorithm<T, T, EqualityDefaultHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    [ThreadStatic]
    static PooledHashSet<T>? _threadCache;

    protected PooledHashSet(int capacity = 0) : base(capacity) { }

    /// <summary>
    /// Returns a set from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashSet{T,TAlgo}.Dispose" />, which returns the instance to the
    /// same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new PooledHashSet<T> Create() => TakeCached() ?? new PooledHashSet<T>(0);

    /// <summary>
    /// Returns a set with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new PooledHashSet<T> Create(int capacity) => TakeCached() ?? new PooledHashSet<T>(capacity);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerSwissHashSet{T}" />.
    /// </summary>
    [Obsolete(AliasErrors.PooledHashSetUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static PooledHashSet<T> Create(IEqualityComparer<T> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.PooledHashSetUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static PooledHashSet<T>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

// ---- SwissTable ----

/// <summary>SwissTable + default hasher + no finalizer. Derives from <see cref="PooledHashSet{T}" /> (same configuration).</summary>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateSwiss))]
public sealed class SwissHashSet<T>
    : PooledHashSet<T>
    where T : notnull
{
    [ThreadStatic]
    static SwissHashSet<T>? _threadCache;

    SwissHashSet(int capacity) : base(capacity) { }

    /// <summary>
    /// Returns a set from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashSet{T,TAlgo}.Dispose" />, which returns the instance to the
    /// same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new SwissHashSet<T> Create() => TakeCached() ?? new SwissHashSet<T>(0);

    /// <summary>
    /// Returns a set with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new SwissHashSet<T> Create(int capacity) => TakeCached() ?? new SwissHashSet<T>(capacity);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerSwissHashSet{T}" />.
    /// </summary>
    [Obsolete(AliasErrors.SwissHashSetUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new SwissHashSet<T> Create(IEqualityComparer<T> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.SwissHashSetUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static SwissHashSet<T>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>SwissTable + default hasher + user-chosen finalizer.</summary>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateSwiss))]
public sealed class SwissHashSet<T, TFinalizer>
    : PooledHashSet<T, SwissTableAlgorithm<T, T, EqualityDefaultHasher<T>, TFinalizer>>
    where T : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static SwissHashSet<T, TFinalizer>? _threadCache;

    SwissHashSet(int capacity) : base(capacity) { }

    /// <summary>
    /// Returns a set from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashSet{T,TAlgo}.Dispose" />, which returns the instance to the
    /// same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new SwissHashSet<T, TFinalizer> Create() => TakeCached() ?? new SwissHashSet<T, TFinalizer>(0);

    /// <summary>
    /// Returns a set with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new SwissHashSet<T, TFinalizer> Create(int capacity) => TakeCached() ?? new SwissHashSet<T, TFinalizer>(capacity);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerSwissHashSet{T,TFinalizer}" />.
    /// </summary>
    [Obsolete(AliasErrors.SwissHashSetFinalizerUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static SwissHashSet<T, TFinalizer> Create(IEqualityComparer<T> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.SwissHashSetFinalizerUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static SwissHashSet<T, TFinalizer>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>SwissTable + user-supplied stateless struct hasher + user-chosen finalizer.</summary>
/// <remarks>
/// <typeparamref name="THasher" /> is always used as <c>default(THasher)</c>, so it must be
/// stateless — the JIT then inlines its hash and equality into every call. For a runtime
/// <see cref="IEqualityComparer{T}" /> use <c>ComparerSwissHashSet</c>;
/// <see cref="ComparerHasher{T}" /> is rejected here with <see cref="NotSupportedException" />.
/// </remarks>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateSwiss))]
public sealed class SwissHashSet<T, THasher, TFinalizer>
    : PooledHashSet<T, SwissTableAlgorithm<T, T, THasher, TFinalizer>>
    where T : notnull
    where THasher : struct, IHasher<T>
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static SwissHashSet<T, THasher, TFinalizer>? _threadCache;

    SwissHashSet(int capacity) : base(capacity) { }

    /// <summary>
    /// Returns a set from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashSet{T,TAlgo}.Dispose" />, which returns the instance to the
    /// same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new SwissHashSet<T, THasher, TFinalizer> Create() => Create(0);

    /// <summary>
    /// Returns a set with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new SwissHashSet<T, THasher, TFinalizer> Create(int capacity)
    {
        AliasGuards.EnsureStatelessHasher<T, THasher>();
        return TakeCached() ?? new SwissHashSet<T, THasher, TFinalizer>(capacity);
    }

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static SwissHashSet<T, THasher, TFinalizer>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>SwissTable + user-supplied <see cref="IEqualityComparer{T}" /> + no finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{T}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new set.
/// </remarks>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateComparerSwiss))]
public sealed class ComparerSwissHashSet<T>
    : PooledHashSet<T, SwissTableAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    [ThreadStatic]
    static ComparerSwissHashSet<T>? _threadCache;

    ComparerSwissHashSet(IEqualityComparer<T> comparer, int capacity)
        : base(capacity, new SwissTableAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>(new ComparerHasher<T>(comparer), default))
        => Comparer = comparer;

    /// <summary>The comparer this set uses for item hashing and equality.</summary>
    public IEqualityComparer<T> Comparer { get; }

    /// <summary>
    /// Returns a set that uses <paramref name="comparer" /> for item equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new set. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerSwissHashSet<T> Create(IEqualityComparer<T>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<T>.Default;
        var cached = _threadCache;
        if (cached is not null)
        {
            _threadCache = null;
            if (ReferenceEquals(cached.Comparer, comparer))
            {
                return cached;
            }
            cached.ReleaseBuffers();
        }
        return new ComparerSwissHashSet<T>(comparer, capacity);
    }

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerSwissHashSet<T> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerSwissHashSet<T> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);
}

/// <summary>SwissTable + user-supplied <see cref="IEqualityComparer{T}" /> + user-chosen finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{T}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new set.
/// </remarks>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateComparerSwiss))]
public sealed class ComparerSwissHashSet<T, TFinalizer>
    : PooledHashSet<T, SwissTableAlgorithm<T, T, ComparerHasher<T>, TFinalizer>>
    where T : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static ComparerSwissHashSet<T, TFinalizer>? _threadCache;

    ComparerSwissHashSet(IEqualityComparer<T> comparer, int capacity)
        : base(capacity, new SwissTableAlgorithm<T, T, ComparerHasher<T>, TFinalizer>(new ComparerHasher<T>(comparer), default))
        => Comparer = comparer;

    /// <summary>The comparer this set uses for item hashing and equality.</summary>
    public IEqualityComparer<T> Comparer { get; }

    /// <summary>
    /// Returns a set that uses <paramref name="comparer" /> for item equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new set. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerSwissHashSet<T, TFinalizer> Create(IEqualityComparer<T>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<T>.Default;
        var cached = _threadCache;
        if (cached is not null)
        {
            _threadCache = null;
            if (ReferenceEquals(cached.Comparer, comparer))
            {
                return cached;
            }
            cached.ReleaseBuffers();
        }
        return new ComparerSwissHashSet<T, TFinalizer>(comparer, capacity);
    }

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerSwissHashSet<T, TFinalizer> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerSwissHashSet<T, TFinalizer> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);
}

// ---- Robin Hood ----

/// <summary>Robin Hood + default hasher + no finalizer.</summary>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateRobinHood))]
public sealed class RobinHoodHashSet<T>
    : PooledHashSet<T, RobinHoodAlgorithm<T, T, EqualityDefaultHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    [ThreadStatic]
    static RobinHoodHashSet<T>? _threadCache;

    RobinHoodHashSet(int capacity) : base(capacity) { }

    /// <summary>
    /// Returns a set from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashSet{T,TAlgo}.Dispose" />, which returns the instance to the
    /// same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new RobinHoodHashSet<T> Create() => TakeCached() ?? new RobinHoodHashSet<T>(0);

    /// <summary>
    /// Returns a set with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new RobinHoodHashSet<T> Create(int capacity) => TakeCached() ?? new RobinHoodHashSet<T>(capacity);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerRobinHoodHashSet{T}" />.
    /// </summary>
    [Obsolete(AliasErrors.RobinHoodHashSetUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static RobinHoodHashSet<T> Create(IEqualityComparer<T> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.RobinHoodHashSetUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static RobinHoodHashSet<T>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Robin Hood + default hasher + user-chosen finalizer.</summary>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateRobinHood))]
public sealed class RobinHoodHashSet<T, TFinalizer>
    : PooledHashSet<T, RobinHoodAlgorithm<T, T, EqualityDefaultHasher<T>, TFinalizer>>
    where T : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static RobinHoodHashSet<T, TFinalizer>? _threadCache;

    RobinHoodHashSet(int capacity) : base(capacity) { }

    /// <summary>
    /// Returns a set from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashSet{T,TAlgo}.Dispose" />, which returns the instance to the
    /// same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new RobinHoodHashSet<T, TFinalizer> Create() => TakeCached() ?? new RobinHoodHashSet<T, TFinalizer>(0);

    /// <summary>
    /// Returns a set with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new RobinHoodHashSet<T, TFinalizer> Create(int capacity) => TakeCached() ?? new RobinHoodHashSet<T, TFinalizer>(capacity);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerRobinHoodHashSet{T,TFinalizer}" />.
    /// </summary>
    [Obsolete(AliasErrors.RobinHoodHashSetFinalizerUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static RobinHoodHashSet<T, TFinalizer> Create(IEqualityComparer<T> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.RobinHoodHashSetFinalizerUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static RobinHoodHashSet<T, TFinalizer>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Robin Hood + user-supplied stateless struct hasher + user-chosen finalizer.</summary>
/// <remarks>
/// <typeparamref name="THasher" /> is always used as <c>default(THasher)</c>, so it must be
/// stateless — the JIT then inlines its hash and equality into every call. For a runtime
/// <see cref="IEqualityComparer{T}" /> use <c>ComparerRobinHoodHashSet</c>;
/// <see cref="ComparerHasher{T}" /> is rejected here with <see cref="NotSupportedException" />.
/// </remarks>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateRobinHood))]
public sealed class RobinHoodHashSet<T, THasher, TFinalizer>
    : PooledHashSet<T, RobinHoodAlgorithm<T, T, THasher, TFinalizer>>
    where T : notnull
    where THasher : struct, IHasher<T>
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static RobinHoodHashSet<T, THasher, TFinalizer>? _threadCache;

    RobinHoodHashSet(int capacity) : base(capacity) { }

    /// <summary>
    /// Returns a set from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashSet{T,TAlgo}.Dispose" />, which returns the instance to the
    /// same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new RobinHoodHashSet<T, THasher, TFinalizer> Create() => Create(0);

    /// <summary>
    /// Returns a set with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new RobinHoodHashSet<T, THasher, TFinalizer> Create(int capacity)
    {
        AliasGuards.EnsureStatelessHasher<T, THasher>();
        return TakeCached() ?? new RobinHoodHashSet<T, THasher, TFinalizer>(capacity);
    }

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static RobinHoodHashSet<T, THasher, TFinalizer>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Robin Hood + user-supplied <see cref="IEqualityComparer{T}" /> + no finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{T}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new set.
/// </remarks>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateComparerRobinHood))]
public sealed class ComparerRobinHoodHashSet<T>
    : PooledHashSet<T, RobinHoodAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    [ThreadStatic]
    static ComparerRobinHoodHashSet<T>? _threadCache;

    ComparerRobinHoodHashSet(IEqualityComparer<T> comparer, int capacity)
        : base(capacity, new RobinHoodAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>(new ComparerHasher<T>(comparer), default))
        => Comparer = comparer;

    /// <summary>The comparer this set uses for item hashing and equality.</summary>
    public IEqualityComparer<T> Comparer { get; }

    /// <summary>
    /// Returns a set that uses <paramref name="comparer" /> for item equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new set. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerRobinHoodHashSet<T> Create(IEqualityComparer<T>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<T>.Default;
        var cached = _threadCache;
        if (cached is not null)
        {
            _threadCache = null;
            if (ReferenceEquals(cached.Comparer, comparer))
            {
                return cached;
            }
            cached.ReleaseBuffers();
        }
        return new ComparerRobinHoodHashSet<T>(comparer, capacity);
    }

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerRobinHoodHashSet<T> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerRobinHoodHashSet<T> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);
}

/// <summary>Robin Hood + user-supplied <see cref="IEqualityComparer{T}" /> + user-chosen finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{T}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new set.
/// </remarks>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateComparerRobinHood))]
public sealed class ComparerRobinHoodHashSet<T, TFinalizer>
    : PooledHashSet<T, RobinHoodAlgorithm<T, T, ComparerHasher<T>, TFinalizer>>
    where T : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static ComparerRobinHoodHashSet<T, TFinalizer>? _threadCache;

    ComparerRobinHoodHashSet(IEqualityComparer<T> comparer, int capacity)
        : base(capacity, new RobinHoodAlgorithm<T, T, ComparerHasher<T>, TFinalizer>(new ComparerHasher<T>(comparer), default))
        => Comparer = comparer;

    /// <summary>The comparer this set uses for item hashing and equality.</summary>
    public IEqualityComparer<T> Comparer { get; }

    /// <summary>
    /// Returns a set that uses <paramref name="comparer" /> for item equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new set. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerRobinHoodHashSet<T, TFinalizer> Create(IEqualityComparer<T>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<T>.Default;
        var cached = _threadCache;
        if (cached is not null)
        {
            _threadCache = null;
            if (ReferenceEquals(cached.Comparer, comparer))
            {
                return cached;
            }
            cached.ReleaseBuffers();
        }
        return new ComparerRobinHoodHashSet<T, TFinalizer>(comparer, capacity);
    }

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerRobinHoodHashSet<T, TFinalizer> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerRobinHoodHashSet<T, TFinalizer> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);
}

// ---- Linear probing ----

/// <summary>Linear probing + default hasher + no finalizer.</summary>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateLinear))]
public sealed class LinearHashSet<T>
    : PooledHashSet<T, LinearProbingAlgorithm<T, T, EqualityDefaultHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    [ThreadStatic]
    static LinearHashSet<T>? _threadCache;

    LinearHashSet(int capacity) : base(capacity) { }

    /// <summary>
    /// Returns a set from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashSet{T,TAlgo}.Dispose" />, which returns the instance to the
    /// same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new LinearHashSet<T> Create() => TakeCached() ?? new LinearHashSet<T>(0);

    /// <summary>
    /// Returns a set with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new LinearHashSet<T> Create(int capacity) => TakeCached() ?? new LinearHashSet<T>(capacity);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerLinearHashSet{T}" />.
    /// </summary>
    [Obsolete(AliasErrors.LinearHashSetUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LinearHashSet<T> Create(IEqualityComparer<T> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.LinearHashSetUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static LinearHashSet<T>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Linear probing + default hasher + user-chosen finalizer.</summary>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateLinear))]
public sealed class LinearHashSet<T, TFinalizer>
    : PooledHashSet<T, LinearProbingAlgorithm<T, T, EqualityDefaultHasher<T>, TFinalizer>>
    where T : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static LinearHashSet<T, TFinalizer>? _threadCache;

    LinearHashSet(int capacity) : base(capacity) { }

    /// <summary>
    /// Returns a set from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashSet{T,TAlgo}.Dispose" />, which returns the instance to the
    /// same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new LinearHashSet<T, TFinalizer> Create() => TakeCached() ?? new LinearHashSet<T, TFinalizer>(0);

    /// <summary>
    /// Returns a set with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new LinearHashSet<T, TFinalizer> Create(int capacity) => TakeCached() ?? new LinearHashSet<T, TFinalizer>(capacity);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerLinearHashSet{T,TFinalizer}" />.
    /// </summary>
    [Obsolete(AliasErrors.LinearHashSetFinalizerUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LinearHashSet<T, TFinalizer> Create(IEqualityComparer<T> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.LinearHashSetFinalizerUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static LinearHashSet<T, TFinalizer>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Linear probing + user-supplied stateless struct hasher + user-chosen finalizer.</summary>
/// <remarks>
/// <typeparamref name="THasher" /> is always used as <c>default(THasher)</c>, so it must be
/// stateless — the JIT then inlines its hash and equality into every call. For a runtime
/// <see cref="IEqualityComparer{T}" /> use <c>ComparerLinearHashSet</c>;
/// <see cref="ComparerHasher{T}" /> is rejected here with <see cref="NotSupportedException" />.
/// </remarks>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateLinear))]
public sealed class LinearHashSet<T, THasher, TFinalizer>
    : PooledHashSet<T, LinearProbingAlgorithm<T, T, THasher, TFinalizer>>
    where T : notnull
    where THasher : struct, IHasher<T>
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static LinearHashSet<T, THasher, TFinalizer>? _threadCache;

    LinearHashSet(int capacity) : base(capacity) { }

    /// <summary>
    /// Returns a set from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashSet{T,TAlgo}.Dispose" />, which returns the instance to the
    /// same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new LinearHashSet<T, THasher, TFinalizer> Create() => Create(0);

    /// <summary>
    /// Returns a set with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new LinearHashSet<T, THasher, TFinalizer> Create(int capacity)
    {
        AliasGuards.EnsureStatelessHasher<T, THasher>();
        return TakeCached() ?? new LinearHashSet<T, THasher, TFinalizer>(capacity);
    }

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static LinearHashSet<T, THasher, TFinalizer>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Linear probing + user-supplied <see cref="IEqualityComparer{T}" /> + no finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{T}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new set.
/// </remarks>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateComparerLinear))]
public sealed class ComparerLinearHashSet<T>
    : PooledHashSet<T, LinearProbingAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    [ThreadStatic]
    static ComparerLinearHashSet<T>? _threadCache;

    ComparerLinearHashSet(IEqualityComparer<T> comparer, int capacity)
        : base(capacity, new LinearProbingAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>(new ComparerHasher<T>(comparer), default))
        => Comparer = comparer;

    /// <summary>The comparer this set uses for item hashing and equality.</summary>
    public IEqualityComparer<T> Comparer { get; }

    /// <summary>
    /// Returns a set that uses <paramref name="comparer" /> for item equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new set. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerLinearHashSet<T> Create(IEqualityComparer<T>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<T>.Default;
        var cached = _threadCache;
        if (cached is not null)
        {
            _threadCache = null;
            if (ReferenceEquals(cached.Comparer, comparer))
            {
                return cached;
            }
            cached.ReleaseBuffers();
        }
        return new ComparerLinearHashSet<T>(comparer, capacity);
    }

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerLinearHashSet<T> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerLinearHashSet<T> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);
}

/// <summary>Linear probing + user-supplied <see cref="IEqualityComparer{T}" /> + user-chosen finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{T}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new set.
/// </remarks>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateComparerLinear))]
public sealed class ComparerLinearHashSet<T, TFinalizer>
    : PooledHashSet<T, LinearProbingAlgorithm<T, T, ComparerHasher<T>, TFinalizer>>
    where T : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static ComparerLinearHashSet<T, TFinalizer>? _threadCache;

    ComparerLinearHashSet(IEqualityComparer<T> comparer, int capacity)
        : base(capacity, new LinearProbingAlgorithm<T, T, ComparerHasher<T>, TFinalizer>(new ComparerHasher<T>(comparer), default))
        => Comparer = comparer;

    /// <summary>The comparer this set uses for item hashing and equality.</summary>
    public IEqualityComparer<T> Comparer { get; }

    /// <summary>
    /// Returns a set that uses <paramref name="comparer" /> for item equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new set. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerLinearHashSet<T, TFinalizer> Create(IEqualityComparer<T>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<T>.Default;
        var cached = _threadCache;
        if (cached is not null)
        {
            _threadCache = null;
            if (ReferenceEquals(cached.Comparer, comparer))
            {
                return cached;
            }
            cached.ReleaseBuffers();
        }
        return new ComparerLinearHashSet<T, TFinalizer>(comparer, capacity);
    }

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerLinearHashSet<T, TFinalizer> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerLinearHashSet<T, TFinalizer> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);
}

// ---- Separate chaining ----

/// <summary>Separate chaining + default hasher + no finalizer.</summary>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateChained))]
public sealed class ChainedHashSet<T>
    : PooledHashSet<T, ChainedAlgorithm<T, T, EqualityDefaultHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    [ThreadStatic]
    static ChainedHashSet<T>? _threadCache;

    ChainedHashSet(int capacity) : base(capacity) { }

    /// <summary>
    /// Returns a set from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashSet{T,TAlgo}.Dispose" />, which returns the instance to the
    /// same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new ChainedHashSet<T> Create() => TakeCached() ?? new ChainedHashSet<T>(0);

    /// <summary>
    /// Returns a set with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new ChainedHashSet<T> Create(int capacity) => TakeCached() ?? new ChainedHashSet<T>(capacity);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerChainedHashSet{T}" />.
    /// </summary>
    [Obsolete(AliasErrors.ChainedHashSetUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ChainedHashSet<T> Create(IEqualityComparer<T> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.ChainedHashSetUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static ChainedHashSet<T>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Separate chaining + default hasher + user-chosen finalizer.</summary>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateChained))]
public sealed class ChainedHashSet<T, TFinalizer>
    : PooledHashSet<T, ChainedAlgorithm<T, T, EqualityDefaultHasher<T>, TFinalizer>>
    where T : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static ChainedHashSet<T, TFinalizer>? _threadCache;

    ChainedHashSet(int capacity) : base(capacity) { }

    /// <summary>
    /// Returns a set from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashSet{T,TAlgo}.Dispose" />, which returns the instance to the
    /// same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new ChainedHashSet<T, TFinalizer> Create() => TakeCached() ?? new ChainedHashSet<T, TFinalizer>(0);

    /// <summary>
    /// Returns a set with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new ChainedHashSet<T, TFinalizer> Create(int capacity) => TakeCached() ?? new ChainedHashSet<T, TFinalizer>(capacity);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerChainedHashSet{T,TFinalizer}" />.
    /// </summary>
    [Obsolete(AliasErrors.ChainedHashSetFinalizerUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ChainedHashSet<T, TFinalizer> Create(IEqualityComparer<T> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.ChainedHashSetFinalizerUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static ChainedHashSet<T, TFinalizer>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Separate chaining + user-supplied stateless struct hasher + user-chosen finalizer.</summary>
/// <remarks>
/// <typeparamref name="THasher" /> is always used as <c>default(THasher)</c>, so it must be
/// stateless — the JIT then inlines its hash and equality into every call. For a runtime
/// <see cref="IEqualityComparer{T}" /> use <c>ComparerChainedHashSet</c>;
/// <see cref="ComparerHasher{T}" /> is rejected here with <see cref="NotSupportedException" />.
/// </remarks>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateChained))]
public sealed class ChainedHashSet<T, THasher, TFinalizer>
    : PooledHashSet<T, ChainedAlgorithm<T, T, THasher, TFinalizer>>
    where T : notnull
    where THasher : struct, IHasher<T>
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static ChainedHashSet<T, THasher, TFinalizer>? _threadCache;

    ChainedHashSet(int capacity) : base(capacity) { }

    /// <summary>
    /// Returns a set from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashSet{T,TAlgo}.Dispose" />, which returns the instance to the
    /// same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new ChainedHashSet<T, THasher, TFinalizer> Create() => Create(0);

    /// <summary>
    /// Returns a set with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new ChainedHashSet<T, THasher, TFinalizer> Create(int capacity)
    {
        AliasGuards.EnsureStatelessHasher<T, THasher>();
        return TakeCached() ?? new ChainedHashSet<T, THasher, TFinalizer>(capacity);
    }

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static ChainedHashSet<T, THasher, TFinalizer>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Separate chaining + user-supplied <see cref="IEqualityComparer{T}" /> + no finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{T}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new set.
/// </remarks>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateComparerChained))]
public sealed class ComparerChainedHashSet<T>
    : PooledHashSet<T, ChainedAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    [ThreadStatic]
    static ComparerChainedHashSet<T>? _threadCache;

    ComparerChainedHashSet(IEqualityComparer<T> comparer, int capacity)
        : base(capacity, new ChainedAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>(new ComparerHasher<T>(comparer), default))
        => Comparer = comparer;

    /// <summary>The comparer this set uses for item hashing and equality.</summary>
    public IEqualityComparer<T> Comparer { get; }

    /// <summary>
    /// Returns a set that uses <paramref name="comparer" /> for item equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new set. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerChainedHashSet<T> Create(IEqualityComparer<T>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<T>.Default;
        var cached = _threadCache;
        if (cached is not null)
        {
            _threadCache = null;
            if (ReferenceEquals(cached.Comparer, comparer))
            {
                return cached;
            }
            cached.ReleaseBuffers();
        }
        return new ComparerChainedHashSet<T>(comparer, capacity);
    }

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerChainedHashSet<T> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerChainedHashSet<T> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);
}

/// <summary>Separate chaining + user-supplied <see cref="IEqualityComparer{T}" /> + user-chosen finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{T}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new set.
/// </remarks>
[CollectionBuilder(typeof(PooledHashSetCollectionBuilder), nameof(PooledHashSetCollectionBuilder.CreateComparerChained))]
public sealed class ComparerChainedHashSet<T, TFinalizer>
    : PooledHashSet<T, ChainedAlgorithm<T, T, ComparerHasher<T>, TFinalizer>>
    where T : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static ComparerChainedHashSet<T, TFinalizer>? _threadCache;

    ComparerChainedHashSet(IEqualityComparer<T> comparer, int capacity)
        : base(capacity, new ChainedAlgorithm<T, T, ComparerHasher<T>, TFinalizer>(new ComparerHasher<T>(comparer), default))
        => Comparer = comparer;

    /// <summary>The comparer this set uses for item hashing and equality.</summary>
    public IEqualityComparer<T> Comparer { get; }

    /// <summary>
    /// Returns a set that uses <paramref name="comparer" /> for item equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new set. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerChainedHashSet<T, TFinalizer> Create(IEqualityComparer<T>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<T>.Default;
        var cached = _threadCache;
        if (cached is not null)
        {
            _threadCache = null;
            if (ReferenceEquals(cached.Comparer, comparer))
            {
                return cached;
            }
            cached.ReleaseBuffers();
        }
        return new ComparerChainedHashSet<T, TFinalizer>(comparer, capacity);
    }

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerChainedHashSet<T, TFinalizer> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{T}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerChainedHashSet<T, TFinalizer> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);
}
