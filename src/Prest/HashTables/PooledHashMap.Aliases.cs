namespace Prest;

// ---------------------------------------------------------------------------
// Concrete alias types for common PooledHashMap configurations.
//
// All aliases have non-public constructors — users construct via the static
// `Create(...)` factory inherited from the base class (which rents from the
// per-thread cache) or via the `Create(IEqualityComparer<TKey>, ...)` overload
// on the Comparer* variants.
//
// Three shapes per algorithm:
//   {Algo}HashMap<K,V>                — defaults (EqualityDefaultHasher + NoOpHashFinalizer)
//   {Algo}HashMap<K,V, TFinalizer>    — pick a finalizer (e.g. FibonacciFinalizer)
//   Comparer{Algo}HashMap<K,V>        — runtime IEqualityComparer<K>
// ---------------------------------------------------------------------------

/// <summary>
/// Default pooled hash map — SwissTable + default equality comparer + no finalizer.
/// Alias for <see cref="SwissHashMap{TKey,TValue}" />.
/// </summary>
public class PooledHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, SwissTableAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    protected PooledHashMap(int capacity = 0) : base(capacity) { }

    protected PooledHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }
}

// ---- SwissTable ----

/// <summary>SwissTable + default hasher + no finalizer.</summary>
public sealed class SwissHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, SwissTableAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    SwissHashMap(int capacity) : base(capacity) { }

    SwissHashMap(TKey[] rentedKeys, TValue[] rentedValues, int count)
        : base(rentedKeys, rentedValues, count) { }
}

/// <summary>SwissTable + default hasher + user-chosen finalizer.</summary>
public sealed class SwissHashMap<TKey, TValue, TFinalizer>
    : PooledHashMap<TKey, TValue, SwissTableAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, TFinalizer>>
    where TKey : notnull
    where TFinalizer : struct, IHashFinalizer
{
    SwissHashMap(int capacity) : base(capacity) { }
}

/// <summary>SwissTable + user-supplied <see cref="IEqualityComparer{TKey}" /> + no finalizer.</summary>
public sealed class ComparerSwissHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, SwissTableAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    ComparerSwissHashMap(IEqualityComparer<TKey> comparer, int capacity)
        : base(capacity, new SwissTableAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer>(
            new ComparerHasher<TKey>(comparer), default)) { }

    /// <summary>Creates a map that uses <paramref name="comparer" /> for key equality.</summary>
    public static ComparerSwissHashMap<TKey, TValue> Create(IEqualityComparer<TKey> comparer, int capacity = 0)
        => new(comparer, capacity);
}

// ---- Robin Hood ----

/// <summary>Robin Hood + default hasher + no finalizer.</summary>
public sealed class RobinHoodHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, RobinHoodAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    RobinHoodHashMap(int capacity) : base(capacity) { }
}

/// <summary>Robin Hood + default hasher + user-chosen finalizer.</summary>
public sealed class RobinHoodHashMap<TKey, TValue, TFinalizer>
    : PooledHashMap<TKey, TValue, RobinHoodAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, TFinalizer>>
    where TKey : notnull
    where TFinalizer : struct, IHashFinalizer
{
    RobinHoodHashMap(int capacity) : base(capacity) { }
}

/// <summary>Robin Hood + user-supplied <see cref="IEqualityComparer{TKey}" /> + no finalizer.</summary>
public sealed class ComparerRobinHoodHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, RobinHoodAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    ComparerRobinHoodHashMap(IEqualityComparer<TKey> comparer, int capacity)
        : base(capacity, new RobinHoodAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer>(
            new ComparerHasher<TKey>(comparer), default)) { }

    /// <summary>Creates a map that uses <paramref name="comparer" /> for key equality.</summary>
    public static ComparerRobinHoodHashMap<TKey, TValue> Create(IEqualityComparer<TKey> comparer, int capacity = 0)
        => new(comparer, capacity);
}

// ---- Linear probing ----

/// <summary>Linear probing + default hasher + no finalizer.</summary>
public sealed class LinearHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, LinearProbingAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    LinearHashMap(int capacity) : base(capacity) { }
}

/// <summary>Linear probing + default hasher + user-chosen finalizer.</summary>
public sealed class LinearHashMap<TKey, TValue, TFinalizer>
    : PooledHashMap<TKey, TValue, LinearProbingAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, TFinalizer>>
    where TKey : notnull
    where TFinalizer : struct, IHashFinalizer
{
    LinearHashMap(int capacity) : base(capacity) { }
}

/// <summary>Linear probing + user-supplied <see cref="IEqualityComparer{TKey}" /> + no finalizer.</summary>
public sealed class ComparerLinearHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, LinearProbingAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    ComparerLinearHashMap(IEqualityComparer<TKey> comparer, int capacity)
        : base(capacity, new LinearProbingAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer>(
            new ComparerHasher<TKey>(comparer), default)) { }

    /// <summary>Creates a map that uses <paramref name="comparer" /> for key equality.</summary>
    public static ComparerLinearHashMap<TKey, TValue> Create(IEqualityComparer<TKey> comparer, int capacity = 0)
        => new(comparer, capacity);
}

// ---- Chained (separate chaining) ----

/// <summary>Separate chaining + default hasher + no finalizer.</summary>
public sealed class ChainedHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, ChainedAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    ChainedHashMap(int capacity) : base(capacity) { }
}

/// <summary>Separate chaining + default hasher + user-chosen finalizer.</summary>
public sealed class ChainedHashMap<TKey, TValue, TFinalizer>
    : PooledHashMap<TKey, TValue, ChainedAlgorithm<KeyValueSlot<TKey, TValue>, TKey, EqualityDefaultHasher<TKey>, TFinalizer>>
    where TKey : notnull
    where TFinalizer : struct, IHashFinalizer
{
    ChainedHashMap(int capacity) : base(capacity) { }
}

/// <summary>Separate chaining + user-supplied <see cref="IEqualityComparer{TKey}" /> + no finalizer.</summary>
public sealed class ComparerChainedHashMap<TKey, TValue>
    : PooledHashMap<TKey, TValue, ChainedAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer>>
    where TKey : notnull
{
    ComparerChainedHashMap(IEqualityComparer<TKey> comparer, int capacity)
        : base(capacity, new ChainedAlgorithm<KeyValueSlot<TKey, TValue>, TKey, ComparerHasher<TKey>, NoOpHashFinalizer>(
            new ComparerHasher<TKey>(comparer), default)) { }

    /// <summary>Creates a map that uses <paramref name="comparer" /> for key equality.</summary>
    public static ComparerChainedHashMap<TKey, TValue> Create(IEqualityComparer<TKey> comparer, int capacity = 0)
        => new(comparer, capacity);
}
