using System.Text;
using Prest.YamlTestModels;
using VYaml.Serialization;

namespace Prest.Serializers.VYaml.Tests;

public partial class FormatterRoundTripTests
{
    [Test]
    public async Task Resolver_AliasTypedProperties_RoundTripIntoTheAliasTypes()
    {
        // Arrange
        const string Key = "a";
        const int Value = 1;
        const int NumberKey = 7;
        const string Tag = "red";
        const int Number = 42;
        var options = Options();
        using var original = new AliasDocument
        {
            Default = PooledHashMap<string, int>.Create(),
            Swiss = SwissHashMap<string, int>.Create(),
            RobinHood = RobinHoodHashMap<string, int, FibonacciFinalizer>.Create(),
            Chained = ChainedHashMap<int, int, EqualityDefaultHasher<int>, XmxFinalizer>.Create(),
            Tags = PooledHashSet<string>.Create(),
            Numbers = LinearHashSet<int, FibonacciFinalizer>.Create(),
        };
        original.Default.Add(Key, Value);
        original.Swiss.Add(Key, Value);
        original.RobinHood.Add(Key, Value);
        original.Chained.Add(NumberKey, Number);
        original.Tags.Add(Tag);
        original.Numbers.Add(Number);
        var yaml = YamlSerializer.Serialize(original, options);

        // Act
        using var deserialized = YamlSerializer.Deserialize<AliasDocument>(yaml, options);

        // Assert
        await Assert.That(deserialized.Default).IsTypeOf<PooledHashMap<string, int>>();
        await Assert.That(deserialized.Default![Key]).IsEqualTo(Value);
        await Assert.That(deserialized.Swiss).IsTypeOf<SwissHashMap<string, int>>();
        await Assert.That(deserialized.Swiss![Key]).IsEqualTo(Value);
        await Assert.That(deserialized.RobinHood).IsTypeOf<RobinHoodHashMap<string, int, FibonacciFinalizer>>();
        await Assert.That(deserialized.RobinHood![Key]).IsEqualTo(Value);
        await Assert.That(deserialized.Chained).IsTypeOf<ChainedHashMap<int, int, EqualityDefaultHasher<int>, XmxFinalizer>>();
        await Assert.That(deserialized.Chained![NumberKey]).IsEqualTo(Number);
        await Assert.That(deserialized.Tags).IsTypeOf<PooledHashSet<string>>();
        await Assert.That(deserialized.Tags!.Contains(Tag)).IsTrue();
        await Assert.That(deserialized.Numbers).IsTypeOf<LinearHashSet<int, FibonacciFinalizer>>();
        await Assert.That(deserialized.Numbers!.Contains(Number)).IsTrue();
    }

    [Test]
    public async Task Resolver_AliasAsRootValue_RoundTripsIntoAliasType()
    {
        // Arrange
        const string Key = "b";
        const int Value = 2;
        var options = Options();
        using var original = SwissHashMap<string, int>.Create();
        original.Add(Key, Value);
        var yaml = YamlSerializer.SerializeToString(original, options);

        // Act
        using var deserialized = YamlSerializer.Deserialize<SwissHashMap<string, int>>(Encoding.UTF8.GetBytes(yaml), options);

        // Assert
        await Assert.That(deserialized.GetType()).IsEqualTo(typeof(SwissHashMap<string, int>));
        await Assert.That(deserialized[Key]).IsEqualTo(Value);
    }

    [Test]
    public async Task Resolver_NullAliasProperties_SerializeAsNullAndReadBackEmpty()
    {
        // Arrange — matches the generic formatters: a null scalar reads back as an empty instance.
        var options = Options();
        using var original = new AliasDocument();

        // Act
        var yaml = YamlSerializer.SerializeToString(original, options);
        using var deserialized = YamlSerializer.Deserialize<AliasDocument>(Encoding.UTF8.GetBytes(yaml), options);

        // Assert
        await Assert.That(yaml.Contains("default: null")).IsTrue();
        await Assert.That(deserialized.Default).IsTypeOf<PooledHashMap<string, int>>();
        await Assert.That(deserialized.Default!.Count).IsEqualTo(0);
        await Assert.That(deserialized.Tags).IsTypeOf<PooledHashSet<string>>();
        await Assert.That(deserialized.Tags!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Resolver_ComparerAliasProperty_SerializesButThrowsNotSupportedOnRead()
    {
        // Arrange — the payload carries no comparer, so the resolver cannot rebuild the alias.
        const string Key = "Key";
        const int Value = 1;
        var options = Options();
        using var original = new ComparerAliasDocument
        {
            Map = ComparerSwissHashMap<string, int>.Create(StringComparer.OrdinalIgnoreCase),
            Set = ComparerRobinHoodHashSet<string>.Create(StringComparer.OrdinalIgnoreCase),
        };
        original.Map.Add(Key, Value);
        original.Set.Add(Key);

        // Act
        var yaml = YamlSerializer.Serialize(original, options);

        // Assert
        await Assert.That(yaml.Length).IsGreaterThan(0);
        await Assert.That(() => _ = YamlSerializer.Deserialize<ComparerAliasDocument>(yaml, options))
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task AliasFormatters_WithComparerFactory_RoundTripWithComparer()
    {
        // Arrange — alias formatters built with a factory supply the comparer the payload lacks.
        const string Key = "Key";
        const string OtherCaseKey = "KEY";
        const int Value = 1;
        var comparer = StringComparer.OrdinalIgnoreCase;
        var options = YamlSerializerOptions.Standard;
        options.Resolver = CompositeResolver.Create(
            [
                new PooledHashMapAliasFormatter<
                    ComparerSwissHashMap<string, int>, string, int,
                    SwissTableAlgorithm<KeyValueSlot<string, int>, string, ComparerHasher<string>, NoOpHashFinalizer>>(
                    capacity => ComparerSwissHashMap<string, int>.Create(comparer, capacity)),
                new PooledHashSetAliasFormatter<
                    ComparerRobinHoodHashSet<string>, string,
                    RobinHoodAlgorithm<string, string, ComparerHasher<string>, NoOpHashFinalizer>>(
                    capacity => ComparerRobinHoodHashSet<string>.Create(comparer, capacity)),
            ],
            [PooledTypeFormatterResolver.Instance, StandardResolver.Instance]);
        using var original = new ComparerAliasDocument
        {
            Map = ComparerSwissHashMap<string, int>.Create(comparer),
            Set = ComparerRobinHoodHashSet<string>.Create(comparer),
        };
        original.Map.Add(Key, Value);
        original.Set.Add(Key);
        var yaml = YamlSerializer.Serialize(original, options);

        // Act
        using var deserialized = YamlSerializer.Deserialize<ComparerAliasDocument>(yaml, options);

        // Assert
        await Assert.That(deserialized.Map!.Comparer).IsSameReferenceAs(comparer);
        await Assert.That(deserialized.Map[OtherCaseKey]).IsEqualTo(Value);
        await Assert.That(deserialized.Set!.Comparer).IsSameReferenceAs(comparer);
        await Assert.That(deserialized.Set.Contains(OtherCaseKey)).IsTrue();
    }
}
