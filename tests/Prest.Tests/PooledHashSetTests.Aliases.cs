using TUnit.Assertions.Enums;
using ChainedIntSet = Prest.PooledHashSet<int, Prest.ChainedAlgorithm<int, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>>;
using LinearIntSet = Prest.PooledHashSet<int, Prest.LinearProbingAlgorithm<int, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>>;
using RobinHoodIntSet = Prest.PooledHashSet<int, Prest.RobinHoodAlgorithm<int, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>>;
using SwissIntSet = Prest.PooledHashSet<int, Prest.SwissTableAlgorithm<int, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>>;

namespace Prest.Tests;

public partial class PooledHashSetTests
{
    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Create_EveryAliasShape_ReturnsInstanceOfThatAlias(HashAlgorithmKind algorithm)
    {
        // Arrange
        Type[] expected = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
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
        var comparer = EqualityComparer<int>.Default;
        IDisposable[] sets = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                SwissHashSet<int>.Create(),
                SwissHashSet<int, FibonacciFinalizer>.Create(),
                SwissHashSet<int, LastDigitHasher, FibonacciFinalizer>.Create(),
                ComparerSwissHashSet<int>.Create(comparer),
                ComparerSwissHashSet<int, FibonacciFinalizer>.Create(comparer),
            ],
            HashAlgorithmKind.RobinHood =>
            [
                RobinHoodHashSet<int>.Create(),
                RobinHoodHashSet<int, FibonacciFinalizer>.Create(),
                RobinHoodHashSet<int, LastDigitHasher, FibonacciFinalizer>.Create(),
                ComparerRobinHoodHashSet<int>.Create(comparer),
                ComparerRobinHoodHashSet<int, FibonacciFinalizer>.Create(comparer),
            ],
            HashAlgorithmKind.Linear =>
            [
                LinearHashSet<int>.Create(),
                LinearHashSet<int, FibonacciFinalizer>.Create(),
                LinearHashSet<int, LastDigitHasher, FibonacciFinalizer>.Create(),
                ComparerLinearHashSet<int>.Create(comparer),
                ComparerLinearHashSet<int, FibonacciFinalizer>.Create(comparer),
            ],
            HashAlgorithmKind.Chained =>
            [
                ChainedHashSet<int>.Create(),
                ChainedHashSet<int, FibonacciFinalizer>.Create(),
                ChainedHashSet<int, LastDigitHasher, FibonacciFinalizer>.Create(),
                ComparerChainedHashSet<int>.Create(comparer),
                ComparerChainedHashSet<int, FibonacciFinalizer>.Create(comparer),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        var actual = sets.Select(set => set.GetType()).ToArray();
        DisposeAll(sets);

        // Assert
        await Assert.That(actual).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Create_Aliases_AssignToFieldsOfTheAliasType()
    {
        // Arrange
        const string Item = "apple";
        const string OtherCaseItem = "APPLE";
        const int Number = 7;
        using var fields = new AliasFields();

        // Act
        fields.Default.Add(Item);
        fields.Swiss.Add(Item);
        fields.RobinHood.Add(Item);
        fields.IgnoreCase.Add(Item);
        fields.LastDigit.Add(Number);
        var swissSeenAsDefault = CountThroughDefaultAlias(fields.Swiss);

        // Assert
        await Assert.That(fields.Default.Contains(Item)).IsTrue();
        await Assert.That(swissSeenAsDefault).IsEqualTo(1);
        await Assert.That(fields.RobinHood.Contains(Item)).IsTrue();
        await Assert.That(fields.IgnoreCase.Contains(OtherCaseItem)).IsTrue();
        await Assert.That(fields.LastDigit.Contains(Number + 10)).IsTrue();
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task Dispose_TypesSharingOneConfiguration_EachReuseOnlyTheirOwnInstance(HashAlgorithmKind algorithm)
    {
        // Arrange — every type closes over the same generic base (same algorithm, hasher and
        // finalizer). A shared cache slot would hand a disposed instance to the wrong type.
        Func<IDisposable>[] creators = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                SwissIntSet.Create,
                PooledHashSet<int>.Create,
                SwissHashSet<int>.Create,
                SwissHashSet<int, NoOpHashFinalizer>.Create,
                SwissHashSet<int, EqualityDefaultHasher<int>, NoOpHashFinalizer>.Create,
            ],
            HashAlgorithmKind.RobinHood =>
            [
                RobinHoodIntSet.Create,
                RobinHoodHashSet<int>.Create,
                RobinHoodHashSet<int, NoOpHashFinalizer>.Create,
                RobinHoodHashSet<int, EqualityDefaultHasher<int>, NoOpHashFinalizer>.Create,
            ],
            HashAlgorithmKind.Linear =>
            [
                LinearIntSet.Create,
                LinearHashSet<int>.Create,
                LinearHashSet<int, NoOpHashFinalizer>.Create,
                LinearHashSet<int, EqualityDefaultHasher<int>, NoOpHashFinalizer>.Create,
            ],
            HashAlgorithmKind.Chained =>
            [
                ChainedIntSet.Create,
                ChainedHashSet<int>.Create,
                ChainedHashSet<int, NoOpHashFinalizer>.Create,
                ChainedHashSet<int, EqualityDefaultHasher<int>, NoOpHashFinalizer>.Create,
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Act
        var (first, second) = DisposeAllThenCreateAgain(creators);

        // Assert
        await Assert.That(second.Length).IsEqualTo(first.Length);
        for (var i = 0; i < first.Length; i++)
        {
            await Assert.That(second[i]).IsSameReferenceAs(first[i]);
            await Assert.That(second[i].GetType()).IsEqualTo(first[i].GetType());
        }
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task HasherAlias_ItemsEqualUnderHasher_AreTreatedAsDuplicates(HashAlgorithmKind algorithm)
    {
        // Arrange — LastDigitHasher treats 1, 11 and 21 as the same item.
        const int Item = 1;
        const int SameDigitItem = 11;
        const int OtherSameDigitItem = 21;

        // Act
        var (addedSameDigit, containsOther, count) = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddSameLastDigit(
                SwissHashSet<int, LastDigitHasher, NoOpHashFinalizer>.Create(), Item, SameDigitItem, OtherSameDigitItem),
            HashAlgorithmKind.RobinHood => AddSameLastDigit(
                RobinHoodHashSet<int, LastDigitHasher, NoOpHashFinalizer>.Create(), Item, SameDigitItem, OtherSameDigitItem),
            HashAlgorithmKind.Linear => AddSameLastDigit(
                LinearHashSet<int, LastDigitHasher, NoOpHashFinalizer>.Create(), Item, SameDigitItem, OtherSameDigitItem),
            HashAlgorithmKind.Chained => AddSameLastDigit(
                ChainedHashSet<int, LastDigitHasher, NoOpHashFinalizer>.Create(), Item, SameDigitItem, OtherSameDigitItem),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(addedSameDigit).IsFalse();
        await Assert.That(containsOther).IsTrue();
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task HasherAlias_WithComparerHasher_ThrowsNotSupported(HashAlgorithmKind algorithm)
    {
        // Arrange — default(ComparerHasher<T>) has no comparer, so the alias rejects it up front.
        Func<object> create = algorithm switch
        {
            HashAlgorithmKind.Swiss => SwissHashSet<string, ComparerHasher<string>, NoOpHashFinalizer>.Create,
            HashAlgorithmKind.RobinHood => RobinHoodHashSet<string, ComparerHasher<string>, NoOpHashFinalizer>.Create,
            HashAlgorithmKind.Linear => LinearHashSet<string, ComparerHasher<string>, NoOpHashFinalizer>.Create,
            HashAlgorithmKind.Chained => ChainedHashSet<string, ComparerHasher<string>, NoOpHashFinalizer>.Create,
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Act & Assert
        await Assert.That(() => _ = create()).ThrowsExactly<NotSupportedException>();
    }

    static void DisposeAll(IEnumerable<IDisposable> disposables)
    {
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
    }

    // Consumes whatever another test left in each cache, creates one instance per creator,
    // disposes all of them, then creates again — the second round should get the first back.
    static (IDisposable[] First, IDisposable[] Second) DisposeAllThenCreateAgain(Func<IDisposable>[] creators)
    {
        foreach (var create in creators)
        {
            _ = create();
        }
        var first = creators.Select(create => create()).ToArray();
        DisposeAll(first);
        var second = creators.Select(create => create()).ToArray();
        return (first, second);
    }

    static (bool AddedSameDigit, bool ContainsOther, int Count) AddSameLastDigit<TAlgo>(
        PooledHashSet<int, TAlgo> set, int item, int sameDigitItem, int otherSameDigitItem)
        where TAlgo : struct, IHashAlgorithm<int, int>
    {
        using (set)
        {
            set.Add(item);
            var added = set.Add(sameDigitItem);
            return (added, set.Contains(otherSameDigitItem), set.Count);
        }
    }

    static int CountThroughDefaultAlias(PooledHashSet<string> set) => set.Count;

    // Fields declared with the short alias types, initialized straight from their Create.
    sealed class AliasFields : IDisposable
    {
        public readonly PooledHashSet<string> Default = PooledHashSet<string>.Create(16);
        public readonly SwissHashSet<string> Swiss = SwissHashSet<string>.Create(16);
        public readonly RobinHoodHashSet<string, FibonacciFinalizer> RobinHood =
            RobinHoodHashSet<string, FibonacciFinalizer>.Create(16);
        public readonly ComparerLinearHashSet<string> IgnoreCase =
            ComparerLinearHashSet<string>.Create(StringComparer.OrdinalIgnoreCase, 16);
        public readonly ChainedHashSet<int, LastDigitHasher, NoOpHashFinalizer> LastDigit =
            ChainedHashSet<int, LastDigitHasher, NoOpHashFinalizer>.Create(16);

        public void Dispose()
        {
            Default.Dispose();
            Swiss.Dispose();
            RobinHood.Dispose();
            IgnoreCase.Dispose();
            LastDigit.Dispose();
        }
    }
}
