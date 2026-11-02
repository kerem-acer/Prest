using System.Buffers;
using TUnit.Assertions.Enums;
using ChainedIntMap = Prest.PooledHashMap<int, int, Prest.ChainedAlgorithm<Prest.KeyValueSlot<int, int>, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>>;
using LinearIntMap = Prest.PooledHashMap<int, int, Prest.LinearProbingAlgorithm<Prest.KeyValueSlot<int, int>, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>>;
using RobinHoodIntMap = Prest.PooledHashMap<int, int, Prest.RobinHoodAlgorithm<Prest.KeyValueSlot<int, int>, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>>;
using SwissIntMap = Prest.PooledHashMap<int, int, Prest.SwissTableAlgorithm<Prest.KeyValueSlot<int, int>, int, Prest.EqualityDefaultHasher<int>, Prest.NoOpHashFinalizer>>;

namespace Prest.Tests;

public partial class PooledHashMapTests
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
        var comparer = EqualityComparer<int>.Default;
        IDisposable[] maps = algorithm switch
        {
            HashAlgorithmKind.Swiss =>
            [
                SwissHashMap<int, int>.Create(),
                SwissHashMap<int, int, FibonacciFinalizer>.Create(),
                SwissHashMap<int, int, LastDigitHasher, FibonacciFinalizer>.Create(),
                ComparerSwissHashMap<int, int>.Create(comparer),
                ComparerSwissHashMap<int, int, FibonacciFinalizer>.Create(comparer),
            ],
            HashAlgorithmKind.RobinHood =>
            [
                RobinHoodHashMap<int, int>.Create(),
                RobinHoodHashMap<int, int, FibonacciFinalizer>.Create(),
                RobinHoodHashMap<int, int, LastDigitHasher, FibonacciFinalizer>.Create(),
                ComparerRobinHoodHashMap<int, int>.Create(comparer),
                ComparerRobinHoodHashMap<int, int, FibonacciFinalizer>.Create(comparer),
            ],
            HashAlgorithmKind.Linear =>
            [
                LinearHashMap<int, int>.Create(),
                LinearHashMap<int, int, FibonacciFinalizer>.Create(),
                LinearHashMap<int, int, LastDigitHasher, FibonacciFinalizer>.Create(),
                ComparerLinearHashMap<int, int>.Create(comparer),
                ComparerLinearHashMap<int, int, FibonacciFinalizer>.Create(comparer),
            ],
            HashAlgorithmKind.Chained =>
            [
                ChainedHashMap<int, int>.Create(),
                ChainedHashMap<int, int, FibonacciFinalizer>.Create(),
                ChainedHashMap<int, int, LastDigitHasher, FibonacciFinalizer>.Create(),
                ComparerChainedHashMap<int, int>.Create(comparer),
                ComparerChainedHashMap<int, int, FibonacciFinalizer>.Create(comparer),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        var actual = maps.Select(map => map.GetType()).ToArray();
        DisposeAll(maps);

        // Assert
        await Assert.That(actual).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Create_Aliases_AssignToFieldsOfTheAliasType()
    {
        // Arrange
        const string Key = "answer";
        const string OtherCaseKey = "ANSWER";
        const int Value = 42;
        using var fields = new AliasFields();

        // Act
        fields.Default.Add(Key, Value);
        fields.Swiss.Add(Key, Value);
        fields.RobinHood.Add(Key, Value);
        fields.IgnoreCase.Add(Key, Value);
        fields.LastDigit.Add(Value, Value);
        var swissSeenAsDefault = CountThroughDefaultAlias(fields.Swiss);

        // Assert
        await Assert.That(fields.Default[Key]).IsEqualTo(Value);
        await Assert.That(swissSeenAsDefault).IsEqualTo(1);
        await Assert.That(fields.RobinHood[Key]).IsEqualTo(Value);
        await Assert.That(fields.IgnoreCase[OtherCaseKey]).IsEqualTo(Value);
        await Assert.That(fields.LastDigit.ContainsKey(Value + 10)).IsTrue();
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
                SwissIntMap.Create,
                PooledHashMap<int, int>.Create,
                SwissHashMap<int, int>.Create,
                SwissHashMap<int, int, NoOpHashFinalizer>.Create,
                SwissHashMap<int, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>.Create,
            ],
            HashAlgorithmKind.RobinHood =>
            [
                RobinHoodIntMap.Create,
                RobinHoodHashMap<int, int>.Create,
                RobinHoodHashMap<int, int, NoOpHashFinalizer>.Create,
                RobinHoodHashMap<int, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>.Create,
            ],
            HashAlgorithmKind.Linear =>
            [
                LinearIntMap.Create,
                LinearHashMap<int, int>.Create,
                LinearHashMap<int, int, NoOpHashFinalizer>.Create,
                LinearHashMap<int, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>.Create,
            ],
            HashAlgorithmKind.Chained =>
            [
                ChainedIntMap.Create,
                ChainedHashMap<int, int>.Create,
                ChainedHashMap<int, int, NoOpHashFinalizer>.Create,
                ChainedHashMap<int, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>.Create,
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
    public async Task Create_RentedArrays_ReturnsPopulatedAlias(HashAlgorithmKind algorithm)
    {
        // Arrange
        const int Count = 2;
        const int FirstKey = 1;
        const int SecondKey = 2;
        const int FirstValue = 10;
        const int SecondValue = 20;
        var keys = ArrayPool<int>.Shared.Rent(Count);
        var values = ArrayPool<int>.Shared.Rent(Count);
        keys[0] = FirstKey;
        keys[1] = SecondKey;
        values[0] = FirstValue;
        values[1] = SecondValue;
        var expectedType = algorithm switch
        {
            HashAlgorithmKind.Swiss => typeof(SwissHashMap<int, int>),
            HashAlgorithmKind.RobinHood => typeof(RobinHoodHashMap<int, int>),
            HashAlgorithmKind.Linear => typeof(LinearHashMap<int, int>),
            HashAlgorithmKind.Chained => typeof(ChainedHashMap<int, int>),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Act
        IReadOnlyDictionary<int, int> map = algorithm switch
        {
            HashAlgorithmKind.Swiss => SwissHashMap<int, int>.Create(keys, values, Count),
            HashAlgorithmKind.RobinHood => RobinHoodHashMap<int, int>.Create(keys, values, Count),
            HashAlgorithmKind.Linear => LinearHashMap<int, int>.Create(keys, values, Count),
            HashAlgorithmKind.Chained => ChainedHashMap<int, int>.Create(keys, values, Count),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        using var owner = (IDisposable)map;

        // Assert
        await Assert.That(map.GetType()).IsEqualTo(expectedType);
        await Assert.That(map.Count).IsEqualTo(Count);
        await Assert.That(map[FirstKey]).IsEqualTo(FirstValue);
        await Assert.That(map[SecondKey]).IsEqualTo(SecondValue);
    }

    [Test]
    public async Task Create_DefaultAliasFromRentedArrays_ReturnsDefaultAlias()
    {
        // Arrange
        const int Count = 1;
        const int Key = 3;
        const int Value = 30;
        var keys = ArrayPool<int>.Shared.Rent(Count);
        var values = ArrayPool<int>.Shared.Rent(Count);
        keys[0] = Key;
        values[0] = Value;

        // Act
        using PooledHashMap<int, int> map = PooledHashMap<int, int>.Create(keys, values, Count);

        // Assert
        await Assert.That(map.GetType()).IsEqualTo(typeof(PooledHashMap<int, int>));
        await Assert.That(map[Key]).IsEqualTo(Value);
    }

    [Test]
    [Arguments(HashAlgorithmKind.Swiss)]
    [Arguments(HashAlgorithmKind.RobinHood)]
    [Arguments(HashAlgorithmKind.Linear)]
    [Arguments(HashAlgorithmKind.Chained)]
    public async Task HasherAlias_KeysEqualUnderHasher_AreTreatedAsTheSameKey(HashAlgorithmKind algorithm)
    {
        // Arrange — LastDigitHasher treats 1, 11 and 21 as the same key.
        const int Key = 1;
        const int SameDigitKey = 11;
        const int OtherSameDigitKey = 21;
        const int Value = 100;
        const int OtherValue = 200;

        // Act
        var (addedSameDigit, lookedUp, count) = algorithm switch
        {
            HashAlgorithmKind.Swiss => AddSameLastDigit(
                SwissHashMap<int, int, LastDigitHasher, NoOpHashFinalizer>.Create(), Key, SameDigitKey, OtherSameDigitKey, Value, OtherValue),
            HashAlgorithmKind.RobinHood => AddSameLastDigit(
                RobinHoodHashMap<int, int, LastDigitHasher, NoOpHashFinalizer>.Create(), Key, SameDigitKey, OtherSameDigitKey, Value, OtherValue),
            HashAlgorithmKind.Linear => AddSameLastDigit(
                LinearHashMap<int, int, LastDigitHasher, NoOpHashFinalizer>.Create(), Key, SameDigitKey, OtherSameDigitKey, Value, OtherValue),
            HashAlgorithmKind.Chained => AddSameLastDigit(
                ChainedHashMap<int, int, LastDigitHasher, NoOpHashFinalizer>.Create(), Key, SameDigitKey, OtherSameDigitKey, Value, OtherValue),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        // Assert
        await Assert.That(addedSameDigit).IsFalse();
        await Assert.That(lookedUp).IsEqualTo(Value);
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
            HashAlgorithmKind.Swiss => SwissHashMap<string, int, ComparerHasher<string>, NoOpHashFinalizer>.Create,
            HashAlgorithmKind.RobinHood => RobinHoodHashMap<string, int, ComparerHasher<string>, NoOpHashFinalizer>.Create,
            HashAlgorithmKind.Linear => LinearHashMap<string, int, ComparerHasher<string>, NoOpHashFinalizer>.Create,
            HashAlgorithmKind.Chained => ChainedHashMap<string, int, ComparerHasher<string>, NoOpHashFinalizer>.Create,
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

    static (bool AddedSameDigit, int LookedUp, int Count) AddSameLastDigit<TAlgo>(
        PooledHashMap<int, int, TAlgo> map, int key, int sameDigitKey, int otherSameDigitKey, int value, int otherValue)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        using (map)
        {
            map.Add(key, value);
            var added = map.TryAdd(sameDigitKey, otherValue);
            return (added, map[otherSameDigitKey], map.Count);
        }
    }

    static int CountThroughDefaultAlias(PooledHashMap<string, int> map) => map.Count;

    // Fields declared with the short alias types, initialized straight from their Create.
    sealed class AliasFields : IDisposable
    {
        public readonly PooledHashMap<string, int> Default = PooledHashMap<string, int>.Create(16);
        public readonly SwissHashMap<string, int> Swiss = SwissHashMap<string, int>.Create(16);
        public readonly RobinHoodHashMap<string, int, FibonacciFinalizer> RobinHood =
            RobinHoodHashMap<string, int, FibonacciFinalizer>.Create(16);
        public readonly ComparerLinearHashMap<string, int> IgnoreCase =
            ComparerLinearHashMap<string, int>.Create(StringComparer.OrdinalIgnoreCase, 16);
        public readonly ChainedHashMap<int, int, LastDigitHasher, NoOpHashFinalizer> LastDigit =
            ChainedHashMap<int, int, LastDigitHasher, NoOpHashFinalizer>.Create(16);

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
