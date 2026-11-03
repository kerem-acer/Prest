using System.ComponentModel;
using System.Runtime.CompilerServices;

#pragma warning disable IDE0028 // The collection builder ends in these `new Alias(capacity)` calls; `[with(capacity)]` here would recurse.

namespace Prest;

// ---------------------------------------------------------------------------
// Concrete alias types for common PooledHashMap configurations.
//
// Every alias is a first-class type: its own static Create(...) returns the alias,
// so fields and parameters can use the short name
// (`PooledHashMap<string, int> _map = PooledHashMap<string, int>.Create(16);`).
// Each alias has its own per-thread single-slot cache; Dispose / Return put an
// instance back into the cache of its own type, never into the generic base's
// or a sibling alias's cache. Constructors are non-public — use Create(...).
//
// Shapes per algorithm ({Algo} = Swiss, RobinHood, Linear, Chained):
//   {Algo}HashMap<K,V>                     — EqualityDefaultHasher + NoOpHashFinalizer
//   {Algo}HashMap<K,V,TFinalizer>          — pick a finalizer (e.g. FibonacciFinalizer)
//   {Algo}HashMap<K,V,THasher,TFinalizer>  — plug in a stateless struct IHasher<K>
//   Comparer{Algo}HashMap<K,V>             — runtime IEqualityComparer<K>
//   Comparer{Algo}HashMap<K,V,TFinalizer>  — runtime IEqualityComparer<K> + finalizer
//
// PooledHashMap<K,V> is the default configuration (SwissTable). SwissHashMap<K,V>
// derives from it, so a SwissHashMap<K,V> can be passed wherever a
// PooledHashMap<K,V> is expected; the two still keep separate caches.
//
// Every alias supports collection expressions through PooledHashMapCollectionBuilder,
// which goes through the alias's own Create. Create(comparer) and [with(comparer)] on
// PooledHashMap<K,V> and the {Algo}HashMap<K,V> / <K,V,TFinalizer> aliases, and a
// Comparer* collection expression without a comparer, are compile errors that name the
// right type.
// ---------------------------------------------------------------------------

