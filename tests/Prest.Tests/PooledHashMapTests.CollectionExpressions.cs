using System.ComponentModel;
using System.Reflection;
using TUnit.Assertions.Enums;

namespace Prest.Tests;

public partial class PooledHashMapTests
{
    [Test]
    public async Task CollectionExpression_DefaultAliasEmpty_ReturnsEmptyDefaultAlias()
    {
        // Arrange — `PooledHashMap<K,V> map = [];` used to fail with CS0122: with no builder the
        // compiler fell back to the protected constructor plus Add.
        var expectedType = typeof(PooledHashMap<string, int>);

        // Act
        using PooledHashMap<string, int> map = [];

        // Assert
        await Assert.That(map.GetType()).IsEqualTo(expectedType);
        await Assert.That(map.Count).IsEqualTo(0);
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
                typeof(PooledHashMap<int, int>),
                typeof(SwissHashMap<int, int>),
                typeof(SwissHashMap<int, int, FibonacciFinalizer>),
                typeof(SwissHashMap<int, int, LastDigitHasher, FibonacciFinalizer>),
                typeof(ComparerSwissHashMap<int, int>),
                typeof(ComparerSwissHashMap<int, int, FibonacciFinalizer>),
            ],
            HashAlgorithmKind.RobinHood =>
            [
                typeof(RobinHoodHashMap<int, int>),
                typeof(RobinHoodHashMap<int, int, FibonacciFinalizer>),
                typeof(RobinHoodHashMap<int, int, LastDigitHasher, FibonacciFinalizer>),
                typeof(ComparerRobinHoodHashMap<int, int>),
                typeof(ComparerRobinHoodHashMap<int, int, FibonacciFinalizer>),
            ],
            HashAlgorithmKind.Linear =>
            [
                typeof(LinearHashMap<int, int>),
                typeof(LinearHashMap<int, int, FibonacciFinalizer>),
                typeof(LinearHashMap<int, int, LastDigitHasher, FibonacciFinalizer>),
                typeof(ComparerLinearHashMap<int, int>),
                typeof(ComparerLinearHashMap<int, int, FibonacciFinalizer>),
            ],
            HashAlgorithmKind.Chained =>
            [
                typeof(ChainedHashMap<int, int>),
                typeof(ChainedHashMap<int, int, FibonacciFinalizer>),
                typeof(ChainedHashMap<int, int, LastDigitHasher, FibonacciFinalizer>),
                typeof(ComparerChainedHashMap<int, int>),
                typeof(ComparerChainedHashMap<int, int, FibonacciFinalizer>),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Act
        IReadOnlyDictionary<int, int>[] maps = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                (PooledHashMap<int, int>)[],
                (SwissHashMap<int, int>)[],
                (SwissHashMap<int, int, FibonacciFinalizer>)[],
                (SwissHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[],
                (ComparerSwissHashMap<int, int>)[with(comparer)],
                (ComparerSwissHashMap<int, int, FibonacciFinalizer>)[with(comparer)],
            ],
            HashAlgorithmKind.RobinHood =>
            [
                (RobinHoodHashMap<int, int>)[],
                (RobinHoodHashMap<int, int, FibonacciFinalizer>)[],
                (RobinHoodHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[],
                (ComparerRobinHoodHashMap<int, int>)[with(comparer)],
                (ComparerRobinHoodHashMap<int, int, FibonacciFinalizer>)[with(comparer)],
            ],
            HashAlgorithmKind.Linear =>
            [
                (LinearHashMap<int, int>)[],
                (LinearHashMap<int, int, FibonacciFinalizer>)[],
                (LinearHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[],
                (ComparerLinearHashMap<int, int>)[with(comparer)],
                (ComparerLinearHashMap<int, int, FibonacciFinalizer>)[with(comparer)],
            ],
            HashAlgorithmKind.Chained =>
            [
                (ChainedHashMap<int, int>)[],
                (ChainedHashMap<int, int, FibonacciFinalizer>)[],
                (ChainedHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[],
                (ComparerChainedHashMap<int, int>)[with(comparer)],
                (ComparerChainedHashMap<int, int, FibonacciFinalizer>)[with(comparer)],
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        var types = maps.Select(map => map.GetType()).ToArray();
        var counts = maps.Select(map => map.Count).ToArray();
        DisposeAll(maps.Cast<IDisposable>());

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
    public async Task CollectionExpression_EntriesOnEveryAliasShape_ContainsEveryEntry(HashAlgorithmKind algorithm)
    {
        // Arrange — keys 1 and 2 also stay distinct under LastDigitHasher.
        const int FirstKey = 1;
        const int SecondKey = 2;
        const int FirstValue = 10;
        const int SecondValue = 20;
        const int ExpectedCount = 2;
        KeyValuePair<int, int> first = new(FirstKey, FirstValue);
        KeyValuePair<int, int> second = new(SecondKey, SecondValue);
        var comparer = EqualityComparer<int>.Default;

        // Act
        IReadOnlyDictionary<int, int>[] maps = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                (PooledHashMap<int, int>)[first, second],
                (SwissHashMap<int, int>)[first, second],
                (SwissHashMap<int, int, FibonacciFinalizer>)[first, second],
                (SwissHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[first, second],
                (ComparerSwissHashMap<int, int>)[with(comparer), first, second],
                (ComparerSwissHashMap<int, int, FibonacciFinalizer>)[with(comparer), first, second],
            ],
            HashAlgorithmKind.RobinHood =>
            [
                (RobinHoodHashMap<int, int>)[first, second],
                (RobinHoodHashMap<int, int, FibonacciFinalizer>)[first, second],
                (RobinHoodHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[first, second],
                (ComparerRobinHoodHashMap<int, int>)[with(comparer), first, second],
                (ComparerRobinHoodHashMap<int, int, FibonacciFinalizer>)[with(comparer), first, second],
            ],
            HashAlgorithmKind.Linear =>
            [
                (LinearHashMap<int, int>)[first, second],
                (LinearHashMap<int, int, FibonacciFinalizer>)[first, second],
                (LinearHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[first, second],
                (ComparerLinearHashMap<int, int>)[with(comparer), first, second],
                (ComparerLinearHashMap<int, int, FibonacciFinalizer>)[with(comparer), first, second],
            ],
            HashAlgorithmKind.Chained =>
            [
                (ChainedHashMap<int, int>)[first, second],
                (ChainedHashMap<int, int, FibonacciFinalizer>)[first, second],
                (ChainedHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[first, second],
                (ComparerChainedHashMap<int, int>)[with(comparer), first, second],
                (ComparerChainedHashMap<int, int, FibonacciFinalizer>)[with(comparer), first, second],
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        var contents = maps.Select(map => (map.Count, First: map[FirstKey], Second: map[SecondKey])).ToArray();
        DisposeAll(maps.Cast<IDisposable>());

        // Assert
        foreach (var (count, firstValue, secondValue) in contents)
        {
            await Assert.That(count).IsEqualTo(ExpectedCount);
            await Assert.That(firstValue).IsEqualTo(FirstValue);
            await Assert.That(secondValue).IsEqualTo(SecondValue);
        }
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task CollectionExpression_RepeatedKey_LastEntryWins(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int Key = 1;
        const int FirstValue = 10;
        const int LastValue = 20;
        const int ExpectedCount = 1;
        KeyValuePair<int, int> first = new(Key, FirstValue);
        KeyValuePair<int, int> last = new(Key, LastValue);
        var comparer = EqualityComparer<int>.Default;

        // Act
        IReadOnlyDictionary<int, int>[] maps = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                (PooledHashMap<int, int>)[first, last],
                (SwissHashMap<int, int>)[first, last],
                (SwissHashMap<int, int, FibonacciFinalizer>)[first, last],
                (SwissHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[first, last],
                (ComparerSwissHashMap<int, int>)[with(comparer), first, last],
                (ComparerSwissHashMap<int, int, FibonacciFinalizer>)[with(comparer), first, last],
            ],
            HashAlgorithmKind.RobinHood =>
            [
                (RobinHoodHashMap<int, int>)[first, last],
                (RobinHoodHashMap<int, int, FibonacciFinalizer>)[first, last],
                (RobinHoodHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[first, last],
                (ComparerRobinHoodHashMap<int, int>)[with(comparer), first, last],
                (ComparerRobinHoodHashMap<int, int, FibonacciFinalizer>)[with(comparer), first, last],
            ],
            HashAlgorithmKind.Linear =>
            [
                (LinearHashMap<int, int>)[first, last],
                (LinearHashMap<int, int, FibonacciFinalizer>)[first, last],
                (LinearHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[first, last],
                (ComparerLinearHashMap<int, int>)[with(comparer), first, last],
                (ComparerLinearHashMap<int, int, FibonacciFinalizer>)[with(comparer), first, last],
            ],
            HashAlgorithmKind.Chained =>
            [
                (ChainedHashMap<int, int>)[first, last],
                (ChainedHashMap<int, int, FibonacciFinalizer>)[first, last],
                (ChainedHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[first, last],
                (ComparerChainedHashMap<int, int>)[with(comparer), first, last],
                (ComparerChainedHashMap<int, int, FibonacciFinalizer>)[with(comparer), first, last],
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        var contents = maps.Select(map => (map.Count, Value: map[Key])).ToArray();
        DisposeAll(maps.Cast<IDisposable>());

        // Assert
        foreach (var (count, value) in contents)
        {
            await Assert.That(count).IsEqualTo(ExpectedCount);
            await Assert.That(value).IsEqualTo(LastValue);
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
                FreshCapacity(() => (PooledHashMap<int, int>)[with(Capacity)]),
                FreshCapacity(() => (SwissHashMap<int, int>)[with(Capacity)]),
                FreshCapacity(() => (SwissHashMap<int, int, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (SwissHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (ComparerSwissHashMap<int, int>)[with(comparer, Capacity)]),
                FreshCapacity(() => (ComparerSwissHashMap<int, int, FibonacciFinalizer>)[with(comparer, Capacity)]),
            ],
            HashAlgorithmKind.RobinHood =>
            [
                FreshCapacity(() => (RobinHoodHashMap<int, int>)[with(Capacity)]),
                FreshCapacity(() => (RobinHoodHashMap<int, int, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (RobinHoodHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (ComparerRobinHoodHashMap<int, int>)[with(comparer, Capacity)]),
                FreshCapacity(() => (ComparerRobinHoodHashMap<int, int, FibonacciFinalizer>)[with(comparer, Capacity)]),
            ],
            HashAlgorithmKind.Linear =>
            [
                FreshCapacity(() => (LinearHashMap<int, int>)[with(Capacity)]),
                FreshCapacity(() => (LinearHashMap<int, int, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (LinearHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (ComparerLinearHashMap<int, int>)[with(comparer, Capacity)]),
                FreshCapacity(() => (ComparerLinearHashMap<int, int, FibonacciFinalizer>)[with(comparer, Capacity)]),
            ],
            HashAlgorithmKind.Chained =>
            [
                FreshCapacity(() => (ChainedHashMap<int, int>)[with(Capacity)]),
                FreshCapacity(() => (ChainedHashMap<int, int, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (ChainedHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[with(Capacity)]),
                FreshCapacity(() => (ComparerChainedHashMap<int, int>)[with(comparer, Capacity)]),
                FreshCapacity(() => (ComparerChainedHashMap<int, int, FibonacciFinalizer>)[with(comparer, Capacity)]),
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
        // Arrange — OrdinalIgnoreCase makes "A" and "a" one key, so the later entry replaces the earlier.
        const string UpperKey = "A";
        const string LowerKey = "a";
        const int FirstValue = 1;
        const int LastValue = 2;
        const int Capacity = 32;
        const int ExpectedCount = 1;
        KeyValuePair<string, int> upper = new(UpperKey, FirstValue);
        KeyValuePair<string, int> lower = new(LowerKey, LastValue);
        var comparer = StringComparer.OrdinalIgnoreCase;

        // Act
        IReadOnlyDictionary<string, int>[] maps = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                (ComparerSwissHashMap<string, int>)[with(comparer), upper, lower],
                (ComparerSwissHashMap<string, int>)[with(comparer, Capacity), upper, lower],
                (ComparerSwissHashMap<string, int, FibonacciFinalizer>)[with(comparer), upper, lower],
                (ComparerSwissHashMap<string, int, FibonacciFinalizer>)[with(comparer, Capacity), upper, lower],
            ],
            HashAlgorithmKind.RobinHood =>
            [
                (ComparerRobinHoodHashMap<string, int>)[with(comparer), upper, lower],
                (ComparerRobinHoodHashMap<string, int>)[with(comparer, Capacity), upper, lower],
                (ComparerRobinHoodHashMap<string, int, FibonacciFinalizer>)[with(comparer), upper, lower],
                (ComparerRobinHoodHashMap<string, int, FibonacciFinalizer>)[with(comparer, Capacity), upper, lower],
            ],
            HashAlgorithmKind.Linear =>
            [
                (ComparerLinearHashMap<string, int>)[with(comparer), upper, lower],
                (ComparerLinearHashMap<string, int>)[with(comparer, Capacity), upper, lower],
                (ComparerLinearHashMap<string, int, FibonacciFinalizer>)[with(comparer), upper, lower],
                (ComparerLinearHashMap<string, int, FibonacciFinalizer>)[with(comparer, Capacity), upper, lower],
            ],
            HashAlgorithmKind.Chained =>
            [
                (ComparerChainedHashMap<string, int>)[with(comparer), upper, lower],
                (ComparerChainedHashMap<string, int>)[with(comparer, Capacity), upper, lower],
                (ComparerChainedHashMap<string, int, FibonacciFinalizer>)[with(comparer), upper, lower],
                (ComparerChainedHashMap<string, int, FibonacciFinalizer>)[with(comparer, Capacity), upper, lower],
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        var contents = maps.Select(map => (map.Count, StoredKey: map.Keys.Single(), Value: map[UpperKey])).ToArray();
        DisposeAll(maps.Cast<IDisposable>());

        // Assert
        foreach (var (count, storedKey, value) in contents)
        {
            await Assert.That(count).IsEqualTo(ExpectedCount);
            await Assert.That(storedKey).IsEqualTo(LowerKey);
            await Assert.That(value).IsEqualTo(LastValue);
        }
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task CollectionExpression_SpreadOfAnotherMap_CopiesItsEntries(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int FirstKey = 1;
        const int SecondKey = 2;
        const int ExtraKey = 3;
        const int FirstValue = 10;
        const int SecondValue = 20;
        const int ExtraValue = 30;
        const int ExpectedCount = 3;
        using PooledHashMap<int, int> source = [new(FirstKey, FirstValue), new(SecondKey, SecondValue)];
        KeyValuePair<int, int> extra = new(ExtraKey, ExtraValue);

        // Act
        IReadOnlyDictionary<int, int> copy = algorithm switch
        {
            HashAlgorithmKind.Swiss => (SwissHashMap<int, int>)[.. source, extra],
            HashAlgorithmKind.RobinHood => (RobinHoodHashMap<int, int>)[.. source, extra],
            HashAlgorithmKind.Linear => (LinearHashMap<int, int>)[.. source, extra],
            HashAlgorithmKind.Chained => (ChainedHashMap<int, int>)[.. source, extra],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        using var owner = (IDisposable)copy;

        // Assert
        await Assert.That(copy.Count).IsEqualTo(ExpectedCount);
        await Assert.That(copy[FirstKey]).IsEqualTo(FirstValue);
        await Assert.That(copy[SecondKey]).IsEqualTo(SecondValue);
        await Assert.That(copy[ExtraKey]).IsEqualTo(ExtraValue);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task CollectionExpression_Dispose_RoundTripsThroughAliasCache(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int Key = 1;
        const int Value = 10;
        KeyValuePair<int, int> entry = new(Key, Value);
        var comparer = EqualityComparer<int>.Default;
        (Func<IDisposable> Build, Func<IDisposable> Create)[] aliases = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                (() => (PooledHashMap<int, int>)[entry], PooledHashMap<int, int>.Create),
                (() => (SwissHashMap<int, int>)[entry], SwissHashMap<int, int>.Create),
                (() => (SwissHashMap<int, int, FibonacciFinalizer>)[entry], SwissHashMap<int, int, FibonacciFinalizer>.Create),
                (() => (SwissHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[entry], SwissHashMap<int, int, LastDigitHasher, FibonacciFinalizer>.Create),
                (() => (ComparerSwissHashMap<int, int>)[with(comparer), entry], () => ComparerSwissHashMap<int, int>.Create(comparer)),
                (() => (ComparerSwissHashMap<int, int, FibonacciFinalizer>)[with(comparer), entry], () => ComparerSwissHashMap<int, int, FibonacciFinalizer>.Create(comparer)),
            ],
            HashAlgorithmKind.RobinHood =>
            [
                (() => (RobinHoodHashMap<int, int>)[entry], RobinHoodHashMap<int, int>.Create),
                (() => (RobinHoodHashMap<int, int, FibonacciFinalizer>)[entry], RobinHoodHashMap<int, int, FibonacciFinalizer>.Create),
                (() => (RobinHoodHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[entry], RobinHoodHashMap<int, int, LastDigitHasher, FibonacciFinalizer>.Create),
                (() => (ComparerRobinHoodHashMap<int, int>)[with(comparer), entry], () => ComparerRobinHoodHashMap<int, int>.Create(comparer)),
                (() => (ComparerRobinHoodHashMap<int, int, FibonacciFinalizer>)[with(comparer), entry], () => ComparerRobinHoodHashMap<int, int, FibonacciFinalizer>.Create(comparer)),
            ],
            HashAlgorithmKind.Linear =>
            [
                (() => (LinearHashMap<int, int>)[entry], LinearHashMap<int, int>.Create),
                (() => (LinearHashMap<int, int, FibonacciFinalizer>)[entry], LinearHashMap<int, int, FibonacciFinalizer>.Create),
                (() => (LinearHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[entry], LinearHashMap<int, int, LastDigitHasher, FibonacciFinalizer>.Create),
                (() => (ComparerLinearHashMap<int, int>)[with(comparer), entry], () => ComparerLinearHashMap<int, int>.Create(comparer)),
                (() => (ComparerLinearHashMap<int, int, FibonacciFinalizer>)[with(comparer), entry], () => ComparerLinearHashMap<int, int, FibonacciFinalizer>.Create(comparer)),
            ],
            HashAlgorithmKind.Chained =>
            [
                (() => (ChainedHashMap<int, int>)[entry], ChainedHashMap<int, int>.Create),
                (() => (ChainedHashMap<int, int, FibonacciFinalizer>)[entry], ChainedHashMap<int, int, FibonacciFinalizer>.Create),
                (() => (ChainedHashMap<int, int, LastDigitHasher, FibonacciFinalizer>)[entry], ChainedHashMap<int, int, LastDigitHasher, FibonacciFinalizer>.Create),
                (() => (ComparerChainedHashMap<int, int>)[with(comparer), entry], () => ComparerChainedHashMap<int, int>.Create(comparer)),
                (() => (ComparerChainedHashMap<int, int, FibonacciFinalizer>)[with(comparer), entry], () => ComparerChainedHashMap<int, int, FibonacciFinalizer>.Create(comparer)),
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
                (typeof(SwissHashMap<int, int>), "ComparerSwissHashMap<TKey,TValue>"),
                (typeof(SwissHashMap<int, int, FibonacciFinalizer>), "ComparerSwissHashMap<TKey,TValue,TFinalizer>"),
            ],
            HashAlgorithmKind.RobinHood =>
            [
                (typeof(RobinHoodHashMap<int, int>), "ComparerRobinHoodHashMap<TKey,TValue>"),
                (typeof(RobinHoodHashMap<int, int, FibonacciFinalizer>), "ComparerRobinHoodHashMap<TKey,TValue,TFinalizer>"),
            ],
            HashAlgorithmKind.Linear =>
            [
                (typeof(LinearHashMap<int, int>), "ComparerLinearHashMap<TKey,TValue>"),
                (typeof(LinearHashMap<int, int, FibonacciFinalizer>), "ComparerLinearHashMap<TKey,TValue,TFinalizer>"),
            ],
            HashAlgorithmKind.Chained =>
            [
                (typeof(ChainedHashMap<int, int>), "ComparerChainedHashMap<TKey,TValue>"),
                (typeof(ChainedHashMap<int, int, FibonacciFinalizer>), "ComparerChainedHashMap<TKey,TValue,TFinalizer>"),
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
            await Assert.That(message).Contains("declare the map as");
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
        // aliases ([...] and [with(capacity), ...]). PooledHashMap<K,V> takes a comparer, so it has none.
        const int DefaultComparerAliasOverloads = 16;
        const int ComparerAliasOverloads = 16;
        const string ComparerHint = "use [with(comparer), ...]";
        Type[] comparerSignature = [typeof(IEqualityComparer<int>), typeof(int)];

        // Act
        var overloads = CollectionBuilderProbe.ProbeObsoleteOverloads(typeof(PooledHashMapCollectionBuilder), [typeof(int), typeof(int)]);
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
    static int FreshCapacity<TKey, TValue, TAlgo>(Func<PooledHashMap<TKey, TValue, TAlgo>> build)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
    {
        _ = build();
        using var map = build();
        return map.Capacity;
    }

    // Disposes a map built by a collection expression, takes it back with Create, disposes that,
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
