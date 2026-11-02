using System.ComponentModel;
using System.Reflection;

namespace Prest.Tests;

public partial class PooledHashSetTests
{
    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Create_ComparerAliasWithoutComparer_IsCompileErrorAndThrowsNotSupported(HashAlgorithmKind algorithm)
    {
        // Arrange — the inherited comparer-less Create overloads used to build a set whose
        // ComparerHasher had no comparer, so the first Add threw NullReferenceException.
        Type[] aliases = algorithm switch
        {
            HashAlgorithmKind.Swiss => [typeof(ComparerSwissHashSet<string>), typeof(ComparerSwissHashSet<string, FibonacciFinalizer>)],
            HashAlgorithmKind.RobinHood => [typeof(ComparerRobinHoodHashSet<string>), typeof(ComparerRobinHoodHashSet<string, FibonacciFinalizer>)],
            HashAlgorithmKind.Linear => [typeof(ComparerLinearHashSet<string>), typeof(ComparerLinearHashSet<string, FibonacciFinalizer>)],
            HashAlgorithmKind.Chained => [typeof(ComparerChainedHashSet<string>), typeof(ComparerChainedHashSet<string, FibonacciFinalizer>)],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        Type[][] comparerlessSignatures = [[], [typeof(int)]];

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
        const string Item = "Hello";
        const string OtherCaseItem = "HELLO";
        var comparer = StringComparer.OrdinalIgnoreCase;

        // Act
        using var plain = CreateComparerSet(algorithm, withFinalizer: false, comparer);
        using var finalized = CreateComparerSet(algorithm, withFinalizer: true, comparer);
        plain.Add(Item);
        finalized.Add(Item);

        // Assert
        await Assert.That(plain.Comparer).IsSameReferenceAs(comparer);
        await Assert.That(finalized.Comparer).IsSameReferenceAs(comparer);
        await Assert.That(plain.Contains(OtherCaseItem)).IsTrue();
        await Assert.That(finalized.Contains(OtherCaseItem)).IsTrue();
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Create_ComparerAliasWithNullComparer_UsesDefaultComparer(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string Item = "Hello";
        const string OtherCaseItem = "HELLO";

        // Act
        using var plain = CreateComparerSet(algorithm, withFinalizer: false, comparer: null);
        using var finalized = CreateComparerSet(algorithm, withFinalizer: true, comparer: null);
        plain.Add(Item);
        finalized.Add(Item);

        // Assert
        await Assert.That(plain.Comparer).IsSameReferenceAs(EqualityComparer<string>.Default);
        await Assert.That(finalized.Comparer).IsSameReferenceAs(EqualityComparer<string>.Default);
        await Assert.That(plain.Contains(Item)).IsTrue();
        await Assert.That(plain.Contains(OtherCaseItem)).IsFalse();
        await Assert.That(finalized.Contains(OtherCaseItem)).IsFalse();
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Create_ComparerAliasWithSameComparerAfterDispose_ReusesInstance(HashAlgorithmKind algorithm)
    {
        // Arrange
        const string Item = "Item";
        var comparer = StringComparer.OrdinalIgnoreCase;

        // Act
        var (plainFirst, plainSecond) = DisposeThenCreateComparerSet(algorithm, withFinalizer: false, comparer, comparer, Item);
        var (finalizedFirst, finalizedSecond) = DisposeThenCreateComparerSet(algorithm, withFinalizer: true, comparer, comparer, Item);

        // Assert
        await Assert.That(plainSecond.Set).IsSameReferenceAs(plainFirst.Set);
        await Assert.That(plainSecond.Set.Count).IsEqualTo(0);
        await Assert.That(finalizedSecond.Set).IsSameReferenceAs(finalizedFirst.Set);
        await Assert.That(finalizedSecond.Set.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Create_ComparerAliasWithOtherComparerAfterDispose_AllocatesWithRequestedComparer(HashAlgorithmKind algorithm)
    {
        // Arrange — a disposed OrdinalIgnoreCase set used to come back from the next Create,
        // still ignoring case.
        const string Item = "Item";
        const string OtherCaseItem = "ITEM";
        var disposedComparer = StringComparer.OrdinalIgnoreCase;
        var requestedComparer = StringComparer.Ordinal;

        // Act
        var (plainFirst, plainSecond) = DisposeThenCreateComparerSet(algorithm, withFinalizer: false, disposedComparer, requestedComparer, Item);
        var (finalizedFirst, finalizedSecond) = DisposeThenCreateComparerSet(algorithm, withFinalizer: true, disposedComparer, requestedComparer, Item);
        plainSecond.Add(Item);
        finalizedSecond.Add(Item);

        // Assert
        await Assert.That(plainSecond.Set).IsNotSameReferenceAs(plainFirst.Set);
        await Assert.That(plainSecond.Comparer).IsSameReferenceAs(requestedComparer);
        await Assert.That(plainSecond.Contains(OtherCaseItem)).IsFalse();
        await Assert.That(finalizedSecond.Set).IsNotSameReferenceAs(finalizedFirst.Set);
        await Assert.That(finalizedSecond.Comparer).IsSameReferenceAs(requestedComparer);
        await Assert.That(finalizedSecond.Contains(OtherCaseItem)).IsFalse();
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
            HashAlgorithmKind.Swiss => PooledHashSet<string, SwissTableAlgorithm<string, string, ComparerHasher<string>, NoOpHashFinalizer>>.Create,
            HashAlgorithmKind.RobinHood => PooledHashSet<string, RobinHoodAlgorithm<string, string, ComparerHasher<string>, NoOpHashFinalizer>>.Create,
            HashAlgorithmKind.Linear => PooledHashSet<string, LinearProbingAlgorithm<string, string, ComparerHasher<string>, NoOpHashFinalizer>>.Create,
            HashAlgorithmKind.Chained => PooledHashSet<string, ChainedAlgorithm<string, string, ComparerHasher<string>, NoOpHashFinalizer>>.Create,
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        var comparer = StringComparer.OrdinalIgnoreCase;
        _ = createBase();
        _ = CreateComparerSet(algorithm, withFinalizer: false, comparer);
        var alias = CreateComparerSet(algorithm, withFinalizer: false, comparer);

        // Act
        alias.Dispose();
        var fromBase = createBase();
        var fromAlias = CreateComparerSet(algorithm, withFinalizer: false, comparer);

        // Assert
        await Assert.That(fromBase).IsNotSameReferenceAs(alias.Set);
        await Assert.That(fromAlias.Set).IsSameReferenceAs(alias.Set);
    }

    // Consumes whatever another test left in the alias's cache, creates a set with
    // disposedComparer, adds one item, disposes it, then creates again with requestedComparer.
    static (ComparerSet First, ComparerSet Second) DisposeThenCreateComparerSet(
        HashAlgorithmKind algorithm,
        bool withFinalizer,
        IEqualityComparer<string> disposedComparer,
        IEqualityComparer<string> requestedComparer,
        string item)
    {
        _ = CreateComparerSet(algorithm, withFinalizer, disposedComparer);
        var first = CreateComparerSet(algorithm, withFinalizer, disposedComparer);
        first.Add(item);
        first.Dispose();
        var second = CreateComparerSet(algorithm, withFinalizer, requestedComparer);
        return (first, second);
    }

    static ComparerSet CreateComparerSet(HashAlgorithmKind algorithm, bool withFinalizer, IEqualityComparer<string>? comparer)
    {
        switch (algorithm, withFinalizer)
        {
            case (HashAlgorithmKind.Swiss, false):
            {
                var set = ComparerSwissHashSet<string>.Create(comparer);
                return new(set, set.Comparer, set.Add, set.Contains);
            }
            case (HashAlgorithmKind.Swiss, true):
            {
                var set = ComparerSwissHashSet<string, FibonacciFinalizer>.Create(comparer);
                return new(set, set.Comparer, set.Add, set.Contains);
            }
            case (HashAlgorithmKind.RobinHood, false):
            {
                var set = ComparerRobinHoodHashSet<string>.Create(comparer);
                return new(set, set.Comparer, set.Add, set.Contains);
            }
            case (HashAlgorithmKind.RobinHood, true):
            {
                var set = ComparerRobinHoodHashSet<string, FibonacciFinalizer>.Create(comparer);
                return new(set, set.Comparer, set.Add, set.Contains);
            }
            case (HashAlgorithmKind.Linear, false):
            {
                var set = ComparerLinearHashSet<string>.Create(comparer);
                return new(set, set.Comparer, set.Add, set.Contains);
            }
            case (HashAlgorithmKind.Linear, true):
            {
                var set = ComparerLinearHashSet<string, FibonacciFinalizer>.Create(comparer);
                return new(set, set.Comparer, set.Add, set.Contains);
            }
            case (HashAlgorithmKind.Chained, false):
            {
                var set = ComparerChainedHashSet<string>.Create(comparer);
                return new(set, set.Comparer, set.Add, set.Contains);
            }
            case (HashAlgorithmKind.Chained, true):
            {
                var set = ComparerChainedHashSet<string, FibonacciFinalizer>.Create(comparer);
                return new(set, set.Comparer, set.Add, set.Contains);
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

    // A Comparer* set viewed through the members every algorithm shares.
    readonly record struct ComparerSet(
        IReadOnlyCollection<string> Set,
        IEqualityComparer<string> Comparer,
        Func<string, bool> Add,
        Func<string, bool> Contains) : IDisposable
    {
        public void Dispose() => ((IDisposable)Set).Dispose();
    }
}