/// <summary>Default pooled hash map — SwissTable + default equality comparer + no finalizer.</summary>
/// <remarks>
/// Same configuration as <see cref="SwissHashMap{TKey,TValue}" />, which derives from this type.
/// Not sealed so that <c>Prest.ObjectPool</c> can subclass it; instances of subclasses never
/// enter this type's per-thread cache. Supports collection expressions, like every alias:
/// <c>PooledHashMap&lt;string, int&gt; map = [new("a", 1)];</c>
/// </remarks>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.Create))]
public class PooledHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, SwissTableAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    [ThreadStatic]
    static PooledHashMap<TKey, TValue>? _threadCache;

    protected PooledHashMap(int capacity = 0) : base(capacity) { }

    protected PooledHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }

    /// <summary>
    /// Returns a map from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.Dispose" />, which returns the instance
    /// to the same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new PooledHashMap<TKey, TValue> Create() => TakeCached() ?? new PooledHashMap<TKey, TValue>(0);

    /// <summary>
    /// Returns a map with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new PooledHashMap<TKey, TValue> Create(int capacity) => TakeCached() ?? new PooledHashMap<TKey, TValue>(capacity);

    /// <summary>
    /// Creates a fresh map pre-populated from <paramref name="rentedKeys" /> and
    /// <paramref name="rentedValues" />, taking ownership of both arrays (they go back to
    /// <see cref="System.Buffers.ArrayPool{T}.Shared" />). Bypasses the per-thread cache.
    /// </summary>
    public static new PooledHashMap<TKey, TValue> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(rentedKeys, rentedValues, count);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerSwissHashMap{TKey,TValue}" />.
    /// </summary>
    [Obsolete(AliasErrors.PooledHashMapUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static PooledHashMap<TKey, TValue> Create(IEqualityComparer<TKey> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.PooledHashMapUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static PooledHashMap<TKey, TValue>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

// ---- SwissTable ----

/// <summary>SwissTable + default hasher + no finalizer. Derives from <see cref="PooledHashMap{TKey,TValue}" /> (same configuration).</summary>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateSwiss))]
public sealed class SwissHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue>
    where TKey : notnull
{
    [ThreadStatic]
    static SwissHashMap<TKey, TValue>? _threadCache;

    SwissHashMap(int capacity) : base(capacity) { }

    SwissHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }

    /// <summary>
    /// Returns a map from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.Dispose" />, which returns the instance
    /// to the same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new SwissHashMap<TKey, TValue> Create() => TakeCached() ?? new SwissHashMap<TKey, TValue>(0);

    /// <summary>
    /// Returns a map with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new SwissHashMap<TKey, TValue> Create(int capacity) => TakeCached() ?? new SwissHashMap<TKey, TValue>(capacity);

    /// <summary>
    /// Creates a fresh map pre-populated from <paramref name="rentedKeys" /> and
    /// <paramref name="rentedValues" />, taking ownership of both arrays (they go back to
    /// <see cref="System.Buffers.ArrayPool{T}.Shared" />). Bypasses the per-thread cache.
    /// </summary>
    public static new SwissHashMap<TKey, TValue> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(rentedKeys, rentedValues, count);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerSwissHashMap{TKey,TValue}" />.
    /// </summary>
    [Obsolete(AliasErrors.SwissHashMapUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new SwissHashMap<TKey, TValue> Create(IEqualityComparer<TKey> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.SwissHashMapUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static SwissHashMap<TKey, TValue>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>SwissTable + default hasher + user-chosen finalizer.</summary>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateSwiss))]
public sealed class SwissHashMap<TKey, TValue, TFinalizer>
    : PooledHashMap<TKey, TValue, SwissTableAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, TFinalizer>>
    where TKey : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static SwissHashMap<TKey, TValue, TFinalizer>? _threadCache;

    SwissHashMap(int capacity) : base(capacity) { }

    SwissHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }

    /// <summary>
    /// Returns a map from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.Dispose" />, which returns the instance
    /// to the same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new SwissHashMap<TKey, TValue, TFinalizer> Create() => TakeCached() ?? new SwissHashMap<TKey, TValue, TFinalizer>(0);

    /// <summary>
    /// Returns a map with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new SwissHashMap<TKey, TValue, TFinalizer> Create(int capacity) => TakeCached() ?? new SwissHashMap<TKey, TValue, TFinalizer>(capacity);

    /// <summary>
    /// Creates a fresh map pre-populated from <paramref name="rentedKeys" /> and
    /// <paramref name="rentedValues" />, taking ownership of both arrays (they go back to
    /// <see cref="System.Buffers.ArrayPool{T}.Shared" />). Bypasses the per-thread cache.
    /// </summary>
    public static new SwissHashMap<TKey, TValue, TFinalizer> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(rentedKeys, rentedValues, count);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerSwissHashMap{TKey,TValue,TFinalizer}" />.
    /// </summary>
    [Obsolete(AliasErrors.SwissHashMapFinalizerUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static SwissHashMap<TKey, TValue, TFinalizer> Create(IEqualityComparer<TKey> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.SwissHashMapFinalizerUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static SwissHashMap<TKey, TValue, TFinalizer>? TakeCached()
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
/// <see cref="IEqualityComparer{T}" /> use <c>ComparerSwissHashMap</c>;
/// <see cref="ComparerHasher{T}" /> is rejected here with <see cref="NotSupportedException" />.
/// </remarks>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateSwiss))]
public sealed class SwissHashMap<TKey, TValue, THasher, TFinalizer>
    : PooledHashMap<TKey, TValue, SwissTableAlgorithm<KeyValueSlot<TKey, TValue>, TKey, THasher, TFinalizer>>
    where TKey : notnull
    where THasher : struct, IHasher<TKey>
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static SwissHashMap<TKey, TValue, THasher, TFinalizer>? _threadCache;

    SwissHashMap(int capacity) : base(capacity) { }

    SwissHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }

    /// <summary>
    /// Returns a map from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.Dispose" />, which returns the instance
    /// to the same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new SwissHashMap<TKey, TValue, THasher, TFinalizer> Create() => Create(0);

    /// <summary>
    /// Returns a map with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new SwissHashMap<TKey, TValue, THasher, TFinalizer> Create(int capacity)
    {
        AliasGuards.EnsureStatelessHasher<TKey, THasher>();
        return TakeCached() ?? new SwissHashMap<TKey, TValue, THasher, TFinalizer>(capacity);
    }

    /// <summary>
    /// Creates a fresh map pre-populated from <paramref name="rentedKeys" /> and
    /// <paramref name="rentedValues" />, taking ownership of both arrays (they go back to
    /// <see cref="System.Buffers.ArrayPool{T}.Shared" />). Bypasses the per-thread cache.
    /// </summary>
    public static new SwissHashMap<TKey, TValue, THasher, TFinalizer> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
    {
        AliasGuards.EnsureStatelessHasher<TKey, THasher>();
        return new(rentedKeys, rentedValues, count);
    }

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static SwissHashMap<TKey, TValue, THasher, TFinalizer>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>SwissTable + user-supplied <see cref="IEqualityComparer{TKey}" /> + no finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{TKey}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new map.
/// </remarks>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateComparerSwiss))]
public sealed class ComparerSwissHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, SwissTableAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    [ThreadStatic]
    static ComparerSwissHashMap<TKey, TValue>? _threadCache;

    ComparerSwissHashMap(IEqualityComparer<TKey> comparer, int capacity)
        : base(capacity, Algorithm(comparer)) => Comparer = comparer;

    ComparerSwissHashMap(IEqualityComparer<TKey> comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count, Algorithm(comparer)) => Comparer = comparer;

    /// <summary>The comparer this map uses for key hashing and equality.</summary>
    public IEqualityComparer<TKey> Comparer { get; }

    /// <summary>
    /// Returns a map that uses <paramref name="comparer" /> for key equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new map. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerSwissHashMap<TKey, TValue> Create(IEqualityComparer<TKey>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<TKey>.Default;
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
        return new ComparerSwissHashMap<TKey, TValue>(comparer, capacity);
    }

    /// <summary>
    /// Creates a fresh map that uses <paramref name="comparer" />, pre-populated from
    /// <paramref name="rentedKeys" /> and <paramref name="rentedValues" /> and taking ownership
    /// of both arrays. Bypasses the per-thread cache.
    /// </summary>
    public static ComparerSwissHashMap<TKey, TValue> Create(
        IEqualityComparer<TKey>? comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(comparer ?? EqualityComparer<TKey>.Default, rentedKeys, rentedValues, count);

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerSwissHashMap<TKey, TValue> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerSwissHashMap<TKey, TValue> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, TKey[], TValue[], int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerSwissHashMap<TKey, TValue> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static SwissTableAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer> Algorithm(IEqualityComparer<TKey> comparer)
        => new(new ComparerHasher<TKey>(comparer), default);
}

/// <summary>SwissTable + user-supplied <see cref="IEqualityComparer{TKey}" /> + user-chosen finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{TKey}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new map.
/// </remarks>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateComparerSwiss))]
public sealed class ComparerSwissHashMap<TKey, TValue, TFinalizer>
    : PooledHashMap<TKey, TValue, SwissTableAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, TFinalizer>>
    where TKey : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static ComparerSwissHashMap<TKey, TValue, TFinalizer>? _threadCache;

    ComparerSwissHashMap(IEqualityComparer<TKey> comparer, int capacity)
        : base(capacity, Algorithm(comparer)) => Comparer = comparer;

    ComparerSwissHashMap(IEqualityComparer<TKey> comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count, Algorithm(comparer)) => Comparer = comparer;

    /// <summary>The comparer this map uses for key hashing and equality.</summary>
    public IEqualityComparer<TKey> Comparer { get; }

    /// <summary>
    /// Returns a map that uses <paramref name="comparer" /> for key equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new map. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerSwissHashMap<TKey, TValue, TFinalizer> Create(IEqualityComparer<TKey>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<TKey>.Default;
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
        return new ComparerSwissHashMap<TKey, TValue, TFinalizer>(comparer, capacity);
    }

    /// <summary>
    /// Creates a fresh map that uses <paramref name="comparer" />, pre-populated from
    /// <paramref name="rentedKeys" /> and <paramref name="rentedValues" /> and taking ownership
    /// of both arrays. Bypasses the per-thread cache.
    /// </summary>
    public static ComparerSwissHashMap<TKey, TValue, TFinalizer> Create(
        IEqualityComparer<TKey>? comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(comparer ?? EqualityComparer<TKey>.Default, rentedKeys, rentedValues, count);

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerSwissHashMap<TKey, TValue, TFinalizer> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerSwissHashMap<TKey, TValue, TFinalizer> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, TKey[], TValue[], int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerSwissHashMap<TKey, TValue, TFinalizer> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static SwissTableAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, TFinalizer> Algorithm(IEqualityComparer<TKey> comparer)
        => new(new ComparerHasher<TKey>(comparer), default);
}

// ---- Robin Hood ----

/// <summary>Robin Hood + default hasher + no finalizer.</summary>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateRobinHood))]
public sealed class RobinHoodHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, RobinHoodAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    [ThreadStatic]
    static RobinHoodHashMap<TKey, TValue>? _threadCache;

    RobinHoodHashMap(int capacity) : base(capacity) { }

    RobinHoodHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }

    /// <summary>
    /// Returns a map from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.Dispose" />, which returns the instance
    /// to the same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new RobinHoodHashMap<TKey, TValue> Create() => TakeCached() ?? new RobinHoodHashMap<TKey, TValue>(0);

    /// <summary>
    /// Returns a map with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new RobinHoodHashMap<TKey, TValue> Create(int capacity) => TakeCached() ?? new RobinHoodHashMap<TKey, TValue>(capacity);

    /// <summary>
    /// Creates a fresh map pre-populated from <paramref name="rentedKeys" /> and
    /// <paramref name="rentedValues" />, taking ownership of both arrays (they go back to
    /// <see cref="System.Buffers.ArrayPool{T}.Shared" />). Bypasses the per-thread cache.
    /// </summary>
    public static new RobinHoodHashMap<TKey, TValue> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(rentedKeys, rentedValues, count);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerRobinHoodHashMap{TKey,TValue}" />.
    /// </summary>
    [Obsolete(AliasErrors.RobinHoodHashMapUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static RobinHoodHashMap<TKey, TValue> Create(IEqualityComparer<TKey> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.RobinHoodHashMapUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static RobinHoodHashMap<TKey, TValue>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Robin Hood + default hasher + user-chosen finalizer.</summary>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateRobinHood))]
public sealed class RobinHoodHashMap<TKey, TValue, TFinalizer>
    : PooledHashMap<TKey, TValue, RobinHoodAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, TFinalizer>>
    where TKey : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static RobinHoodHashMap<TKey, TValue, TFinalizer>? _threadCache;

    RobinHoodHashMap(int capacity) : base(capacity) { }

    RobinHoodHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }

    /// <summary>
    /// Returns a map from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.Dispose" />, which returns the instance
    /// to the same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new RobinHoodHashMap<TKey, TValue, TFinalizer> Create() => TakeCached() ?? new RobinHoodHashMap<TKey, TValue, TFinalizer>(0);

    /// <summary>
    /// Returns a map with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new RobinHoodHashMap<TKey, TValue, TFinalizer> Create(int capacity) => TakeCached() ?? new RobinHoodHashMap<TKey, TValue, TFinalizer>(capacity);

    /// <summary>
    /// Creates a fresh map pre-populated from <paramref name="rentedKeys" /> and
    /// <paramref name="rentedValues" />, taking ownership of both arrays (they go back to
    /// <see cref="System.Buffers.ArrayPool{T}.Shared" />). Bypasses the per-thread cache.
    /// </summary>
    public static new RobinHoodHashMap<TKey, TValue, TFinalizer> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(rentedKeys, rentedValues, count);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerRobinHoodHashMap{TKey,TValue,TFinalizer}" />.
    /// </summary>
    [Obsolete(AliasErrors.RobinHoodHashMapFinalizerUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static RobinHoodHashMap<TKey, TValue, TFinalizer> Create(IEqualityComparer<TKey> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.RobinHoodHashMapFinalizerUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static RobinHoodHashMap<TKey, TValue, TFinalizer>? TakeCached()
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
/// <see cref="IEqualityComparer{T}" /> use <c>ComparerRobinHoodHashMap</c>;
/// <see cref="ComparerHasher{T}" /> is rejected here with <see cref="NotSupportedException" />.
/// </remarks>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateRobinHood))]
public sealed class RobinHoodHashMap<TKey, TValue, THasher, TFinalizer>
    : PooledHashMap<TKey, TValue, RobinHoodAlgorithm<KeyValueSlot<TKey, TValue>, TKey, THasher, TFinalizer>>
    where TKey : notnull
    where THasher : struct, IHasher<TKey>
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static RobinHoodHashMap<TKey, TValue, THasher, TFinalizer>? _threadCache;

    RobinHoodHashMap(int capacity) : base(capacity) { }

    RobinHoodHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }

    /// <summary>
    /// Returns a map from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.Dispose" />, which returns the instance
    /// to the same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new RobinHoodHashMap<TKey, TValue, THasher, TFinalizer> Create() => Create(0);

    /// <summary>
    /// Returns a map with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new RobinHoodHashMap<TKey, TValue, THasher, TFinalizer> Create(int capacity)
    {
        AliasGuards.EnsureStatelessHasher<TKey, THasher>();
        return TakeCached() ?? new RobinHoodHashMap<TKey, TValue, THasher, TFinalizer>(capacity);
    }

    /// <summary>
    /// Creates a fresh map pre-populated from <paramref name="rentedKeys" /> and
    /// <paramref name="rentedValues" />, taking ownership of both arrays (they go back to
    /// <see cref="System.Buffers.ArrayPool{T}.Shared" />). Bypasses the per-thread cache.
    /// </summary>
    public static new RobinHoodHashMap<TKey, TValue, THasher, TFinalizer> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
    {
        AliasGuards.EnsureStatelessHasher<TKey, THasher>();
        return new(rentedKeys, rentedValues, count);
    }

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static RobinHoodHashMap<TKey, TValue, THasher, TFinalizer>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Robin Hood + user-supplied <see cref="IEqualityComparer{TKey}" /> + no finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{TKey}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new map.
/// </remarks>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateComparerRobinHood))]
public sealed class ComparerRobinHoodHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, RobinHoodAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    [ThreadStatic]
    static ComparerRobinHoodHashMap<TKey, TValue>? _threadCache;

    ComparerRobinHoodHashMap(IEqualityComparer<TKey> comparer, int capacity)
        : base(capacity, Algorithm(comparer)) => Comparer = comparer;

    ComparerRobinHoodHashMap(IEqualityComparer<TKey> comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count, Algorithm(comparer)) => Comparer = comparer;

    /// <summary>The comparer this map uses for key hashing and equality.</summary>
    public IEqualityComparer<TKey> Comparer { get; }

    /// <summary>
    /// Returns a map that uses <paramref name="comparer" /> for key equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new map. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerRobinHoodHashMap<TKey, TValue> Create(IEqualityComparer<TKey>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<TKey>.Default;
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
        return new ComparerRobinHoodHashMap<TKey, TValue>(comparer, capacity);
    }

    /// <summary>
    /// Creates a fresh map that uses <paramref name="comparer" />, pre-populated from
    /// <paramref name="rentedKeys" /> and <paramref name="rentedValues" /> and taking ownership
    /// of both arrays. Bypasses the per-thread cache.
    /// </summary>
    public static ComparerRobinHoodHashMap<TKey, TValue> Create(
        IEqualityComparer<TKey>? comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(comparer ?? EqualityComparer<TKey>.Default, rentedKeys, rentedValues, count);

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerRobinHoodHashMap<TKey, TValue> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerRobinHoodHashMap<TKey, TValue> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, TKey[], TValue[], int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerRobinHoodHashMap<TKey, TValue> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static RobinHoodAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer> Algorithm(IEqualityComparer<TKey> comparer)
        => new(new ComparerHasher<TKey>(comparer), default);
}

/// <summary>Robin Hood + user-supplied <see cref="IEqualityComparer{TKey}" /> + user-chosen finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{TKey}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new map.
/// </remarks>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateComparerRobinHood))]
public sealed class ComparerRobinHoodHashMap<TKey, TValue, TFinalizer>
    : PooledHashMap<TKey, TValue, RobinHoodAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, TFinalizer>>
    where TKey : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static ComparerRobinHoodHashMap<TKey, TValue, TFinalizer>? _threadCache;

    ComparerRobinHoodHashMap(IEqualityComparer<TKey> comparer, int capacity)
        : base(capacity, Algorithm(comparer)) => Comparer = comparer;

    ComparerRobinHoodHashMap(IEqualityComparer<TKey> comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count, Algorithm(comparer)) => Comparer = comparer;

    /// <summary>The comparer this map uses for key hashing and equality.</summary>
    public IEqualityComparer<TKey> Comparer { get; }

    /// <summary>
    /// Returns a map that uses <paramref name="comparer" /> for key equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new map. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerRobinHoodHashMap<TKey, TValue, TFinalizer> Create(IEqualityComparer<TKey>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<TKey>.Default;
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
        return new ComparerRobinHoodHashMap<TKey, TValue, TFinalizer>(comparer, capacity);
    }

    /// <summary>
    /// Creates a fresh map that uses <paramref name="comparer" />, pre-populated from
    /// <paramref name="rentedKeys" /> and <paramref name="rentedValues" /> and taking ownership
    /// of both arrays. Bypasses the per-thread cache.
    /// </summary>
    public static ComparerRobinHoodHashMap<TKey, TValue, TFinalizer> Create(
        IEqualityComparer<TKey>? comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(comparer ?? EqualityComparer<TKey>.Default, rentedKeys, rentedValues, count);

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerRobinHoodHashMap<TKey, TValue, TFinalizer> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerRobinHoodHashMap<TKey, TValue, TFinalizer> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, TKey[], TValue[], int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerRobinHoodHashMap<TKey, TValue, TFinalizer> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static RobinHoodAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, TFinalizer> Algorithm(IEqualityComparer<TKey> comparer)
        => new(new ComparerHasher<TKey>(comparer), default);
}

