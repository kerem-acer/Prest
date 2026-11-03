using System.ComponentModel;

namespace Prest;

/// <summary>
/// Collection-expression builder for the <c>PooledHashMap</c> alias types; the compiler calls it for
/// <c>PooledHashMap&lt;string, int&gt; map = [new("a", 1), new("b", 2)];</c>.
/// Call the alias's own <c>Create</c> rather than these methods.
/// </summary>
/// <remarks>
/// <para>
/// Each method rents through the alias's <c>Create</c>, so the per-thread cache applies (and, on the
/// <c>Comparer*</c> aliases, the same-comparer rule). The leading parameters bind to C# 15
/// <c>with(...)</c> arguments: <c>[with(capacity), ...]</c> on every alias, and <c>[with(comparer), ...]</c>
/// or <c>[with(comparer, capacity), ...]</c> on the <c>Comparer*</c> aliases. The map is pre-sized
/// for the larger of the capacity and the element count; as with <c>Create(capacity)</c>, a cached
/// instance is reused at its current size.
/// </para>
/// <para>When a key appears more than once, the last entry wins.</para>
/// <para>
/// The <see cref="ObsoleteAttribute" /> overloads turn comparer misuse into a compile error that
/// names the right type: a <c>Comparer*</c> alias without a comparer, or a comparer on an alias that
/// always uses <see cref="EqualityComparer{T}.Default" />.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class PooledHashMapCollectionBuilder
{
    // ---- PooledHashMap ----

    /// <summary>Builds a <see cref="PooledHashMap{TKey,TValue}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static PooledHashMap<TKey, TValue> Create<TKey, TValue>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => Create<TKey, TValue>(0, items);

    /// <summary>Builds a <see cref="PooledHashMap{TKey,TValue}" /> pre-sized for <paramref name="capacity" /> entries: <c>[with(capacity), a, b]</c>.</summary>
    public static PooledHashMap<TKey, TValue> Create<TKey, TValue>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
    {
        var map = PooledHashMap<TKey, TValue>.Create(Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    // ---- SwissHashMap ----

    /// <summary>Builds a <see cref="SwissHashMap{TKey,TValue}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static SwissHashMap<TKey, TValue> CreateSwiss<TKey, TValue>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => CreateSwiss<TKey, TValue>(0, items);

    /// <summary>Builds a <see cref="SwissHashMap{TKey,TValue}" /> pre-sized for <paramref name="capacity" /> entries: <c>[with(capacity), a, b]</c>.</summary>
    public static SwissHashMap<TKey, TValue> CreateSwiss<TKey, TValue>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
    {
        var map = SwissHashMap<TKey, TValue>.Create(Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="SwissHashMap{TKey,TValue}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.SwissHashMapUsesDefaultComparer, error: true)]
    public static SwissHashMap<TKey, TValue> CreateSwiss<TKey, TValue>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.SwissHashMapUsesDefaultComparer);

    /// <summary>Not supported — <see cref="SwissHashMap{TKey,TValue}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.SwissHashMapUsesDefaultComparer, error: true)]
    public static SwissHashMap<TKey, TValue> CreateSwiss<TKey, TValue>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.SwissHashMapUsesDefaultComparer);

    /// <summary>Builds a <see cref="SwissHashMap{TKey,TValue,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static SwissHashMap<TKey, TValue, TFinalizer> CreateSwiss<TKey, TValue, TFinalizer>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateSwiss<TKey, TValue, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="SwissHashMap{TKey,TValue,TFinalizer}" /> pre-sized for <paramref name="capacity" /> entries: <c>[with(capacity), a, b]</c>.</summary>
    public static SwissHashMap<TKey, TValue, TFinalizer> CreateSwiss<TKey, TValue, TFinalizer>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var map = SwissHashMap<TKey, TValue, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="SwissHashMap{TKey,TValue,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.SwissHashMapFinalizerUsesDefaultComparer, error: true)]
    public static SwissHashMap<TKey, TValue, TFinalizer> CreateSwiss<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.SwissHashMapFinalizerUsesDefaultComparer);

    /// <summary>Not supported — <see cref="SwissHashMap{TKey,TValue,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.SwissHashMapFinalizerUsesDefaultComparer, error: true)]
    public static SwissHashMap<TKey, TValue, TFinalizer> CreateSwiss<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.SwissHashMapFinalizerUsesDefaultComparer);

    /// <summary>Builds a <see cref="SwissHashMap{TKey,TValue,THasher,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static SwissHashMap<TKey, TValue, THasher, TFinalizer> CreateSwiss<TKey, TValue, THasher, TFinalizer>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where THasher : struct, IHasher<TKey>
        where TFinalizer : struct, IHashFinalizer
        => CreateSwiss<TKey, TValue, THasher, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="SwissHashMap{TKey,TValue,THasher,TFinalizer}" /> pre-sized for <paramref name="capacity" /> entries: <c>[with(capacity), a, b]</c>.</summary>
    public static SwissHashMap<TKey, TValue, THasher, TFinalizer> CreateSwiss<TKey, TValue, THasher, TFinalizer>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where THasher : struct, IHasher<TKey>
        where TFinalizer : struct, IHashFinalizer
    {
        var map = SwissHashMap<TKey, TValue, THasher, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    // ---- ComparerSwissHashMap ----

    /// <summary>Builds a <see cref="ComparerSwissHashMap{TKey,TValue}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerSwissHashMap<TKey, TValue> CreateComparerSwiss<TKey, TValue>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => CreateComparerSwiss<TKey, TValue>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerSwissHashMap{TKey,TValue}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> entries: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerSwissHashMap<TKey, TValue> CreateComparerSwiss<TKey, TValue>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
    {
        var map = ComparerSwissHashMap<TKey, TValue>.Create(comparer, Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="ComparerSwissHashMap{TKey,TValue}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerSwissHashMap<TKey, TValue> CreateComparerSwiss<TKey, TValue>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerSwissHashMap{TKey,TValue}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerSwissHashMap<TKey, TValue> CreateComparerSwiss<TKey, TValue>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Builds a <see cref="ComparerSwissHashMap{TKey,TValue,TFinalizer}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerSwissHashMap<TKey, TValue, TFinalizer> CreateComparerSwiss<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateComparerSwiss<TKey, TValue, TFinalizer>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerSwissHashMap{TKey,TValue,TFinalizer}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> entries: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerSwissHashMap<TKey, TValue, TFinalizer> CreateComparerSwiss<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var map = ComparerSwissHashMap<TKey, TValue, TFinalizer>.Create(comparer, Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="ComparerSwissHashMap{TKey,TValue,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerSwissHashMap<TKey, TValue, TFinalizer> CreateComparerSwiss<TKey, TValue, TFinalizer>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerSwissHashMap{TKey,TValue,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerSwissHashMap<TKey, TValue, TFinalizer> CreateComparerSwiss<TKey, TValue, TFinalizer>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    // ---- RobinHoodHashMap ----

    /// <summary>Builds a <see cref="RobinHoodHashMap{TKey,TValue}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static RobinHoodHashMap<TKey, TValue> CreateRobinHood<TKey, TValue>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => CreateRobinHood<TKey, TValue>(0, items);

    /// <summary>Builds a <see cref="RobinHoodHashMap{TKey,TValue}" /> pre-sized for <paramref name="capacity" /> entries: <c>[with(capacity), a, b]</c>.</summary>
    public static RobinHoodHashMap<TKey, TValue> CreateRobinHood<TKey, TValue>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
    {
        var map = RobinHoodHashMap<TKey, TValue>.Create(Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="RobinHoodHashMap{TKey,TValue}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.RobinHoodHashMapUsesDefaultComparer, error: true)]
    public static RobinHoodHashMap<TKey, TValue> CreateRobinHood<TKey, TValue>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.RobinHoodHashMapUsesDefaultComparer);

    /// <summary>Not supported — <see cref="RobinHoodHashMap{TKey,TValue}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.RobinHoodHashMapUsesDefaultComparer, error: true)]
    public static RobinHoodHashMap<TKey, TValue> CreateRobinHood<TKey, TValue>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.RobinHoodHashMapUsesDefaultComparer);

    /// <summary>Builds a <see cref="RobinHoodHashMap{TKey,TValue,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static RobinHoodHashMap<TKey, TValue, TFinalizer> CreateRobinHood<TKey, TValue, TFinalizer>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateRobinHood<TKey, TValue, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="RobinHoodHashMap{TKey,TValue,TFinalizer}" /> pre-sized for <paramref name="capacity" /> entries: <c>[with(capacity), a, b]</c>.</summary>
    public static RobinHoodHashMap<TKey, TValue, TFinalizer> CreateRobinHood<TKey, TValue, TFinalizer>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var map = RobinHoodHashMap<TKey, TValue, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="RobinHoodHashMap{TKey,TValue,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.RobinHoodHashMapFinalizerUsesDefaultComparer, error: true)]
    public static RobinHoodHashMap<TKey, TValue, TFinalizer> CreateRobinHood<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.RobinHoodHashMapFinalizerUsesDefaultComparer);

    /// <summary>Not supported — <see cref="RobinHoodHashMap{TKey,TValue,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.RobinHoodHashMapFinalizerUsesDefaultComparer, error: true)]
    public static RobinHoodHashMap<TKey, TValue, TFinalizer> CreateRobinHood<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.RobinHoodHashMapFinalizerUsesDefaultComparer);

    /// <summary>Builds a <see cref="RobinHoodHashMap{TKey,TValue,THasher,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static RobinHoodHashMap<TKey, TValue, THasher, TFinalizer> CreateRobinHood<TKey, TValue, THasher, TFinalizer>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where THasher : struct, IHasher<TKey>
        where TFinalizer : struct, IHashFinalizer
        => CreateRobinHood<TKey, TValue, THasher, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="RobinHoodHashMap{TKey,TValue,THasher,TFinalizer}" /> pre-sized for <paramref name="capacity" /> entries: <c>[with(capacity), a, b]</c>.</summary>
    public static RobinHoodHashMap<TKey, TValue, THasher, TFinalizer> CreateRobinHood<TKey, TValue, THasher, TFinalizer>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where THasher : struct, IHasher<TKey>
        where TFinalizer : struct, IHashFinalizer
    {
        var map = RobinHoodHashMap<TKey, TValue, THasher, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    // ---- ComparerRobinHoodHashMap ----

    /// <summary>Builds a <see cref="ComparerRobinHoodHashMap{TKey,TValue}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerRobinHoodHashMap<TKey, TValue> CreateComparerRobinHood<TKey, TValue>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => CreateComparerRobinHood<TKey, TValue>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerRobinHoodHashMap{TKey,TValue}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> entries: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerRobinHoodHashMap<TKey, TValue> CreateComparerRobinHood<TKey, TValue>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
    {
        var map = ComparerRobinHoodHashMap<TKey, TValue>.Create(comparer, Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="ComparerRobinHoodHashMap{TKey,TValue}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerRobinHoodHashMap<TKey, TValue> CreateComparerRobinHood<TKey, TValue>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerRobinHoodHashMap{TKey,TValue}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerRobinHoodHashMap<TKey, TValue> CreateComparerRobinHood<TKey, TValue>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Builds a <see cref="ComparerRobinHoodHashMap{TKey,TValue,TFinalizer}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerRobinHoodHashMap<TKey, TValue, TFinalizer> CreateComparerRobinHood<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateComparerRobinHood<TKey, TValue, TFinalizer>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerRobinHoodHashMap{TKey,TValue,TFinalizer}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> entries: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerRobinHoodHashMap<TKey, TValue, TFinalizer> CreateComparerRobinHood<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var map = ComparerRobinHoodHashMap<TKey, TValue, TFinalizer>.Create(comparer, Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="ComparerRobinHoodHashMap{TKey,TValue,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerRobinHoodHashMap<TKey, TValue, TFinalizer> CreateComparerRobinHood<TKey, TValue, TFinalizer>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerRobinHoodHashMap{TKey,TValue,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerRobinHoodHashMap<TKey, TValue, TFinalizer> CreateComparerRobinHood<TKey, TValue, TFinalizer>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    // ---- LinearHashMap ----

    /// <summary>Builds a <see cref="LinearHashMap{TKey,TValue}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static LinearHashMap<TKey, TValue> CreateLinear<TKey, TValue>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => CreateLinear<TKey, TValue>(0, items);

    /// <summary>Builds a <see cref="LinearHashMap{TKey,TValue}" /> pre-sized for <paramref name="capacity" /> entries: <c>[with(capacity), a, b]</c>.</summary>
    public static LinearHashMap<TKey, TValue> CreateLinear<TKey, TValue>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
    {
        var map = LinearHashMap<TKey, TValue>.Create(Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="LinearHashMap{TKey,TValue}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.LinearHashMapUsesDefaultComparer, error: true)]
    public static LinearHashMap<TKey, TValue> CreateLinear<TKey, TValue>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.LinearHashMapUsesDefaultComparer);

    /// <summary>Not supported — <see cref="LinearHashMap{TKey,TValue}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.LinearHashMapUsesDefaultComparer, error: true)]
    public static LinearHashMap<TKey, TValue> CreateLinear<TKey, TValue>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.LinearHashMapUsesDefaultComparer);

    /// <summary>Builds a <see cref="LinearHashMap{TKey,TValue,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static LinearHashMap<TKey, TValue, TFinalizer> CreateLinear<TKey, TValue, TFinalizer>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateLinear<TKey, TValue, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="LinearHashMap{TKey,TValue,TFinalizer}" /> pre-sized for <paramref name="capacity" /> entries: <c>[with(capacity), a, b]</c>.</summary>
    public static LinearHashMap<TKey, TValue, TFinalizer> CreateLinear<TKey, TValue, TFinalizer>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var map = LinearHashMap<TKey, TValue, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="LinearHashMap{TKey,TValue,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.LinearHashMapFinalizerUsesDefaultComparer, error: true)]
    public static LinearHashMap<TKey, TValue, TFinalizer> CreateLinear<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.LinearHashMapFinalizerUsesDefaultComparer);

    /// <summary>Not supported — <see cref="LinearHashMap{TKey,TValue,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.LinearHashMapFinalizerUsesDefaultComparer, error: true)]
    public static LinearHashMap<TKey, TValue, TFinalizer> CreateLinear<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.LinearHashMapFinalizerUsesDefaultComparer);

    /// <summary>Builds a <see cref="LinearHashMap{TKey,TValue,THasher,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static LinearHashMap<TKey, TValue, THasher, TFinalizer> CreateLinear<TKey, TValue, THasher, TFinalizer>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where THasher : struct, IHasher<TKey>
        where TFinalizer : struct, IHashFinalizer
        => CreateLinear<TKey, TValue, THasher, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="LinearHashMap{TKey,TValue,THasher,TFinalizer}" /> pre-sized for <paramref name="capacity" /> entries: <c>[with(capacity), a, b]</c>.</summary>
    public static LinearHashMap<TKey, TValue, THasher, TFinalizer> CreateLinear<TKey, TValue, THasher, TFinalizer>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where THasher : struct, IHasher<TKey>
        where TFinalizer : struct, IHashFinalizer
    {
        var map = LinearHashMap<TKey, TValue, THasher, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    // ---- ComparerLinearHashMap ----

    /// <summary>Builds a <see cref="ComparerLinearHashMap{TKey,TValue}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerLinearHashMap<TKey, TValue> CreateComparerLinear<TKey, TValue>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => CreateComparerLinear<TKey, TValue>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerLinearHashMap{TKey,TValue}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> entries: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerLinearHashMap<TKey, TValue> CreateComparerLinear<TKey, TValue>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
    {
        var map = ComparerLinearHashMap<TKey, TValue>.Create(comparer, Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="ComparerLinearHashMap{TKey,TValue}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerLinearHashMap<TKey, TValue> CreateComparerLinear<TKey, TValue>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerLinearHashMap{TKey,TValue}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerLinearHashMap<TKey, TValue> CreateComparerLinear<TKey, TValue>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Builds a <see cref="ComparerLinearHashMap{TKey,TValue,TFinalizer}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerLinearHashMap<TKey, TValue, TFinalizer> CreateComparerLinear<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateComparerLinear<TKey, TValue, TFinalizer>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerLinearHashMap{TKey,TValue,TFinalizer}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> entries: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerLinearHashMap<TKey, TValue, TFinalizer> CreateComparerLinear<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var map = ComparerLinearHashMap<TKey, TValue, TFinalizer>.Create(comparer, Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="ComparerLinearHashMap{TKey,TValue,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerLinearHashMap<TKey, TValue, TFinalizer> CreateComparerLinear<TKey, TValue, TFinalizer>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerLinearHashMap{TKey,TValue,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerLinearHashMap<TKey, TValue, TFinalizer> CreateComparerLinear<TKey, TValue, TFinalizer>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    // ---- ChainedHashMap ----

    /// <summary>Builds a <see cref="ChainedHashMap{TKey,TValue}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static ChainedHashMap<TKey, TValue> CreateChained<TKey, TValue>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => CreateChained<TKey, TValue>(0, items);

    /// <summary>Builds a <see cref="ChainedHashMap{TKey,TValue}" /> pre-sized for <paramref name="capacity" /> entries: <c>[with(capacity), a, b]</c>.</summary>
    public static ChainedHashMap<TKey, TValue> CreateChained<TKey, TValue>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
    {
        var map = ChainedHashMap<TKey, TValue>.Create(Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="ChainedHashMap{TKey,TValue}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.ChainedHashMapUsesDefaultComparer, error: true)]
    public static ChainedHashMap<TKey, TValue> CreateChained<TKey, TValue>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ChainedHashMapUsesDefaultComparer);

    /// <summary>Not supported — <see cref="ChainedHashMap{TKey,TValue}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.ChainedHashMapUsesDefaultComparer, error: true)]
    public static ChainedHashMap<TKey, TValue> CreateChained<TKey, TValue>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ChainedHashMapUsesDefaultComparer);

    /// <summary>Builds a <see cref="ChainedHashMap{TKey,TValue,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static ChainedHashMap<TKey, TValue, TFinalizer> CreateChained<TKey, TValue, TFinalizer>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateChained<TKey, TValue, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="ChainedHashMap{TKey,TValue,TFinalizer}" /> pre-sized for <paramref name="capacity" /> entries: <c>[with(capacity), a, b]</c>.</summary>
    public static ChainedHashMap<TKey, TValue, TFinalizer> CreateChained<TKey, TValue, TFinalizer>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var map = ChainedHashMap<TKey, TValue, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="ChainedHashMap{TKey,TValue,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.ChainedHashMapFinalizerUsesDefaultComparer, error: true)]
    public static ChainedHashMap<TKey, TValue, TFinalizer> CreateChained<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ChainedHashMapFinalizerUsesDefaultComparer);

    /// <summary>Not supported — <see cref="ChainedHashMap{TKey,TValue,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.ChainedHashMapFinalizerUsesDefaultComparer, error: true)]
    public static ChainedHashMap<TKey, TValue, TFinalizer> CreateChained<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ChainedHashMapFinalizerUsesDefaultComparer);

    /// <summary>Builds a <see cref="ChainedHashMap{TKey,TValue,THasher,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static ChainedHashMap<TKey, TValue, THasher, TFinalizer> CreateChained<TKey, TValue, THasher, TFinalizer>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where THasher : struct, IHasher<TKey>
        where TFinalizer : struct, IHashFinalizer
        => CreateChained<TKey, TValue, THasher, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="ChainedHashMap{TKey,TValue,THasher,TFinalizer}" /> pre-sized for <paramref name="capacity" /> entries: <c>[with(capacity), a, b]</c>.</summary>
    public static ChainedHashMap<TKey, TValue, THasher, TFinalizer> CreateChained<TKey, TValue, THasher, TFinalizer>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where THasher : struct, IHasher<TKey>
        where TFinalizer : struct, IHashFinalizer
    {
        var map = ChainedHashMap<TKey, TValue, THasher, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    // ---- ComparerChainedHashMap ----

    /// <summary>Builds a <see cref="ComparerChainedHashMap{TKey,TValue}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerChainedHashMap<TKey, TValue> CreateComparerChained<TKey, TValue>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => CreateComparerChained<TKey, TValue>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerChainedHashMap{TKey,TValue}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> entries: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerChainedHashMap<TKey, TValue> CreateComparerChained<TKey, TValue>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
    {
        var map = ComparerChainedHashMap<TKey, TValue>.Create(comparer, Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="ComparerChainedHashMap{TKey,TValue}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerChainedHashMap<TKey, TValue> CreateComparerChained<TKey, TValue>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerChainedHashMap{TKey,TValue}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerChainedHashMap<TKey, TValue> CreateComparerChained<TKey, TValue>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Builds a <see cref="ComparerChainedHashMap{TKey,TValue,TFinalizer}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerChainedHashMap<TKey, TValue, TFinalizer> CreateComparerChained<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateComparerChained<TKey, TValue, TFinalizer>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerChainedHashMap{TKey,TValue,TFinalizer}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> entries: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerChainedHashMap<TKey, TValue, TFinalizer> CreateComparerChained<TKey, TValue, TFinalizer>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var map = ComparerChainedHashMap<TKey, TValue, TFinalizer>.Create(comparer, Math.Max(capacity, items.Length));
        AddOrReplace(map, items);
        return map;
    }

    /// <summary>Not supported — <see cref="ComparerChainedHashMap{TKey,TValue,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerChainedHashMap<TKey, TValue, TFinalizer> CreateComparerChained<TKey, TValue, TFinalizer>(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerChainedHashMap{TKey,TValue,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerChainedHashMap<TKey, TValue, TFinalizer> CreateComparerChained<TKey, TValue, TFinalizer>(int capacity, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    // There is no indexer setter, so a repeated key is replaced with Remove + Add.
    static void AddOrReplace<TKey, TValue, TAlgo>(PooledHashMap<TKey, TValue, TAlgo> map, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
    {
        foreach (var item in items)
        {
            if (!map.TryAdd(item.Key, item.Value))
            {
                map.Remove(item.Key);
                map.Add(item.Key, item.Value);
            }
        }
    }
}
