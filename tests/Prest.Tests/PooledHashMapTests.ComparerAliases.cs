using System.Buffers;
using System.ComponentModel;
using System.Reflection;

namespace Prest.Tests;

public partial class PooledHashMapTests
{
    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Create_ComparerAliasWithoutComparer_IsCompileErrorAndThrowsNotSupported(HashAlgorithmKind algorithm)
    {
        // Arrange — the inherited comparer-less Create overloads used to build a map whose
        // ComparerHasher had no comparer, so the first Add threw NullReferenceException.
        Type[] aliases = algorithm switch
        {
            HashAlgorithmKind.Swiss => [typeof(ComparerSwissHashMap<string, int>), typeof(ComparerSwissHashMap<string, int, FibonacciFinalizer>)],
            HashAlgorithmKind.RobinHood => [typeof(ComparerRobinHoodHashMap<string, int>), typeof(ComparerRobinHoodHashMap<string, int, FibonacciFinalizer>)],
            HashAlgorithmKind.Linear => [typeof(ComparerLinearHashMap<string, int>), typeof(ComparerLinearHashMap<string, int, FibonacciFinalizer>)],
            HashAlgorithmKind.Chained => [typeof(ComparerChainedHashMap<string, int>), typeof(ComparerChainedHashMap<string, int, FibonacciFinalizer>)],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        Type[][] comparerlessSignatures = [[], [typeof(int)], [typeof(string[]), typeof(int[]), typeof(int)]];

        // Act
        var probes = aliases
            .SelectMany(alias => comparerlessSignatures.Select(signature => ProbeHiddenCreate(alias, signature)))
            .ToArray();

        // Assert
        await Assert.That(probes.Length).IsEqualTo(aliases.Length * comparerlessSignatures.Length);
        foreach (var (isCompileError, isHiddenFromIntelliSense, thrown) in probes)
        {
            await Assert.That(isCompileError).IsTrue();
            await Assert.That(isHiddenFromIntelliSense).IsTrue();
            await Assert.That(thrown).IsEqualTo(typeof(NotSupportedException));
        }
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Create_ComparerAlias_ExposesAndUsesSuppliedComparer(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string Key = "Hello";
        const string OtherCaseKey = "HELLO";
        const int Value = 1;
        var comparer = StringComparer.OrdinalIgnoreCase;

        // Act
        using var plain = CreateComparerMap(algorithm, withFinalizer: false, comparer);
        using var finalized = CreateComparerMap(algorithm, withFinalizer: true, comparer);
        plain.Add(Key, Value);
        finalized.Add(Key, Value);

        // Assert
        await Assert.That(plain.Comparer).IsSameReferenceAs(comparer);
        await Assert.That(finalized.Comparer).IsSameReferenceAs(comparer);
        await Assert.That(plain.Map[OtherCaseKey]).IsEqualTo(Value);
        await Assert.That(finalized.Map[OtherCaseKey]).IsEqualTo(Value);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Create_ComparerAliasWithNullComparer_UsesDefaultComparer(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string Key = "Hello";
        const string OtherCaseKey = "HELLO";
        const int Value = 1;

        // Act
        using var plain = CreateComparerMap(algorithm, withFinalizer: false, comparer: null);
        using var finalized = CreateComparerMap(algorithm, withFinalizer: true, comparer: null);
        plain.Add(Key, Value);
        finalized.Add(Key, Value);

        // Assert
        await Assert.That(plain.Comparer).IsSameReferenceAs(EqualityComparer<string>.Default);
        await Assert.That(finalized.Comparer).IsSameReferenceAs(EqualityComparer<string>.Default);
        await Assert.That(plain.Map.ContainsKey(Key)).IsTrue();
        await Assert.That(plain.Map.ContainsKey(OtherCaseKey)).IsFalse();
        await Assert.That(finalized.Map.ContainsKey(OtherCaseKey)).IsFalse();
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Create_ComparerAliasWithSameComparerAfterDispose_ReusesInstance(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string Key = "Key";
        const int Value = 1;
        var comparer = StringComparer.OrdinalIgnoreCase;

        // Act
        var (plainFirst, plainSecond) = DisposeThenCreateComparerMap(algorithm, withFinalizer: false, comparer, comparer, Key, Value);
        var (finalizedFirst, finalizedSecond) = DisposeThenCreateComparerMap(algorithm, withFinalizer: true, comparer, comparer, Key, Value);

        // Assert
        await Assert.That(plainSecond.Map).IsSameReferenceAs(plainFirst.Map);
        await Assert.That(plainSecond.Map.Count).IsEqualTo(0);
        await Assert.That(finalizedSecond.Map).IsSameReferenceAs(finalizedFirst.Map);
        await Assert.That(finalizedSecond.Map.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Create_ComparerAliasWithOtherComparerAfterDispose_AllocatesWithRequestedComparer(HashAlgorithmKind algorithm)
    {
        // Arrange — a disposed OrdinalIgnoreCase map used to come back from the next Create,
        // still ignoring case.
        const string Key = "Key";
        const string OtherCaseKey = "KEY";
        const int Value = 1;
        var disposedComparer = StringComparer.OrdinalIgnoreCase;
        var requestedComparer = StringComparer.Ordinal;

        // Act
        var (plainFirst, plainSecond) = DisposeThenCreateComparerMap(algorithm, withFinalizer: false, disposedComparer, requestedComparer, Key, Value);
        var (finalizedFirst, finalizedSecond) = DisposeThenCreateComparerMap(algorithm, withFinalizer: true, disposedComparer, requestedComparer, Key, Value);
        plainSecond.Add(Key, Value);
        finalizedSecond.Add(Key, Value);

        // Assert
        await Assert.That(plainSecond.Map).IsNotSameReferenceAs(plainFirst.Map);
        await Assert.That(plainSecond.Comparer).IsSameReferenceAs(requestedComparer);
        await Assert.That(plainSecond.Map.ContainsKey(OtherCaseKey)).IsFalse();
        await Assert.That(finalizedSecond.Map).IsNotSameReferenceAs(finalizedFirst.Map);
        await Assert.That(finalizedSecond.Comparer).IsSameReferenceAs(requestedComparer);
        await Assert.That(finalizedSecond.Map.ContainsKey(OtherCaseKey)).IsFalse();
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Dispose_ComparerAlias_DoesNotReachGenericBaseCache(HashAlgorithmKind algorithm)
    {
        // Arrange — the generic base shares the alias's configuration but cannot know the comparer.
        Func<object> createBase = algorithm switch
        {
            HashAlgorithmKind.Swiss => PooledHashMap<string, int, SwissTableAlgorithm<KeyValueSlot<string, int>, string, ComparerHasher<string>, NoOpHashFinalizer>>.Create,
            HashAlgorithmKind.RobinHood => PooledHashMap<string, int, RobinHoodAlgorithm<KeyValueSlot<string, int>, string, ComparerHasher<string>, NoOpHashFinalizer>>.Create,
            HashAlgorithmKind.Linear => PooledHashMap<string, int, LinearProbingAlgorithm<KeyValueSlot<string, int>, string, ComparerHasher<string>, NoOpHashFinalizer>>.Create,
            HashAlgorithmKind.Chained => PooledHashMap<string, int, ChainedAlgorithm<KeyValueSlot<string, int>, string, ComparerHasher<string>, NoOpHashFinalizer>>.Create,
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        var comparer = StringComparer.OrdinalIgnoreCase;
        _ = createBase();
        _ = CreateComparerMap(algorithm, withFinalizer: false, comparer);
        var alias = CreateComparerMap(algorithm, withFinalizer: false, comparer);

        // Act
        alias.Dispose();
        var fromBase = createBase();
        var fromAlias = CreateComparerMap(algorithm, withFinalizer: false, comparer);

        // Assert
        await Assert.That(fromBase).IsNotSameReferenceAs(alias.Map);
        await Assert.That(fromAlias.Map).IsSameReferenceAs(alias.Map);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Create_ComparerAliasFromRentedArrays_UsesComparer(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int Count = 2;
        const string FirstKey = "a";
        const string SecondKey = "b";
        const string FirstKeyOtherCase = "A";
        const int FirstValue = 1;
        const int SecondValue = 2;
        var comparer = StringComparer.OrdinalIgnoreCase;
        var keys = ArrayPool<string>.Shared.Rent(Count);
        var values = ArrayPool<int>.Shared.Rent(Count);
        keys[0] = FirstKey;
        keys[1] = SecondKey;
        values[0] = FirstValue;
        values[1] = SecondValue;

        // Act
        using var created = CreateComparerMapFromRentedArrays(algorithm, comparer, keys, values, Count);

        // Assert
        await Assert.That(created.Comparer).IsSameReferenceAs(comparer);
        await Assert.That(created.Map.Count).IsEqualTo(Count);
        await Assert.That(created.Map[FirstKeyOtherCase]).IsEqualTo(FirstValue);
    }

    // Consumes whatever another test left in the alias's cache, creates a map with
    // disposedComparer, adds one entry, disposes it, then creates again with requestedComparer.
    static (ComparerMap First, ComparerMap Second) DisposeThenCreateComparerMap(
        HashAlgorithmKind algorithm,
        bool withFinalizer,
        IEqualityComparer<string> disposedComparer,
        IEqualityComparer<string> requestedComparer,
        string key,
        int value)
    {
        _ = CreateComparerMap(algorithm, withFinalizer, disposedComparer);
        var first = CreateComparerMap(algorithm, withFinalizer, disposedComparer);
        first.Add(key, value);
        first.Dispose();
        var second = CreateComparerMap(algorithm, withFinalizer, requestedComparer);
        return (first, second);
    }

    static ComparerMap CreateComparerMap(HashAlgorithmKind algorithm, bool withFinalizer, IEqualityComparer<string>? comparer)
    {
        switch (algorithm, withFinalizer)
        {
            case (HashAlgorithmKind.Swiss, false):
            {
                var map = ComparerSwissHashMap<string, int>.Create(comparer);
                return new(map, map.Comparer, map.Add);
            }
            case (HashAlgorithmKind.Swiss, true):
            {
                var map = ComparerSwissHashMap<string, int, FibonacciFinalizer>.Create(comparer);
                return new(map, map.Comparer, map.Add);
            }
            case (HashAlgorithmKind.RobinHood, false):
            {
                var map = ComparerRobinHoodHashMap<string, int>.Create(comparer);
                return new(map, map.Comparer, map.Add);
            }
            case (HashAlgorithmKind.RobinHood, true):
            {
                var map = ComparerRobinHoodHashMap<string, int, FibonacciFinalizer>.Create(comparer);
                return new(map, map.Comparer, map.Add);
            }
            case (HashAlgorithmKind.Linear, false):
            {
                var map = ComparerLinearHashMap<string, int>.Create(comparer);
                return new(map, map.Comparer, map.Add);
            }
            case (HashAlgorithmKind.Linear, true):
            {
                var map = ComparerLinearHashMap<string, int, FibonacciFinalizer>.Create(comparer);
                return new(map, map.Comparer, map.Add);
            }
            case (HashAlgorithmKind.Chained, false):
            {
                var map = ComparerChainedHashMap<string, int>.Create(comparer);
                return new(map, map.Comparer, map.Add);
            }
            case (HashAlgorithmKind.Chained, true):
            {
                var map = ComparerChainedHashMap<string, int, FibonacciFinalizer>.Create(comparer);
                return new(map, map.Comparer, map.Add);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(algorithm));
        }
    }

    static ComparerMap CreateComparerMapFromRentedArrays(
        HashAlgorithmKind algorithm, IEqualityComparer<string> comparer, string[] keys, int[] values, int count)
    {
        switch (algorithm)
        {
            case HashAlgorithmKind.Swiss:
            {
                var map = ComparerSwissHashMap<string, int>.Create(comparer, keys, values, count);
                return new(map, map.Comparer, map.Add);
            }
            case HashAlgorithmKind.RobinHood:
            {
                var map = ComparerRobinHoodHashMap<string, int>.Create(comparer, keys, values, count);
                return new(map, map.Comparer, map.Add);
            }
            case HashAlgorithmKind.Linear:
            {
                var map = ComparerLinearHashMap<string, int>.Create(comparer, keys, values, count);
                return new(map, map.Comparer, map.Add);
            }
            case HashAlgorithmKind.Chained:
            {
                var map = ComparerChainedHashMap<string, int>.Create(comparer, keys, values, count);
                return new(map, map.Comparer, map.Add);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(algorithm));
        }
    }

    // Reflects on a comparer-less Create overload that a Comparer* alias hides.
    static (bool IsCompileError, bool IsHiddenFromIntelliSense, Type? Thrown) ProbeHiddenCreate(Type alias, Type[] signature)
    {
        var method = alias.GetMethod("Create", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly, signature);
        if (method is null)
        {
            return (false, false, null);
        }

        Type? thrown = null;
        try
        {
            method.Invoke(null, signature.Select(type => type.IsValueType ? Activator.CreateInstance(type) : null).ToArray());
        }
        catch (TargetInvocationException e)
        {
            thrown = e.InnerException?.GetType();
        }

        return (
            method.GetCustomAttribute<ObsoleteAttribute>() is { IsError: true },
            method.GetCustomAttribute<EditorBrowsableAttribute>() is { State: EditorBrowsableState.Never },
            thrown);
    }

    // A Comparer* map viewed through the members every algorithm shares.
    readonly record struct ComparerMap(
        IReadOnlyDictionary<string, int> Map,
        IEqualityComparer<string> Comparer,
        Action<string, int> Add) : IDisposable
    {
        public void Dispose() => ((IDisposable)Map).Dispose();
    }
}