// ---- Linear probing ----

/// <summary>Linear probing + default hasher + no finalizer.</summary>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateLinear))]
public sealed class LinearHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, LinearProbingAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    [ThreadStatic]
    static LinearHashMap<TKey, TValue>? _threadCache;

    LinearHashMap(int capacity) : base(capacity) { }

    LinearHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }

    /// <summary>
    /// Returns a map from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.Dispose" />, which returns the instance
    /// to the same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new LinearHashMap<TKey, TValue> Create() => TakeCached() ?? new LinearHashMap<TKey, TValue>(0);

    /// <summary>
    /// Returns a map with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new LinearHashMap<TKey, TValue> Create(int capacity) => TakeCached() ?? new LinearHashMap<TKey, TValue>(capacity);

    /// <summary>
    /// Creates a fresh map pre-populated from <paramref name="rentedKeys" /> and
    /// <paramref name="rentedValues" />, taking ownership of both arrays (they go back to
    /// <see cref="System.Buffers.ArrayPool{T}.Shared" />). Bypasses the per-thread cache.
    /// </summary>
    public static new LinearHashMap<TKey, TValue> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(rentedKeys, rentedValues, count);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerLinearHashMap{TKey,TValue}" />.
    /// </summary>
    [Obsolete(AliasErrors.LinearHashMapUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LinearHashMap<TKey, TValue> Create(IEqualityComparer<TKey> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.LinearHashMapUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static LinearHashMap<TKey, TValue>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Linear probing + default hasher + user-chosen finalizer.</summary>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateLinear))]
