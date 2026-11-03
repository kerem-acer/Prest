using System.ComponentModel;

namespace Prest;

/// <summary>
/// Collection-expression builder for the <c>PooledHashSet</c> alias types; the compiler calls it for
/// <c>PooledHashSet&lt;string&gt; set = ["a", "b"];</c>.
/// Call the alias's own <c>Create</c> rather than these methods.
/// </summary>
/// <remarks>
/// <para>
/// Each method rents through the alias's <c>Create</c>, so the per-thread cache applies (and, on the
/// <c>Comparer*</c> aliases, the same-comparer rule). The leading parameters bind to C# 15
/// <c>with(...)</c> arguments: <c>[with(capacity), ...]</c> on every alias, and <c>[with(comparer), ...]</c>
/// or <c>[with(comparer, capacity), ...]</c> on the <c>Comparer*</c> aliases. The set is pre-sized
/// for the larger of the capacity and the element count; as with <c>Create(capacity)</c>, a cached
/// instance is reused at its current size.
/// </para>
/// <para>Items already in the set are ignored, as with <c>Add</c>.</para>
/// <para>
/// The <see cref="ObsoleteAttribute" /> overloads turn comparer misuse into a compile error that
/// names the right type: a <c>Comparer*</c> alias without a comparer, or a comparer on an alias that
/// always uses <see cref="EqualityComparer{T}.Default" />.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class PooledHashSetCollectionBuilder
{
    // ---- PooledHashSet ----

    /// <summary>Builds a <see cref="PooledHashSet{T}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static PooledHashSet<T> Create<T>(ReadOnlySpan<T> items)
        where T : notnull
        => Create<T>(0, items);

    /// <summary>Builds a <see cref="PooledHashSet{T}" /> pre-sized for <paramref name="capacity" /> items: <c>[with(capacity), a, b]</c>.</summary>
    public static PooledHashSet<T> Create<T>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
    {
        var set = PooledHashSet<T>.Create(Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Builds a <see cref="PooledHashSet{T}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static PooledHashSet<T> Create<T>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        => Create(comparer, 0, items);

    /// <summary>Builds a <see cref="PooledHashSet{T}" /> that uses <paramref name="comparer" />, pre-sized for <paramref name="capacity" /> items: <c>[with(comparer, capacity), a, b]</c>.</summary>
    public static PooledHashSet<T> Create<T>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
    {
        var set = PooledHashSet<T>.Create(comparer, Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    // ---- SwissHashSet ----

    /// <summary>Builds a <see cref="SwissHashSet{T}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static SwissHashSet<T> CreateSwiss<T>(ReadOnlySpan<T> items)
        where T : notnull
        => CreateSwiss<T>(0, items);

    /// <summary>Builds a <see cref="SwissHashSet{T}" /> pre-sized for <paramref name="capacity" /> items: <c>[with(capacity), a, b]</c>.</summary>
    public static SwissHashSet<T> CreateSwiss<T>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
    {
        var set = SwissHashSet<T>.Create(Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="SwissHashSet{T}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.SwissHashSetUsesDefaultComparer, error: true)]
    public static SwissHashSet<T> CreateSwiss<T>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.SwissHashSetUsesDefaultComparer);

    /// <summary>Not supported — <see cref="SwissHashSet{T}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.SwissHashSetUsesDefaultComparer, error: true)]
    public static SwissHashSet<T> CreateSwiss<T>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.SwissHashSetUsesDefaultComparer);

    /// <summary>Builds a <see cref="SwissHashSet{T,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static SwissHashSet<T, TFinalizer> CreateSwiss<T, TFinalizer>(ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateSwiss<T, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="SwissHashSet{T,TFinalizer}" /> pre-sized for <paramref name="capacity" /> items: <c>[with(capacity), a, b]</c>.</summary>
    public static SwissHashSet<T, TFinalizer> CreateSwiss<T, TFinalizer>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var set = SwissHashSet<T, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="SwissHashSet{T,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.SwissHashSetFinalizerUsesDefaultComparer, error: true)]
    public static SwissHashSet<T, TFinalizer> CreateSwiss<T, TFinalizer>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.SwissHashSetFinalizerUsesDefaultComparer);

    /// <summary>Not supported — <see cref="SwissHashSet{T,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.SwissHashSetFinalizerUsesDefaultComparer, error: true)]
    public static SwissHashSet<T, TFinalizer> CreateSwiss<T, TFinalizer>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.SwissHashSetFinalizerUsesDefaultComparer);

    /// <summary>Builds a <see cref="SwissHashSet{T,THasher,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static SwissHashSet<T, THasher, TFinalizer> CreateSwiss<T, THasher, TFinalizer>(ReadOnlySpan<T> items)
        where T : notnull
        where THasher : struct, IHasher<T>
        where TFinalizer : struct, IHashFinalizer
        => CreateSwiss<T, THasher, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="SwissHashSet{T,THasher,TFinalizer}" /> pre-sized for <paramref name="capacity" /> items: <c>[with(capacity), a, b]</c>.</summary>
    public static SwissHashSet<T, THasher, TFinalizer> CreateSwiss<T, THasher, TFinalizer>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where THasher : struct, IHasher<T>
        where TFinalizer : struct, IHashFinalizer
    {
        var set = SwissHashSet<T, THasher, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    // ---- ComparerSwissHashSet ----

    /// <summary>Builds a <see cref="ComparerSwissHashSet{T}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerSwissHashSet<T> CreateComparerSwiss<T>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        => CreateComparerSwiss<T>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerSwissHashSet{T}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> items: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerSwissHashSet<T> CreateComparerSwiss<T>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
    {
        var set = ComparerSwissHashSet<T>.Create(comparer, Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="ComparerSwissHashSet{T}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerSwissHashSet<T> CreateComparerSwiss<T>(ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerSwissHashSet{T}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerSwissHashSet<T> CreateComparerSwiss<T>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Builds a <see cref="ComparerSwissHashSet{T,TFinalizer}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerSwissHashSet<T, TFinalizer> CreateComparerSwiss<T, TFinalizer>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateComparerSwiss<T, TFinalizer>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerSwissHashSet{T,TFinalizer}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> items: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerSwissHashSet<T, TFinalizer> CreateComparerSwiss<T, TFinalizer>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var set = ComparerSwissHashSet<T, TFinalizer>.Create(comparer, Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="ComparerSwissHashSet{T,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerSwissHashSet<T, TFinalizer> CreateComparerSwiss<T, TFinalizer>(ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerSwissHashSet{T,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerSwissHashSet<T, TFinalizer> CreateComparerSwiss<T, TFinalizer>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    // ---- RobinHoodHashSet ----

    /// <summary>Builds a <see cref="RobinHoodHashSet{T}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static RobinHoodHashSet<T> CreateRobinHood<T>(ReadOnlySpan<T> items)
        where T : notnull
        => CreateRobinHood<T>(0, items);

    /// <summary>Builds a <see cref="RobinHoodHashSet{T}" /> pre-sized for <paramref name="capacity" /> items: <c>[with(capacity), a, b]</c>.</summary>
    public static RobinHoodHashSet<T> CreateRobinHood<T>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
    {
        var set = RobinHoodHashSet<T>.Create(Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="RobinHoodHashSet{T}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.RobinHoodHashSetUsesDefaultComparer, error: true)]
    public static RobinHoodHashSet<T> CreateRobinHood<T>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.RobinHoodHashSetUsesDefaultComparer);

    /// <summary>Not supported — <see cref="RobinHoodHashSet{T}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.RobinHoodHashSetUsesDefaultComparer, error: true)]
    public static RobinHoodHashSet<T> CreateRobinHood<T>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.RobinHoodHashSetUsesDefaultComparer);

    /// <summary>Builds a <see cref="RobinHoodHashSet{T,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static RobinHoodHashSet<T, TFinalizer> CreateRobinHood<T, TFinalizer>(ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateRobinHood<T, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="RobinHoodHashSet{T,TFinalizer}" /> pre-sized for <paramref name="capacity" /> items: <c>[with(capacity), a, b]</c>.</summary>
    public static RobinHoodHashSet<T, TFinalizer> CreateRobinHood<T, TFinalizer>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var set = RobinHoodHashSet<T, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="RobinHoodHashSet{T,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.RobinHoodHashSetFinalizerUsesDefaultComparer, error: true)]
    public static RobinHoodHashSet<T, TFinalizer> CreateRobinHood<T, TFinalizer>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.RobinHoodHashSetFinalizerUsesDefaultComparer);

    /// <summary>Not supported — <see cref="RobinHoodHashSet{T,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.RobinHoodHashSetFinalizerUsesDefaultComparer, error: true)]
    public static RobinHoodHashSet<T, TFinalizer> CreateRobinHood<T, TFinalizer>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.RobinHoodHashSetFinalizerUsesDefaultComparer);

    /// <summary>Builds a <see cref="RobinHoodHashSet{T,THasher,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static RobinHoodHashSet<T, THasher, TFinalizer> CreateRobinHood<T, THasher, TFinalizer>(ReadOnlySpan<T> items)
        where T : notnull
        where THasher : struct, IHasher<T>
        where TFinalizer : struct, IHashFinalizer
        => CreateRobinHood<T, THasher, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="RobinHoodHashSet{T,THasher,TFinalizer}" /> pre-sized for <paramref name="capacity" /> items: <c>[with(capacity), a, b]</c>.</summary>
    public static RobinHoodHashSet<T, THasher, TFinalizer> CreateRobinHood<T, THasher, TFinalizer>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where THasher : struct, IHasher<T>
        where TFinalizer : struct, IHashFinalizer
    {
        var set = RobinHoodHashSet<T, THasher, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    // ---- ComparerRobinHoodHashSet ----

    /// <summary>Builds a <see cref="ComparerRobinHoodHashSet{T}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerRobinHoodHashSet<T> CreateComparerRobinHood<T>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        => CreateComparerRobinHood<T>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerRobinHoodHashSet{T}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> items: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerRobinHoodHashSet<T> CreateComparerRobinHood<T>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
    {
        var set = ComparerRobinHoodHashSet<T>.Create(comparer, Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="ComparerRobinHoodHashSet{T}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerRobinHoodHashSet<T> CreateComparerRobinHood<T>(ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerRobinHoodHashSet{T}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerRobinHoodHashSet<T> CreateComparerRobinHood<T>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Builds a <see cref="ComparerRobinHoodHashSet{T,TFinalizer}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerRobinHoodHashSet<T, TFinalizer> CreateComparerRobinHood<T, TFinalizer>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateComparerRobinHood<T, TFinalizer>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerRobinHoodHashSet{T,TFinalizer}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> items: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerRobinHoodHashSet<T, TFinalizer> CreateComparerRobinHood<T, TFinalizer>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var set = ComparerRobinHoodHashSet<T, TFinalizer>.Create(comparer, Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="ComparerRobinHoodHashSet{T,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerRobinHoodHashSet<T, TFinalizer> CreateComparerRobinHood<T, TFinalizer>(ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerRobinHoodHashSet{T,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerRobinHoodHashSet<T, TFinalizer> CreateComparerRobinHood<T, TFinalizer>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    // ---- LinearHashSet ----

    /// <summary>Builds a <see cref="LinearHashSet{T}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static LinearHashSet<T> CreateLinear<T>(ReadOnlySpan<T> items)
        where T : notnull
        => CreateLinear<T>(0, items);

    /// <summary>Builds a <see cref="LinearHashSet{T}" /> pre-sized for <paramref name="capacity" /> items: <c>[with(capacity), a, b]</c>.</summary>
    public static LinearHashSet<T> CreateLinear<T>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
    {
        var set = LinearHashSet<T>.Create(Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="LinearHashSet{T}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.LinearHashSetUsesDefaultComparer, error: true)]
    public static LinearHashSet<T> CreateLinear<T>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.LinearHashSetUsesDefaultComparer);

    /// <summary>Not supported — <see cref="LinearHashSet{T}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.LinearHashSetUsesDefaultComparer, error: true)]
    public static LinearHashSet<T> CreateLinear<T>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.LinearHashSetUsesDefaultComparer);

    /// <summary>Builds a <see cref="LinearHashSet{T,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static LinearHashSet<T, TFinalizer> CreateLinear<T, TFinalizer>(ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateLinear<T, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="LinearHashSet{T,TFinalizer}" /> pre-sized for <paramref name="capacity" /> items: <c>[with(capacity), a, b]</c>.</summary>
    public static LinearHashSet<T, TFinalizer> CreateLinear<T, TFinalizer>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var set = LinearHashSet<T, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="LinearHashSet{T,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.LinearHashSetFinalizerUsesDefaultComparer, error: true)]
    public static LinearHashSet<T, TFinalizer> CreateLinear<T, TFinalizer>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.LinearHashSetFinalizerUsesDefaultComparer);

    /// <summary>Not supported — <see cref="LinearHashSet{T,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.LinearHashSetFinalizerUsesDefaultComparer, error: true)]
    public static LinearHashSet<T, TFinalizer> CreateLinear<T, TFinalizer>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.LinearHashSetFinalizerUsesDefaultComparer);

    /// <summary>Builds a <see cref="LinearHashSet{T,THasher,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static LinearHashSet<T, THasher, TFinalizer> CreateLinear<T, THasher, TFinalizer>(ReadOnlySpan<T> items)
        where T : notnull
        where THasher : struct, IHasher<T>
        where TFinalizer : struct, IHashFinalizer
        => CreateLinear<T, THasher, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="LinearHashSet{T,THasher,TFinalizer}" /> pre-sized for <paramref name="capacity" /> items: <c>[with(capacity), a, b]</c>.</summary>
    public static LinearHashSet<T, THasher, TFinalizer> CreateLinear<T, THasher, TFinalizer>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where THasher : struct, IHasher<T>
        where TFinalizer : struct, IHashFinalizer
    {
        var set = LinearHashSet<T, THasher, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    // ---- ComparerLinearHashSet ----

    /// <summary>Builds a <see cref="ComparerLinearHashSet{T}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerLinearHashSet<T> CreateComparerLinear<T>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        => CreateComparerLinear<T>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerLinearHashSet{T}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> items: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerLinearHashSet<T> CreateComparerLinear<T>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
    {
        var set = ComparerLinearHashSet<T>.Create(comparer, Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="ComparerLinearHashSet{T}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerLinearHashSet<T> CreateComparerLinear<T>(ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerLinearHashSet{T}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerLinearHashSet<T> CreateComparerLinear<T>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Builds a <see cref="ComparerLinearHashSet{T,TFinalizer}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerLinearHashSet<T, TFinalizer> CreateComparerLinear<T, TFinalizer>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateComparerLinear<T, TFinalizer>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerLinearHashSet{T,TFinalizer}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> items: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerLinearHashSet<T, TFinalizer> CreateComparerLinear<T, TFinalizer>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var set = ComparerLinearHashSet<T, TFinalizer>.Create(comparer, Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="ComparerLinearHashSet{T,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerLinearHashSet<T, TFinalizer> CreateComparerLinear<T, TFinalizer>(ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerLinearHashSet{T,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerLinearHashSet<T, TFinalizer> CreateComparerLinear<T, TFinalizer>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    // ---- ChainedHashSet ----

    /// <summary>Builds a <see cref="ChainedHashSet{T}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static ChainedHashSet<T> CreateChained<T>(ReadOnlySpan<T> items)
        where T : notnull
        => CreateChained<T>(0, items);

    /// <summary>Builds a <see cref="ChainedHashSet{T}" /> pre-sized for <paramref name="capacity" /> items: <c>[with(capacity), a, b]</c>.</summary>
    public static ChainedHashSet<T> CreateChained<T>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
    {
        var set = ChainedHashSet<T>.Create(Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="ChainedHashSet{T}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.ChainedHashSetUsesDefaultComparer, error: true)]
    public static ChainedHashSet<T> CreateChained<T>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ChainedHashSetUsesDefaultComparer);

    /// <summary>Not supported — <see cref="ChainedHashSet{T}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.ChainedHashSetUsesDefaultComparer, error: true)]
    public static ChainedHashSet<T> CreateChained<T>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ChainedHashSetUsesDefaultComparer);

    /// <summary>Builds a <see cref="ChainedHashSet{T,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static ChainedHashSet<T, TFinalizer> CreateChained<T, TFinalizer>(ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateChained<T, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="ChainedHashSet{T,TFinalizer}" /> pre-sized for <paramref name="capacity" /> items: <c>[with(capacity), a, b]</c>.</summary>
    public static ChainedHashSet<T, TFinalizer> CreateChained<T, TFinalizer>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var set = ChainedHashSet<T, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="ChainedHashSet{T,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.ChainedHashSetFinalizerUsesDefaultComparer, error: true)]
    public static ChainedHashSet<T, TFinalizer> CreateChained<T, TFinalizer>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ChainedHashSetFinalizerUsesDefaultComparer);

    /// <summary>Not supported — <see cref="ChainedHashSet{T,TFinalizer}" /> always uses the default comparer.</summary>
    [Obsolete(AliasErrors.ChainedHashSetFinalizerUsesDefaultComparer, error: true)]
    public static ChainedHashSet<T, TFinalizer> CreateChained<T, TFinalizer>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ChainedHashSetFinalizerUsesDefaultComparer);

    /// <summary>Builds a <see cref="ChainedHashSet{T,THasher,TFinalizer}" /> from <paramref name="items" />: <c>[a, b]</c>.</summary>
    public static ChainedHashSet<T, THasher, TFinalizer> CreateChained<T, THasher, TFinalizer>(ReadOnlySpan<T> items)
        where T : notnull
        where THasher : struct, IHasher<T>
        where TFinalizer : struct, IHashFinalizer
        => CreateChained<T, THasher, TFinalizer>(0, items);

    /// <summary>Builds a <see cref="ChainedHashSet{T,THasher,TFinalizer}" /> pre-sized for <paramref name="capacity" /> items: <c>[with(capacity), a, b]</c>.</summary>
    public static ChainedHashSet<T, THasher, TFinalizer> CreateChained<T, THasher, TFinalizer>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where THasher : struct, IHasher<T>
        where TFinalizer : struct, IHashFinalizer
    {
        var set = ChainedHashSet<T, THasher, TFinalizer>.Create(Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    // ---- ComparerChainedHashSet ----

    /// <summary>Builds a <see cref="ComparerChainedHashSet{T}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerChainedHashSet<T> CreateComparerChained<T>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        => CreateComparerChained<T>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerChainedHashSet{T}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> items: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerChainedHashSet<T> CreateComparerChained<T>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
    {
        var set = ComparerChainedHashSet<T>.Create(comparer, Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="ComparerChainedHashSet{T}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerChainedHashSet<T> CreateComparerChained<T>(ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerChainedHashSet{T}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerChainedHashSet<T> CreateComparerChained<T>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Builds a <see cref="ComparerChainedHashSet{T,TFinalizer}" /> that uses <paramref name="comparer" />: <c>[with(comparer), a, b]</c>.</summary>
    public static ComparerChainedHashSet<T, TFinalizer> CreateComparerChained<T, TFinalizer>(IEqualityComparer<T>? comparer, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => CreateComparerChained<T, TFinalizer>(comparer, 0, items);

    /// <summary>
    /// Builds a <see cref="ComparerChainedHashSet{T,TFinalizer}" /> that uses <paramref name="comparer" />, pre-sized for
    /// <paramref name="capacity" /> items: <c>[with(comparer, capacity), a, b]</c>.
    /// </summary>
    public static ComparerChainedHashSet<T, TFinalizer> CreateComparerChained<T, TFinalizer>(IEqualityComparer<T>? comparer, int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
    {
        var set = ComparerChainedHashSet<T, TFinalizer>.Create(comparer, Math.Max(capacity, items.Length));
        AddAll(set, items);
        return set;
    }

    /// <summary>Not supported — <see cref="ComparerChainedHashSet{T,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerChainedHashSet<T, TFinalizer> CreateComparerChained<T, TFinalizer>(ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    /// <summary>Not supported — <see cref="ComparerChainedHashSet{T,TFinalizer}" /> needs a comparer: <c>[with(comparer), ...]</c>.</summary>
    [Obsolete(AliasErrors.ComparerRequiredInCollectionExpression, error: true)]
    public static ComparerChainedHashSet<T, TFinalizer> CreateComparerChained<T, TFinalizer>(int capacity, ReadOnlySpan<T> items)
        where T : notnull
        where TFinalizer : struct, IHashFinalizer
        => throw AliasErrors.NotSupported(AliasErrors.ComparerRequiredInCollectionExpression);

    static void AddAll<T, TAlgo>(PooledHashSet<T, TAlgo> set, ReadOnlySpan<T> items)
        where T : notnull
        where TAlgo : struct, IHashAlgorithm<T, T>
    {
        foreach (var item in items)
        {
            set.Add(item);
        }
    }
}
