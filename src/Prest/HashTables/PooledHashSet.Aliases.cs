namespace Prest;

// All aliases have non-public constructors — users construct via the static
// `Create(...)` factory inherited from the base class (or the Comparer* overload
// on the Comparer* variants).
//
// Three shapes per algorithm, mirroring PooledHashMap.Aliases.cs:
//   {Algo}HashSet<T>               — defaults
//   {Algo}HashSet<T, TFinalizer>   — pick a finalizer
//   Comparer{Algo}HashSet<T>       — runtime IEqualityComparer<T>

/// <summary>Default pooled hash set — SwissTable + default equality comparer + no finalizer.</summary>
public class PooledHashSet<T>
    : PooledHashSet<T, SwissTableAlgorithm<T, T, EqualityDefaultHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    protected PooledHashSet(int capacity = 0) : base(capacity) { }
}

// ---- SwissTable ----

/// <summary>SwissTable + default hasher + no finalizer.</summary>
public sealed class SwissHashSet<T>
    : PooledHashSet<T, SwissTableAlgorithm<T, T, EqualityDefaultHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    SwissHashSet(int capacity) : base(capacity) { }
}

/// <summary>SwissTable + default hasher + user-chosen finalizer.</summary>
public sealed class SwissHashSet<T, TFinalizer>
    : PooledHashSet<T, SwissTableAlgorithm<T, T, EqualityDefaultHasher<T>, TFinalizer>>
    where T : notnull
    where TFinalizer : struct, IHashFinalizer
{
    SwissHashSet(int capacity) : base(capacity) { }
}

/// <summary>SwissTable + user-supplied <see cref="IEqualityComparer{T}" /> + no finalizer.</summary>
public sealed class ComparerSwissHashSet<T>
    : PooledHashSet<T, SwissTableAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    ComparerSwissHashSet(IEqualityComparer<T> comparer, int capacity)
        : base(
            capacity,
            new SwissTableAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>(
                new ComparerHasher<T>(comparer),
                default))
    { }

    /// <summary>Creates a set that uses <paramref name="comparer" /> for item equality.</summary>
    public static ComparerSwissHashSet<T> Create(IEqualityComparer<T> comparer, int capacity = 0)
        => new(comparer, capacity);
}

// ---- Robin Hood ----

/// <summary>Robin Hood + default hasher + no finalizer.</summary>
public sealed class RobinHoodHashSet<T>
    : PooledHashSet<T, RobinHoodAlgorithm<T, T, EqualityDefaultHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    RobinHoodHashSet(int capacity) : base(capacity) { }
}

/// <summary>Robin Hood + default hasher + user-chosen finalizer.</summary>
public sealed class RobinHoodHashSet<T, TFinalizer>
    : PooledHashSet<T, RobinHoodAlgorithm<T, T, EqualityDefaultHasher<T>, TFinalizer>>
    where T : notnull
    where TFinalizer : struct, IHashFinalizer
{
    RobinHoodHashSet(int capacity) : base(capacity) { }
}

/// <summary>Robin Hood + user-supplied <see cref="IEqualityComparer{T}" /> + no finalizer.</summary>
public sealed class ComparerRobinHoodHashSet<T>
    : PooledHashSet<T, RobinHoodAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    ComparerRobinHoodHashSet(IEqualityComparer<T> comparer, int capacity)
        : base(
            capacity,
            new RobinHoodAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>(
                new ComparerHasher<T>(comparer),
                default))
    { }

    /// <summary>Creates a set that uses <paramref name="comparer" /> for item equality.</summary>
    public static ComparerRobinHoodHashSet<T> Create(IEqualityComparer<T> comparer, int capacity = 0)
        => new(comparer, capacity);
}

// ---- Linear probing ----

/// <summary>Linear probing + default hasher + no finalizer.</summary>
public sealed class LinearHashSet<T>
    : PooledHashSet<T, LinearProbingAlgorithm<T, T, EqualityDefaultHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    LinearHashSet(int capacity) : base(capacity) { }
}

/// <summary>Linear probing + default hasher + user-chosen finalizer.</summary>
public sealed class LinearHashSet<T, TFinalizer>
    : PooledHashSet<T, LinearProbingAlgorithm<T, T, EqualityDefaultHasher<T>, TFinalizer>>
    where T : notnull
    where TFinalizer : struct, IHashFinalizer
{
    LinearHashSet(int capacity) : base(capacity) { }
}

/// <summary>Linear probing + user-supplied <see cref="IEqualityComparer{T}" /> + no finalizer.</summary>
public sealed class ComparerLinearHashSet<T>
    : PooledHashSet<T, LinearProbingAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    ComparerLinearHashSet(IEqualityComparer<T> comparer, int capacity)
        : base(
            capacity,
            new LinearProbingAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>(
                new ComparerHasher<T>(comparer),
                default))
    { }

    /// <summary>Creates a set that uses <paramref name="comparer" /> for item equality.</summary>
    public static ComparerLinearHashSet<T> Create(IEqualityComparer<T> comparer, int capacity = 0)
        => new(comparer, capacity);
}

// ---- Chained (separate chaining) ----

/// <summary>Separate chaining + default hasher + no finalizer.</summary>
public sealed class ChainedHashSet<T>
    : PooledHashSet<T, ChainedAlgorithm<T, T, EqualityDefaultHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    ChainedHashSet(int capacity) : base(capacity) { }
}

/// <summary>Separate chaining + default hasher + user-chosen finalizer.</summary>
public sealed class ChainedHashSet<T, TFinalizer>
    : PooledHashSet<T, ChainedAlgorithm<T, T, EqualityDefaultHasher<T>, TFinalizer>>
    where T : notnull
    where TFinalizer : struct, IHashFinalizer
{
    ChainedHashSet(int capacity) : base(capacity) { }
}

/// <summary>Separate chaining + user-supplied <see cref="IEqualityComparer{T}" /> + no finalizer.</summary>
public sealed class ComparerChainedHashSet<T>
    : PooledHashSet<T, ChainedAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>>
    where T : notnull
{
    ComparerChainedHashSet(IEqualityComparer<T> comparer, int capacity)
        : base(
            capacity,
            new ChainedAlgorithm<T, T, ComparerHasher<T>, NoOpHashFinalizer>(
                new ComparerHasher<T>(comparer),
                default))
    { }

    /// <summary>Creates a set that uses <paramref name="comparer" /> for item equality.</summary>
    public static ComparerChainedHashSet<T> Create(IEqualityComparer<T> comparer, int capacity = 0)
        => new(comparer, capacity);
}