public sealed class LinearHashMap<TKey, TValue, TFinalizer>
    : PooledHashMap<TKey, TValue, LinearProbingAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, TFinalizer>>
    where TKey : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static LinearHashMap<TKey, TValue, TFinalizer>? _threadCache;

    LinearHashMap(int capacity) : base(capacity) { }

    LinearHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }

    /// <summary>
    /// Returns a map from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.Dispose" />, which returns the instance
    /// to the same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new LinearHashMap<TKey, TValue, TFinalizer> Create() => TakeCached() ?? new LinearHashMap<TKey, TValue, TFinalizer>(0);

    /// <summary>
    /// Returns a map with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new LinearHashMap<TKey, TValue, TFinalizer> Create(int capacity) => TakeCached() ?? new LinearHashMap<TKey, TValue, TFinalizer>(capacity);

    /// <summary>
    /// Creates a fresh map pre-populated from <paramref name="rentedKeys" /> and
    /// <paramref name="rentedValues" />, taking ownership of both arrays (they go back to
    /// <see cref="System.Buffers.ArrayPool{T}.Shared" />). Bypasses the per-thread cache.
    /// </summary>
    public static new LinearHashMap<TKey, TValue, TFinalizer> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(rentedKeys, rentedValues, count);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerLinearHashMap{TKey,TValue,TFinalizer}" />.
    /// </summary>
    [Obsolete(AliasErrors.LinearHashMapFinalizerUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LinearHashMap<TKey, TValue, TFinalizer> Create(IEqualityComparer<TKey> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.LinearHashMapFinalizerUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static LinearHashMap<TKey, TValue, TFinalizer>? TakeCached()
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
/// <see cref="IEqualityComparer{T}" /> use <c>ComparerLinearHashMap</c>;
/// <see cref="ComparerHasher{T}" /> is rejected here with <see cref="NotSupportedException" />.
/// </remarks>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateLinear))]
public sealed class LinearHashMap<TKey, TValue, THasher, TFinalizer>
    : PooledHashMap<TKey, TValue, LinearProbingAlgorithm<KeyValueSlot<TKey, TValue>, TKey, THasher, TFinalizer>>
    where TKey : notnull
    where THasher : struct, IHasher<TKey>
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static LinearHashMap<TKey, TValue, THasher, TFinalizer>? _threadCache;

    LinearHashMap(int capacity) : base(capacity) { }

    LinearHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }

    /// <summary>
    /// Returns a map from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.Dispose" />, which returns the instance
    /// to the same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new LinearHashMap<TKey, TValue, THasher, TFinalizer> Create() => Create(0);

    /// <summary>
    /// Returns a map with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new LinearHashMap<TKey, TValue, THasher, TFinalizer> Create(int capacity)
    {
        AliasGuards.EnsureStatelessHasher<TKey, THasher>();
        return TakeCached() ?? new LinearHashMap<TKey, TValue, THasher, TFinalizer>(capacity);
    }

    /// <summary>
    /// Creates a fresh map pre-populated from <paramref name="rentedKeys" /> and
    /// <paramref name="rentedValues" />, taking ownership of both arrays (they go back to
    /// <see cref="System.Buffers.ArrayPool{T}.Shared" />). Bypasses the per-thread cache.
    /// </summary>
    public static new LinearHashMap<TKey, TValue, THasher, TFinalizer> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
    {
        AliasGuards.EnsureStatelessHasher<TKey, THasher>();
        return new(rentedKeys, rentedValues, count);
    }

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static LinearHashMap<TKey, TValue, THasher, TFinalizer>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Linear probing + user-supplied <see cref="IEqualityComparer{TKey}" /> + no finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{TKey}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new map.
/// </remarks>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateComparerLinear))]
public sealed class ComparerLinearHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, LinearProbingAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    [ThreadStatic]
    static ComparerLinearHashMap<TKey, TValue>? _threadCache;

    ComparerLinearHashMap(IEqualityComparer<TKey> comparer, int capacity)
        : base(capacity, Algorithm(comparer)) => Comparer = comparer;

    ComparerLinearHashMap(IEqualityComparer<TKey> comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count, Algorithm(comparer)) => Comparer = comparer;

    /// <summary>The comparer this map uses for key hashing and equality.</summary>
    public IEqualityComparer<TKey> Comparer { get; }

    /// <summary>
    /// Returns a map that uses <paramref name="comparer" /> for key equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new map. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerLinearHashMap<TKey, TValue> Create(IEqualityComparer<TKey>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<TKey>.Default;
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
        return new ComparerLinearHashMap<TKey, TValue>(comparer, capacity);
    }

    /// <summary>
    /// Creates a fresh map that uses <paramref name="comparer" />, pre-populated from
    /// <paramref name="rentedKeys" /> and <paramref name="rentedValues" /> and taking ownership
    /// of both arrays. Bypasses the per-thread cache.
    /// </summary>
    public static ComparerLinearHashMap<TKey, TValue> Create(
        IEqualityComparer<TKey>? comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(comparer ?? EqualityComparer<TKey>.Default, rentedKeys, rentedValues, count);

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerLinearHashMap<TKey, TValue> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerLinearHashMap<TKey, TValue> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, TKey[], TValue[], int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerLinearHashMap<TKey, TValue> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static LinearProbingAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer> Algorithm(IEqualityComparer<TKey> comparer)
        => new(new ComparerHasher<TKey>(comparer), default);
}

/// <summary>Linear probing + user-supplied <see cref="IEqualityComparer{TKey}" /> + user-chosen finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{TKey}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new map.
/// </remarks>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateComparerLinear))]
public sealed class ComparerLinearHashMap<TKey, TValue, TFinalizer>
    : PooledHashMap<TKey, TValue, LinearProbingAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, TFinalizer>>
    where TKey : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static ComparerLinearHashMap<TKey, TValue, TFinalizer>? _threadCache;

    ComparerLinearHashMap(IEqualityComparer<TKey> comparer, int capacity)
        : base(capacity, Algorithm(comparer)) => Comparer = comparer;

    ComparerLinearHashMap(IEqualityComparer<TKey> comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count, Algorithm(comparer)) => Comparer = comparer;

    /// <summary>The comparer this map uses for key hashing and equality.</summary>
    public IEqualityComparer<TKey> Comparer { get; }

    /// <summary>
    /// Returns a map that uses <paramref name="comparer" /> for key equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new map. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerLinearHashMap<TKey, TValue, TFinalizer> Create(IEqualityComparer<TKey>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<TKey>.Default;
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
        return new ComparerLinearHashMap<TKey, TValue, TFinalizer>(comparer, capacity);
    }

    /// <summary>
    /// Creates a fresh map that uses <paramref name="comparer" />, pre-populated from
    /// <paramref name="rentedKeys" /> and <paramref name="rentedValues" /> and taking ownership
    /// of both arrays. Bypasses the per-thread cache.
    /// </summary>
    public static ComparerLinearHashMap<TKey, TValue, TFinalizer> Create(
        IEqualityComparer<TKey>? comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(comparer ?? EqualityComparer<TKey>.Default, rentedKeys, rentedValues, count);

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerLinearHashMap<TKey, TValue, TFinalizer> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerLinearHashMap<TKey, TValue, TFinalizer> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, TKey[], TValue[], int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerLinearHashMap<TKey, TValue, TFinalizer> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static LinearProbingAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, TFinalizer> Algorithm(IEqualityComparer<TKey> comparer)
        => new(new ComparerHasher<TKey>(comparer), default);
}

