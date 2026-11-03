using System.ComponentModel;
using System.Reflection;
using TUnit.Assertions.Enums;

namespace Prest.Tests;

public partial class PooledHashSetTests
{
    [Test]
    public async Task CollectionExpression_DefaultAliasEmpty_ReturnsEmptyDefaultAlias()
    {
        // Arrange — `PooledHashSet<T> set = [];` used to fail with CS0122: with no builder the
        // compiler fell back to the protected constructor plus Add.
        var expectedType = typeof(PooledHashSet<string>);

        // Act
        using PooledHashSet<string> set = [];

        // Assert
        await Assert.That(set.GetType()).IsEqualTo(expectedType);
        await Assert.That(set.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task CollectionExpression_EmptyOnEveryAliasShape_ReturnsEmptyInstanceOfThatAlias(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int EmptyCount = 0;
        var comparer = EqualityComparer<int>.Default;
        Type[] expected = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                typeof(PooledHashSet<int>),
                typeof(SwissHashSet<int>),
                typeof(SwissHashSet<int, FibonacciFinalizer>),
                typeof(SwissHashSet<int, LastDigitHasher, FibonacciFinalizer>),
                typeof(ComparerSwissHashSet<int>),
                typeof(ComparerSwissHashSet<int, FibonacciFinalizer>),
            ],
            HashAlgorithmKind.RobinHood =>
            [
                typeof(RobinHoodHashSet<int>),
                typeof(RobinHoodHashSet<int, FibonacciFinalizer>),
                typeof(RobinHoodHashSet<int, LastDigitHasher, FibonacciFinalizer>),
                typeof(ComparerRobinHoodHashSet<int>),
                typeof(ComparerRobinHoodHashSet<int, FibonacciFinalizer>),
            ],
            HashAlgorithmKind.Linear =>
            [
                typeof(LinearHashSet<int>),
                typeof(LinearHashSet<int, FibonacciFinalizer>),
                typeof(LinearHashSet<int, LastDigitHasher, FibonacciFinalizer>),
                typeof(ComparerLinearHashSet<int>),
                typeof(ComparerLinearHashSet<int, FibonacciFinalizer>),
            ],
            HashAlgorithmKind.Chained =>
            [
                typeof(ChainedHashSet<int>),
                typeof(ChainedHashSet<int, FibonacciFinalizer>),
                typeof(ChainedHashSet<int, LastDigitHasher, FibonacciFinalizer>),
                typeof(ComparerChainedHashSet<int>),
                typeof(ComparerChainedHashSet<int, FibonacciFinalizer>),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Act
        IReadOnlyCollection<int>[] sets = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                (PooledHashSet<int>)[],
                (SwissHashSet<int>)[],
                (SwissHashSet<int, FibonacciFinalizer>)[],
                (SwissHashSet<int, LastDigitHasher, FibonacciFinalizer>)[],
                (ComparerSwissHashSet<int>)[with(comparer)],
                (ComparerSwissHashSet<int, FibonacciFinalizer>)[with(comparer)],
            ],
            HashAlgorithmKind.RobinHood =>
            [
                (RobinHoodHashSet<int>)[],
                (RobinHoodHashSet<int, FibonacciFinalizer>)[],
                (RobinHoodHashSet<int, LastDigitHasher, FibonacciFinalizer>)[],
                (ComparerRobinHoodHashSet<int>)[with(comparer)],
                (ComparerRobinHoodHashSet<int, FibonacciFinalizer>)[with(comparer)],
            ],
            HashAlgorithmKind.Linear =>
            [
                (LinearHashSet<int>)[],
                (LinearHashSet<int, FibonacciFinalizer>)[],
                (LinearHashSet<int, LastDigitHasher, FibonacciFinalizer>)[],
                (ComparerLinearHashSet<int>)[with(comparer)],
                (ComparerLinearHashSet<int, FibonacciFinalizer>)[with(comparer)],
            ],
            HashAlgorithmKind.Chained =>
            [
                (ChainedHashSet<int>)[],
                (ChainedHashSet<int, FibonacciFinalizer>)[],
                (ChainedHashSet<int, LastDigitHasher, FibonacciFinalizer>)[],
                (ComparerChainedHashSet<int>)[with(comparer)],
                (ComparerChainedHashSet<int, FibonacciFinalizer>)[with(comparer)],
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        var types = sets.Select(set => set.GetType()).ToArray();
        var counts = sets.Select(set => set.Count).ToArray();
        DisposeAll(sets.Cast<IDisposable>());

        // Assert
        await Assert.That(types).IsEquivalentTo(expected, CollectionOrdering.Matching);
        foreach (var count in counts)
        {
            await Assert.That(count).IsEqualTo(EmptyCount);
        }
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task CollectionExpression_DuplicateItems_AreAddedOnce(HashAlgorithmKind algorithm)
    {
        // Arrange — 1 and 2 also stay distinct under LastDigitHasher.
        const int Item = 1;
        const int OtherItem = 2;
        const int ExpectedCount = 2;
        int[] expectedItems = [Item, OtherItem];
        var comparer = EqualityComparer<int>.Default;

        // Act
        IReadOnlyCollection<int>[] sets = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                (PooledHashSet<int>)[Item, OtherItem, Item],
                (SwissHashSet<int>)[Item, OtherItem, Item],
                (SwissHashSet<int, FibonacciFinalizer>)[Item, OtherItem, Item],
                (SwissHashSet<int, LastDigitHasher, FibonacciFinalizer>)[Item, OtherItem, Item],
                (ComparerSwissHashSet<int>)[with(comparer), Item, OtherItem, Item],
                (ComparerSwissHashSet<int, FibonacciFinalizer>)[with(comparer), Item, OtherItem, Item],
            ],
            HashAlgorithmKind.RobinHood =>
            [
                (RobinHoodHashSet<int>)[Item, OtherItem, Item],
                (RobinHoodHashSet<int, FibonacciFinalizer>)[Item, OtherItem, Item],
                (RobinHoodHashSet<int, LastDigitHasher, FibonacciFinalizer>)[Item, OtherItem, Item],
                (ComparerRobinHoodHashSet<int>)[with(comparer), Item, OtherItem, Item],
                (ComparerRobinHoodHashSet<int, FibonacciFinalizer>)[with(comparer), Item, OtherItem, Item],
            ],
            HashAlgorithmKind.Linear =>
            [
                (LinearHashSet<int>)[Item, OtherItem, Item],
                (LinearHashSet<int, FibonacciFinalizer>)[Item, OtherItem, Item],
                (LinearHashSet<int, LastDigitHasher, FibonacciFinalizer>)[Item, OtherItem, Item],
                (ComparerLinearHashSet<int>)[with(comparer), Item, OtherItem, Item],
                (ComparerLinearHashSet<int, FibonacciFinalizer>)[with(comparer), Item, OtherItem, Item],
            ],
            HashAlgorithmKind.Chained =>
            [
                (ChainedHashSet<int>)[Item, OtherItem, Item],
                (ChainedHashSet<int, FibonacciFinalizer>)[Item, OtherItem, Item],
                (ChainedHashSet<int, LastDigitHasher, FibonacciFinalizer>)[Item, OtherItem, Item],
                (ComparerChainedHashSet<int>)[with(comparer), Item, OtherItem, Item],
                (ComparerChainedHashSet<int, FibonacciFinalizer>)[with(comparer), Item, OtherItem, Item],
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        var contents = sets.Select(set => (set.Count, Items: set.Order().ToArray())).ToArray();
        DisposeAll(sets.Cast<IDisposable>());

        // Assert
        foreach (var (count, items) in contents)
        {
            await Assert.That(count).IsEqualTo(ExpectedCount);
            await Assert.That(items).IsEquivalentTo(expectedItems, CollectionOrdering.Matching);
        }
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task CollectionExpression_WithCapacity_PreSizesEveryAliasShape(HashAlgorithmKind algorithm)
    {
        // Arrange — 100 is well above the default first allocation, so the size must come from with(...).
        const int Capacity = 100;
        var comparer = EqualityComparer<int>.Default;

        // Act
        int[] capacities = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                FreshCapacity(() => (PooledHashSet<int>)[with(Capacity)]),
                FreshCapacity(() => (SwissHashSet<int>)[with(Capacity)]),
                FreshCapacity(() => (SwissHashSet<int, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (SwissHashSet<int, LastDigitHasher, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (ComparerSwissHashSet<int>)[with(comparer, Capacity)]),
                FreshCapacity(() => (ComparerSwissHashSet<int, FibonacciFinalizer>)[with(comparer, Capacity)]),
            ],
            HashAlgorithmKind.RobinHood =>
            [
                FreshCapacity(() => (RobinHoodHashSet<int>)[with(Capacity)]),
                FreshCapacity(() => (RobinHoodHashSet<int, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (RobinHoodHashSet<int, LastDigitHasher, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (ComparerRobinHoodHashSet<int>)[with(comparer, Capacity)]),
                FreshCapacity(() => (ComparerRobinHoodHashSet<int, FibonacciFinalizer>)[with(comparer, Capacity)]),
            ],
            HashAlgorithmKind.Linear =>
            [
                FreshCapacity(() => (LinearHashSet<int>)[with(Capacity)]),
                FreshCapacity(() => (LinearHashSet<int, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (LinearHashSet<int, LastDigitHasher, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (ComparerLinearHashSet<int>)[with(comparer, Capacity)]),
                FreshCapacity(() => (ComparerLinearHashSet<int, FibonacciFinalizer>)[with(comparer, Capacity)]),
            ],
            HashAlgorithmKind.Chained =>
            [
                FreshCapacity(() => (ChainedHashSet<int>)[with(Capacity)]),
                FreshCapacity(() => (ChainedHashSet<int, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (ChainedHashSet<int, LastDigitHasher, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (ComparerChainedHashSet<int>)[with(comparer, Capacity)]),
                FreshCapacity(() => (ComparerChainedHashSet<int, FibonacciFinalizer>)[with(comparer, Capacity)]),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        foreach (var capacity in capacities)
        {
            await Assert.That(capacity).IsGreaterThanOrEqualTo(Capacity);
        }
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task CollectionExpression_ComparerAliasWithComparer_UsesThatComparer(HashAlgorithmKind algorithm)
    {
        // Arrange — OrdinalIgnoreCase makes "A" and "a" one item, so the later one is ignored.
        const string Upper = "A";
        const string Lower = "a";
        const int Capacity = 32;
        const int ExpectedCount = 1;
        var comparer = StringComparer.OrdinalIgnoreCase;

        // Act
        IReadOnlyCollection<string>[] sets = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                (ComparerSwissHashSet<string>)[with(comparer), Upper, Lower],
                (ComparerSwissHashSet<string>)[with(comparer, Capacity), Upper, Lower],
                (ComparerSwissHashSet<string, FibonacciFinalizer>)[with(comparer), Upper, Lower],
                (ComparerSwissHashSet<string, FibonacciFinalizer>)[with(comparer, Capacity), Upper, Lower],
            ],
            HashAlgorithmKind.RobinHood =>
            [
                (ComparerRobinHoodHashSet<string>)[with(comparer), Upper, Lower],
                (ComparerRobinHoodHashSet<string>)[with(comparer, Capacity), Upper, Lower],
                (ComparerRobinHoodHashSet<string, FibonacciFinalizer>)[with(comparer), Upper, Lower],
                (ComparerRobinHoodHashSet<string, FibonacciFinalizer>)[with(comparer, Capacity), Upper, Lower],
            ],
            HashAlgorithmKind.Linear =>
            [
                (ComparerLinearHashSet<string>)[with(comparer), Upper, Lower],
                (ComparerLinearHashSet<string>)[with(comparer, Capacity), Upper, Lower],
                (ComparerLinearHashSet<string, FibonacciFinalizer>)[with(comparer), Upper, Lower],
                (ComparerLinearHashSet<string, FibonacciFinalizer>)[with(comparer, Capacity), Upper, Lower],
            ],
            HashAlgorithmKind.Chained =>
            [
                (ComparerChainedHashSet<string>)[with(comparer), Upper, Lower],
                (ComparerChainedHashSet<string>)[with(comparer, Capacity), Upper, Lower],
                (ComparerChainedHashSet<string, FibonacciFinalizer>)[with(comparer), Upper, Lower],
                (ComparerChainedHashSet<string, FibonacciFinalizer>)[with(comparer, Capacity), Upper, Lower],
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        var contents = sets.Select(set => (set.Count, StoredItem: set.Single())).ToArray();
        DisposeAll(sets.Cast<IDisposable>());

        // Assert
        foreach (var (count, storedItem) in contents)
        {
            await Assert.That(count).IsEqualTo(ExpectedCount);
            await Assert.That(storedItem).IsEqualTo(Upper);
        }
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task CollectionExpression_SpreadOfAnotherSet_CopiesItsItems(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int First = 1;
        const int Second = 2;
        const int Extra = 3;
        const int ExpectedCount = 3;
        int[] expectedItems = [First, Second, Extra];
        using PooledHashSet<int> source = [First, Second];

        // Act
        IReadOnlyCollection<int> copy = algorithm switch
        {
            HashAlgorithmKind.Swiss => (SwissHashSet<int>)[.. source, Extra],
            HashAlgorithmKind.RobinHood => (RobinHoodHashSet<int>)[.. source, Extra],
            HashAlgorithmKind.Linear => (LinearHashSet<int>)[.. source, Extra],
            HashAlgorithmKind.Chained => (ChainedHashSet<int>)[.. source, Extra],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        using var owner = (IDisposable)copy;
        var items = copy.Order().ToArray();

        // Assert
        await Assert.That(copy.Count).IsEqualTo(ExpectedCount);
        await Assert.That(items).IsEquivalentTo(expectedItems, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task CollectionExpression_Dispose_RoundTripsThroughAliasCache(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int Item = 1;
        var comparer = EqualityComparer<int>.Default;
        (Func<IDisposable> Build, Func<IDisposable> Create)[] aliases = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                (() => (PooledHashSet<int>)[Item], PooledHashSet<int>.Create),
                (() => (SwissHashSet<int>)[Item], SwissHashSet<int>.Create),
                (() => (SwissHashSet<int, FibonacciFinalizer>)[Item], SwissHashSet<int, FibonacciFinalizer>.Create),
                (() => (SwissHashSet<int, LastDigitHasher, FibonacciFinalizer>)[Item], SwissHashSet<int, LastDigitHasher, FibonacciFinalizer>.Create),
                (() => (ComparerSwissHashSet<int>)[with(comparer), Item], () => ComparerSwissHashSet<int>.Create(comparer)),
                (() => (ComparerSwissHashSet<int, FibonacciFinalizer>)[with(comparer), Item], () => ComparerSwissHashSet<int, FibonacciFinalizer>.Create(comparer)),
            ],
            HashAlgorithmKind.RobinHood =>
            [
                (() => (RobinHoodHashSet<int>)[Item], RobinHoodHashSet<int>.Create),
                (() => (RobinHoodHashSet<int, FibonacciFinalizer>)[Item], RobinHoodHashSet<int, FibonacciFinalizer>.Create),
                (() => (RobinHoodHashSet<int, LastDigitHasher, FibonacciFinalizer>)[Item], RobinHoodHashSet<int, LastDigitHasher, FibonacciFinalizer>.Create),
                (() => (ComparerRobinHoodHashSet<int>)[with(comparer), Item], () => ComparerRobinHoodHashSet<int>.Create(comparer)),
                (() => (ComparerRobinHoodHashSet<int, FibonacciFinalizer>)[with(comparer), Item], () => ComparerRobinHoodHashSet<int, FibonacciFinalizer>.Create(comparer)),
            ],
            HashAlgorithmKind.Linear =>
            [
                (() => (LinearHashSet<int>)[Item], LinearHashSet<int>.Create),
                (() => (LinearHashSet<int, FibonacciFinalizer>)[Item], LinearHashSet<int, FibonacciFinalizer>.Create),
                (() => (LinearHashSet<int, LastDigitHasher, FibonacciFinalizer>)[Item], LinearHashSet<int, LastDigitHasher, FibonacciFinalizer>.Create),
                (() => (ComparerLinearHashSet<int>)[with(comparer), Item], () => ComparerLinearHashSet<int>.Create(comparer)),
                (() => (ComparerLinearHashSet<int, FibonacciFinalizer>)[with(comparer), Item], () => ComparerLinearHashSet<int, FibonacciFinalizer>.Create(comparer)),
            ],
            HashAlgorithmKind.Chained =>
            [
                (() => (ChainedHashSet<int>)[Item], ChainedHashSet<int>.Create),
                (() => (ChainedHashSet<int, FibonacciFinalizer>)[Item], ChainedHashSet<int, FibonacciFinalizer>.Create),
                (() => (ChainedHashSet<int, LastDigitHasher, FibonacciFinalizer>)[Item], ChainedHashSet<int, LastDigitHasher, FibonacciFinalizer>.Create),
                (() => (ComparerChainedHashSet<int>)[with(comparer), Item], () => ComparerChainedHashSet<int>.Create(comparer)),
                (() => (ComparerChainedHashSet<int, FibonacciFinalizer>)[with(comparer), Item], () => ComparerChainedHashSet<int, FibonacciFinalizer>.Create(comparer)),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Act
        var rounds = aliases.Select(alias => DisposeThroughCache(alias.Build, alias.Create)).ToArray();

        // Assert
        foreach (var (built, created, rebuilt) in rounds)
        {
            await Assert.That(created).IsSameReferenceAs(built);
            await Assert.That(rebuilt).IsSameReferenceAs(created);
        }
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Create_ComparerOnDefaultComparerAlias_IsCompileErrorNamingComparerAlias(HashAlgorithmKind algorithm)
    {
        // Arrange — this overload used to be missing, so Create(comparer) failed with a confusing
        // CS1503 ("cannot convert ... to int"). It exists only to name the alias that takes a comparer.
        (Type Alias, string ComparerAlias)[] aliases = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                (typeof(SwissHashSet<int>), "ComparerSwissHashSet<T>"),
                (typeof(SwissHashSet<int, FibonacciFinalizer>), "ComparerSwissHashSet<T,TFinalizer>"),
            ],
            HashAlgorithmKind.RobinHood =>
            [
                (typeof(RobinHoodHashSet<int>), "ComparerRobinHoodHashSet<T>"),
                (typeof(RobinHoodHashSet<int, FibonacciFinalizer>), "ComparerRobinHoodHashSet<T,TFinalizer>"),
            ],
            HashAlgorithmKind.Linear =>
            [
                (typeof(LinearHashSet<int>), "ComparerLinearHashSet<T>"),
                (typeof(LinearHashSet<int, FibonacciFinalizer>), "ComparerLinearHashSet<T,TFinalizer>"),
            ],
            HashAlgorithmKind.Chained =>
            [
                (typeof(ChainedHashSet<int>), "ComparerChainedHashSet<T>"),
                (typeof(ChainedHashSet<int, FibonacciFinalizer>), "ComparerChainedHashSet<T,TFinalizer>"),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        Type[] comparerSignature = [typeof(IEqualityComparer<int>), typeof(int)];

        // Act
        var probes = aliases
            .Select(alias => (
                alias.ComparerAlias,
                Probe: ProbeHiddenCreate(alias.Alias, comparerSignature),
                Message: ObsoleteMessage(alias.Alias, comparerSignature),
                ThrownMessage: ThrownByCreate(alias.Alias, comparerSignature).Message))
            .ToArray();

        // Assert
        foreach (var (comparerAlias, (isCompileError, isHiddenFromIntelliSense, thrown), message, thrownMessage) in probes)
        {
            await Assert.That(isCompileError).IsTrue();
            await Assert.That(isHiddenFromIntelliSense).IsTrue();
            await Assert.That(thrown).IsEqualTo(typeof(NotSupportedException));
            await Assert.That(message).Contains("declare the set as");
            await Assert.That(message).Contains(comparerAlias);
            await Assert.That(message).Contains("Create(comparer) and [with(comparer)] work there");
            await Assert.That(thrownMessage).IsEqualTo(message);
        }
    }

    [Test]
    public async Task CollectionBuilder_ComparerMisuseOverloads_AreCompileErrorsThatThrowNotSupported()
    {
        // Arrange — two overloads for each of the 8 aliases that always use the default comparer
        // ([with(comparer), ...] and [with(comparer, capacity), ...]) and for each of the 8 Comparer*
        // aliases ([...] and [with(capacity), ...]). PooledHashSet<T> takes a comparer, so it has none.
        const int DefaultComparerAliasOverloads = 16;
        const int ComparerAliasOverloads = 16;
        const string ComparerHint = "use [with(comparer), ...]";
        Type[] comparerSignature = [typeof(IEqualityComparer<int>), typeof(int)];

        // Act
        var overloads = CollectionBuilderProbe.ProbeObsoleteOverloads(typeof(PooledHashSetCollectionBuilder), [typeof(int)]);
        var forComparerAliases = overloads.Where(overload => overload.Builds.Name.StartsWith("Comparer", StringComparison.Ordinal)).ToArray();
        var forDefaultComparerAliases = overloads.Except(forComparerAliases).ToArray();

        // Assert
        await Assert.That(forDefaultComparerAliases.Length).IsEqualTo(DefaultComparerAliasOverloads);
        await Assert.That(forComparerAliases.Length).IsEqualTo(ComparerAliasOverloads);
        foreach (var (_, obsolete, thrown) in overloads)
        {
            await Assert.That(obsolete.IsError).IsTrue();
            await Assert.That(thrown).IsTypeOf<NotSupportedException>();
            await Assert.That(thrown!.Message).IsEqualTo(obsolete.Message);
        }
        foreach (var (_, obsolete, _) in forComparerAliases)
        {
            await Assert.That(obsolete.Message).Contains(ComparerHint);
        }
        foreach (var (builds, obsolete, _) in forDefaultComparerAliases)
        {
            await Assert.That(obsolete.Message).IsEqualTo(ObsoleteMessage(builds, comparerSignature));
        }
    }

    // Builds once to take whatever another test left in the alias's cache (a cached instance keeps
    // its size), then builds again, which has to allocate.
    static int FreshCapacity<T, TAlgo>(Func<PooledHashSet<T, TAlgo>> build)
        where T : notnull
        where TAlgo : struct, IHashAlgorithm<T, T>
    {
        _ = build();
        using var set = build();
        return set.Capacity;
    }

    // Disposes a set built by a collection expression, takes it back with Create, disposes that,
    // and builds again. Each step should get the instance the previous one returned to the cache.
    static (IDisposable Built, IDisposable Created, IDisposable Rebuilt) DisposeThroughCache(
        Func<IDisposable> build, Func<IDisposable> create)
    {
        var built = build();
        built.Dispose();
        var created = create();
        created.Dispose();
        var rebuilt = build();
        rebuilt.Dispose();
        return (built, created, rebuilt);
    }

    // Calls a hidden Create overload with a null comparer and zero capacity; it must throw.
    static Exception ThrownByCreate(Type alias, Type[] signature)
    {
        var method = alias.GetMethod("Create", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly, signature)!;
        try
        {
            method.Invoke(null, [null, 0]);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            return e.InnerException;
        }
        throw new InvalidOperationException($"{method} did not throw.");
    }

    static string? ObsoleteMessage(Type alias, Type[] signature)
        => alias.GetMethod("Create", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly, signature)
            ?.GetCustomAttribute<ObsoleteAttribute>()
            ?.Message;
}