// ---- Separate chaining ----

/// <summary>Separate chaining + default hasher + no finalizer.</summary>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateChained))]
public sealed class ChainedHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, ChainedAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    [ThreadStatic]
    static ChainedHashMap<TKey, TValue>? _threadCache;

    ChainedHashMap(int capacity) : base(capacity) { }

    ChainedHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }

    /// <summary>
    /// Returns a map from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.Dispose" />, which returns the instance
    /// to the same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new ChainedHashMap<TKey, TValue> Create() => TakeCached() ?? new ChainedHashMap<TKey, TValue>(0);

    /// <summary>
    /// Returns a map with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new ChainedHashMap<TKey, TValue> Create(int capacity) => TakeCached() ?? new ChainedHashMap<TKey, TValue>(capacity);

    /// <summary>
    /// Creates a fresh map pre-populated from <paramref name="rentedKeys" /> and
    /// <paramref name="rentedValues" />, taking ownership of both arrays (they go back to
    /// <see cref="System.Buffers.ArrayPool{T}.Shared" />). Bypasses the per-thread cache.
    /// </summary>
    public static new ChainedHashMap<TKey, TValue> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(rentedKeys, rentedValues, count);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerChainedHashMap{TKey,TValue}" />.
    /// </summary>
    [Obsolete(AliasErrors.ChainedHashMapUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ChainedHashMap<TKey, TValue> Create(IEqualityComparer<TKey> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.ChainedHashMapUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static ChainedHashMap<TKey, TValue>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Separate chaining + default hasher + user-chosen finalizer.</summary>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateChained))]
public sealed class ChainedHashMap<TKey, TValue, TFinalizer>
    : PooledHashMap<TKey, TValue, ChainedAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, TFinalizer>>
    where TKey : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static ChainedHashMap<TKey, TValue, TFinalizer>? _threadCache;

    ChainedHashMap(int capacity) : base(capacity) { }

    ChainedHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }

    /// <summary>
    /// Returns a map from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.Dispose" />, which returns the instance
    /// to the same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new ChainedHashMap<TKey, TValue, TFinalizer> Create() => TakeCached() ?? new ChainedHashMap<TKey, TValue, TFinalizer>(0);

    /// <summary>
    /// Returns a map with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new ChainedHashMap<TKey, TValue, TFinalizer> Create(int capacity) => TakeCached() ?? new ChainedHashMap<TKey, TValue, TFinalizer>(capacity);

    /// <summary>
    /// Creates a fresh map pre-populated from <paramref name="rentedKeys" /> and
    /// <paramref name="rentedValues" />, taking ownership of both arrays (they go back to
    /// <see cref="System.Buffers.ArrayPool{T}.Shared" />). Bypasses the per-thread cache.
    /// </summary>
    public static new ChainedHashMap<TKey, TValue, TFinalizer> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(rentedKeys, rentedValues, count);

    /// <summary>
    /// Not supported — this alias always uses <see cref="EqualityComparer{T}.Default" />. For a
    /// custom comparer use <see cref="ComparerChainedHashMap{TKey,TValue,TFinalizer}" />.
    /// </summary>
    [Obsolete(AliasErrors.ChainedHashMapFinalizerUsesDefaultComparer, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ChainedHashMap<TKey, TValue, TFinalizer> Create(IEqualityComparer<TKey> comparer, int capacity = 0)
        => throw AliasErrors.NotSupported(AliasErrors.ChainedHashMapFinalizerUsesDefaultComparer);

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static ChainedHashMap<TKey, TValue, TFinalizer>? TakeCached()
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
/// <see cref="IEqualityComparer{T}" /> use <c>ComparerChainedHashMap</c>;
/// <see cref="ComparerHasher{T}" /> is rejected here with <see cref="NotSupportedException" />.
/// </remarks>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateChained))]
public sealed class ChainedHashMap<TKey, TValue, THasher, TFinalizer>
    : PooledHashMap<TKey, TValue, ChainedAlgorithm<KeyValueSlot<TKey, TValue>, TKey, THasher, TFinalizer>>
    where TKey : notnull
    where THasher : struct, IHasher<TKey>
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static ChainedHashMap<TKey, TValue, THasher, TFinalizer>? _threadCache;

    ChainedHashMap(int capacity) : base(capacity) { }

    ChainedHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }

    /// <summary>
    /// Returns a map from this type's per-thread cache, or allocates a new one. Pair with
    /// <see cref="PooledHashMap{TKey,TValue,TAlgo}.Dispose" />, which returns the instance
    /// to the same cache.
    /// </summary>
    /// <remarks>
    /// Single-slot cache per thread, separate for every alias type. A second
    /// <see cref="Create()" /> before the first is returned allocates fresh.
    /// </remarks>
    public static new ChainedHashMap<TKey, TValue, THasher, TFinalizer> Create() => Create(0);

    /// <summary>
    /// Returns a map with at least <paramref name="capacity" /> slots pre-allocated.
    /// A cached instance is reused regardless of its current capacity; the hint
    /// applies only when allocating a fresh instance.
    /// </summary>
    public static new ChainedHashMap<TKey, TValue, THasher, TFinalizer> Create(int capacity)
    {
        AliasGuards.EnsureStatelessHasher<TKey, THasher>();
        return TakeCached() ?? new ChainedHashMap<TKey, TValue, THasher, TFinalizer>(capacity);
    }

    /// <summary>
    /// Creates a fresh map pre-populated from <paramref name="rentedKeys" /> and
    /// <paramref name="rentedValues" />, taking ownership of both arrays (they go back to
    /// <see cref="System.Buffers.ArrayPool{T}.Shared" />). Bypasses the per-thread cache.
    /// </summary>
    public static new ChainedHashMap<TKey, TValue, THasher, TFinalizer> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
    {
        AliasGuards.EnsureStatelessHasher<TKey, THasher>();
        return new(rentedKeys, rentedValues, count);
    }

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static ChainedHashMap<TKey, TValue, THasher, TFinalizer>? TakeCached()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached;
    }
}

/// <summary>Separate chaining + user-supplied <see cref="IEqualityComparer{TKey}" /> + no finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{TKey}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new map.
/// </remarks>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateComparerChained))]
public sealed class ComparerChainedHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, ChainedAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    [ThreadStatic]
    static ComparerChainedHashMap<TKey, TValue>? _threadCache;

    ComparerChainedHashMap(IEqualityComparer<TKey> comparer, int capacity)
        : base(capacity, Algorithm(comparer)) => Comparer = comparer;

    ComparerChainedHashMap(IEqualityComparer<TKey> comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count, Algorithm(comparer)) => Comparer = comparer;

    /// <summary>The comparer this map uses for key hashing and equality.</summary>
    public IEqualityComparer<TKey> Comparer { get; }

    /// <summary>
    /// Returns a map that uses <paramref name="comparer" /> for key equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new map. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerChainedHashMap<TKey, TValue> Create(IEqualityComparer<TKey>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<TKey>.Default;
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
        return new ComparerChainedHashMap<TKey, TValue>(comparer, capacity);
    }

    /// <summary>
    /// Creates a fresh map that uses <paramref name="comparer" />, pre-populated from
    /// <paramref name="rentedKeys" /> and <paramref name="rentedValues" /> and taking ownership
    /// of both arrays. Bypasses the per-thread cache.
    /// </summary>
    public static ComparerChainedHashMap<TKey, TValue> Create(
        IEqualityComparer<TKey>? comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(comparer ?? EqualityComparer<TKey>.Default, rentedKeys, rentedValues, count);

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerChainedHashMap<TKey, TValue> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerChainedHashMap<TKey, TValue> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, TKey[], TValue[], int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerChainedHashMap<TKey, TValue> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static ChainedAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer> Algorithm(IEqualityComparer<TKey> comparer)
        => new(new ComparerHasher<TKey>(comparer), default);
}

/// <summary>Separate chaining + user-supplied <see cref="IEqualityComparer{TKey}" /> + user-chosen finalizer.</summary>
/// <remarks>
/// Create with <see cref="Create(IEqualityComparer{TKey}, int)" />. The per-thread cache
/// hands a disposed instance back only to a <c>Create</c> call that passes the same comparer
/// instance (reference equality — e.g. <see cref="StringComparer.OrdinalIgnoreCase" />);
/// any other comparer releases the cached instance and allocates a new map.
/// </remarks>
[CollectionBuilder(typeof(PooledHashMapCollectionBuilder), nameof(PooledHashMapCollectionBuilder.CreateComparerChained))]
public sealed class ComparerChainedHashMap<TKey, TValue, TFinalizer>
    : PooledHashMap<TKey, TValue, ChainedAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, TFinalizer>>
    where TKey : notnull
    where TFinalizer : struct, IHashFinalizer
{
    [ThreadStatic]
    static ComparerChainedHashMap<TKey, TValue, TFinalizer>? _threadCache;

    ComparerChainedHashMap(IEqualityComparer<TKey> comparer, int capacity)
        : base(capacity, Algorithm(comparer)) => Comparer = comparer;

    ComparerChainedHashMap(IEqualityComparer<TKey> comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count, Algorithm(comparer)) => Comparer = comparer;

    /// <summary>The comparer this map uses for key hashing and equality.</summary>
    public IEqualityComparer<TKey> Comparer { get; }

    /// <summary>
    /// Returns a map that uses <paramref name="comparer" /> for key equality
    /// (<see langword="null" /> means <see cref="EqualityComparer{T}.Default" />). Reuses this
    /// type's per-thread cached instance when it was created with the same comparer instance;
    /// otherwise releases the cached instance and allocates a new map. The capacity hint applies
    /// only when allocating.
    /// </summary>
    public static ComparerChainedHashMap<TKey, TValue, TFinalizer> Create(IEqualityComparer<TKey>? comparer, int capacity = 0)
    {
        comparer ??= EqualityComparer<TKey>.Default;
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
        return new ComparerChainedHashMap<TKey, TValue, TFinalizer>(comparer, capacity);
    }

    /// <summary>
    /// Creates a fresh map that uses <paramref name="comparer" />, pre-populated from
    /// <paramref name="rentedKeys" /> and <paramref name="rentedValues" /> and taking ownership
    /// of both arrays. Bypasses the per-thread cache.
    /// </summary>
    public static ComparerChainedHashMap<TKey, TValue, TFinalizer> Create(
        IEqualityComparer<TKey>? comparer, TKey[] rentedKeys, TValue[] rentedValues, int count)
        => new(comparer ?? EqualityComparer<TKey>.Default, rentedKeys, rentedValues, count);

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerChainedHashMap<TKey, TValue, TFinalizer> Create() => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerChainedHashMap<TKey, TValue, TFinalizer> Create(int capacity) => throw AliasErrors.ComparerRequiredException();

    /// <summary>Not supported — this alias needs a comparer. Call <see cref="Create(IEqualityComparer{TKey}, TKey[], TValue[], int)" />.</summary>
    [Obsolete(AliasErrors.ComparerRequired, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new ComparerChainedHashMap<TKey, TValue, TFinalizer> Create(TKey[] rentedKeys, TValue[] rentedValues, int count)
        => throw AliasErrors.ComparerRequiredException();

    private protected override void ReturnToCache() => ReturnToSlot(ref _threadCache, this);

    static ChainedAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, TFinalizer> Algorithm(IEqualityComparer<TKey> comparer)
        => new(new ComparerHasher<TKey>(comparer), default);
}

static class AliasErrors
{
    internal const string ComparerRequired =
        "Comparer* aliases need a comparer: call Create(comparer, ...). Passing null uses EqualityComparer<T>.Default.";

    // A Comparer* collection expression without a comparer: `ComparerSwissHashMap<K,V> map = [];`.
    internal const string ComparerRequiredInCollectionExpression =
        "Comparer* aliases need a comparer: use [with(comparer), ...] or Create(comparer, ...). Passing null uses EqualityComparer<T>.Default.";

    // A comparer passed to an alias that always uses EqualityComparer<T>.Default — through
    // Create(comparer) or `[with(comparer), ...]`. Each message names the matching Comparer* alias.
    const string MapDefaultComparerOnly = " always uses EqualityComparer<TKey>.Default; for a custom IEqualityComparer<TKey> use ";
    const string SetDefaultComparerOnly = " always uses EqualityComparer<T>.Default; for a custom IEqualityComparer<T> use ";
    const string CreateWithComparer = ".Create(comparer).";
    const string CreateOrWithComparer = ".Create(comparer) or [with(comparer)].";

    internal const string PooledHashMapUsesDefaultComparer =
        "PooledHashMap<TKey,TValue>" + MapDefaultComparerOnly + "ComparerSwissHashMap<TKey,TValue>" + CreateOrWithComparer;
    internal const string SwissHashMapUsesDefaultComparer =
        "SwissHashMap<TKey,TValue>" + MapDefaultComparerOnly + "ComparerSwissHashMap<TKey,TValue>" + CreateWithComparer;
    internal const string SwissHashMapFinalizerUsesDefaultComparer =
        "SwissHashMap<TKey,TValue,TFinalizer>" + MapDefaultComparerOnly + "ComparerSwissHashMap<TKey,TValue,TFinalizer>" + CreateWithComparer;
    internal const string RobinHoodHashMapUsesDefaultComparer =
        "RobinHoodHashMap<TKey,TValue>" + MapDefaultComparerOnly + "ComparerRobinHoodHashMap<TKey,TValue>" + CreateWithComparer;
    internal const string RobinHoodHashMapFinalizerUsesDefaultComparer =
        "RobinHoodHashMap<TKey,TValue,TFinalizer>" + MapDefaultComparerOnly + "ComparerRobinHoodHashMap<TKey,TValue,TFinalizer>" + CreateWithComparer;
    internal const string LinearHashMapUsesDefaultComparer =
        "LinearHashMap<TKey,TValue>" + MapDefaultComparerOnly + "ComparerLinearHashMap<TKey,TValue>" + CreateWithComparer;
    internal const string LinearHashMapFinalizerUsesDefaultComparer =
        "LinearHashMap<TKey,TValue,TFinalizer>" + MapDefaultComparerOnly + "ComparerLinearHashMap<TKey,TValue,TFinalizer>" + CreateWithComparer;
    internal const string ChainedHashMapUsesDefaultComparer =
        "ChainedHashMap<TKey,TValue>" + MapDefaultComparerOnly + "ComparerChainedHashMap<TKey,TValue>" + CreateWithComparer;
    internal const string ChainedHashMapFinalizerUsesDefaultComparer =
        "ChainedHashMap<TKey,TValue,TFinalizer>" + MapDefaultComparerOnly + "ComparerChainedHashMap<TKey,TValue,TFinalizer>" + CreateWithComparer;

    internal const string PooledHashSetUsesDefaultComparer =
        "PooledHashSet<T>" + SetDefaultComparerOnly + "ComparerSwissHashSet<T>" + CreateOrWithComparer;
    internal const string SwissHashSetUsesDefaultComparer =
        "SwissHashSet<T>" + SetDefaultComparerOnly + "ComparerSwissHashSet<T>" + CreateWithComparer;
    internal const string SwissHashSetFinalizerUsesDefaultComparer =
        "SwissHashSet<T,TFinalizer>" + SetDefaultComparerOnly + "ComparerSwissHashSet<T,TFinalizer>" + CreateWithComparer;
    internal const string RobinHoodHashSetUsesDefaultComparer =
        "RobinHoodHashSet<T>" + SetDefaultComparerOnly + "ComparerRobinHoodHashSet<T>" + CreateWithComparer;
    internal const string RobinHoodHashSetFinalizerUsesDefaultComparer =
        "RobinHoodHashSet<T,TFinalizer>" + SetDefaultComparerOnly + "ComparerRobinHoodHashSet<T,TFinalizer>" + CreateWithComparer;
    internal const string LinearHashSetUsesDefaultComparer =
        "LinearHashSet<T>" + SetDefaultComparerOnly + "ComparerLinearHashSet<T>" + CreateWithComparer;
    internal const string LinearHashSetFinalizerUsesDefaultComparer =
        "LinearHashSet<T,TFinalizer>" + SetDefaultComparerOnly + "ComparerLinearHashSet<T,TFinalizer>" + CreateWithComparer;
    internal const string ChainedHashSetUsesDefaultComparer =
        "ChainedHashSet<T>" + SetDefaultComparerOnly + "ComparerChainedHashSet<T>" + CreateWithComparer;
    internal const string ChainedHashSetFinalizerUsesDefaultComparer =
        "ChainedHashSet<T,TFinalizer>" + SetDefaultComparerOnly + "ComparerChainedHashSet<T,TFinalizer>" + CreateWithComparer;

    internal static NotSupportedException ComparerRequiredException() => new(ComparerRequired);

    internal static NotSupportedException NotSupported(string message) => new(message);
}

static class AliasGuards
{
    // default(ComparerHasher<T>) has no comparer and would throw NullReferenceException on first use.
    internal static void EnsureStatelessHasher<T, THasher>()
        where T : notnull
        where THasher : struct, IHasher<T>
    {
        if (typeof(THasher) == typeof(ComparerHasher<T>))
        {
            throw new NotSupportedException(
                "Hasher aliases use default(THasher); ComparerHasher<T> needs a comparer. Use a Comparer* alias instead.");
        }
    }
}
