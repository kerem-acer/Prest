using System.Text.Json;

namespace Prest.Serializers.SystemTextJson.Tests;

public partial class ConverterFactoryTests
{
    [Test]
    public async Task Factory_AliasTypedProperties_RoundTripIntoTheAliasTypes()
    {
        // Arrange
        const string Key = "a";
        const int Value = 1;
        const int NumberKey = 7;
        const string NumberValue = "seven";
        const string Tag = "red";
        const int Number = 42;
        var options = Options();
        using var original = new AliasDto
        {
            Default = PooledHashMap<string, int>.Create(),
            Swiss = SwissHashMap<string, int>.Create(),
            RobinHood = RobinHoodHashMap<int, string, FibonacciFinalizer>.Create(),
            Chained = ChainedHashMap<int, int, EqualityDefaultHasher<int>, XmxFinalizer>.Create(),
            Tags = PooledHashSet<string>.Create(),
            Numbers = LinearHashSet<int, FibonacciFinalizer>.Create(),
        };
        original.Default.Add(Key, Value);
        original.Swiss.Add(Key, Value);
        original.RobinHood.Add(NumberKey, NumberValue);
        original.Chained.Add(NumberKey, Number);
        original.Tags.Add(Tag);
        original.Numbers.Add(Number);
        var json = JsonSerializer.Serialize(original, options);

        // Act
        using var deserialized = JsonSerializer.Deserialize<AliasDto>(json, options)!;

        // Assert
        await Assert.That(deserialized.Default).IsTypeOf<PooledHashMap<string, int>>();
        await Assert.That(deserialized.Default![Key]).IsEqualTo(Value);
        await Assert.That(deserialized.Swiss).IsTypeOf<SwissHashMap<string, int>>();
        await Assert.That(deserialized.Swiss![Key]).IsEqualTo(Value);
        await Assert.That(deserialized.RobinHood).IsTypeOf<RobinHoodHashMap<int, string, FibonacciFinalizer>>();
        await Assert.That(deserialized.RobinHood![NumberKey]).IsEqualTo(NumberValue);
        await Assert.That(deserialized.Chained).IsTypeOf<ChainedHashMap<int, int, EqualityDefaultHasher<int>, XmxFinalizer>>();
        await Assert.That(deserialized.Chained![NumberKey]).IsEqualTo(Number);
        await Assert.That(deserialized.Tags).IsTypeOf<PooledHashSet<string>>();
        await Assert.That(deserialized.Tags!.Contains(Tag)).IsTrue();
        await Assert.That(deserialized.Numbers).IsTypeOf<LinearHashSet<int, FibonacciFinalizer>>();
        await Assert.That(deserialized.Numbers!.Contains(Number)).IsTrue();
    }

    [Test]
    public async Task Factory_ComparerAliasProperty_SerializesButThrowsNotSupportedOnRead()
    {
        // Arrange — the payload carries no comparer, so the factory cannot rebuild the alias.
        const string Key = "Key";
        const int Value = 1;
        const string ExpectedJson = "{\"Map\":{\"Key\":1},\"Set\":[\"Key\"]}";
        var options = Options();
        using var original = new ComparerAliasDto
        {
            Map = ComparerSwissHashMap<string, int>.Create(StringComparer.OrdinalIgnoreCase),
            Set = ComparerRobinHoodHashSet<string>.Create(StringComparer.OrdinalIgnoreCase),
        };
        original.Map.Add(Key, Value);
        original.Set.Add(Key);

        // Act
        var json = JsonSerializer.Serialize(original, options);

        // Assert
        await Assert.That(json).IsEqualTo(ExpectedJson);
        await Assert.That(() => _ = JsonSerializer.Deserialize<ComparerAliasDto>(json, options))
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task Factory_ComparerAliasWithRegisteredFactoryConverters_RoundTripsWithComparer()
    {
        // Arrange — explicit alias converters registered ahead of the factory supply the comparer.
        const string Key = "Key";
        const string OtherCaseKey = "KEY";
        const int Value = 1;
        const string Json = "{\"Map\":{\"Key\":1},\"Set\":[\"Key\"]}";
        var comparer = StringComparer.OrdinalIgnoreCase;
        var options = OptionsWithComparerAliasConverters(comparer);

        // Act
        using var deserialized = JsonSerializer.Deserialize<ComparerAliasDto>(Json, options)!;

        // Assert
        await Assert.That(deserialized.Map!.Comparer).IsSameReferenceAs(comparer);
        await Assert.That(deserialized.Map[OtherCaseKey]).IsEqualTo(Value);
        await Assert.That(deserialized.Set!.Comparer).IsSameReferenceAs(comparer);
        await Assert.That(deserialized.Set.Contains(OtherCaseKey)).IsTrue();
        await Assert.That(deserialized.Map.ContainsKey(Key)).IsTrue();
    }

    static JsonSerializerOptions OptionsWithComparerAliasConverters(IEqualityComparer<string> comparer)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new PooledHashMapAliasConverter<
            ComparerSwissHashMap<string, int>, string, int,
            SwissTableAlgorithm<KeyValueSlot<string, int>, string, ComparerHasher<string>, NoOpHashFinalizer>>(
            capacity => ComparerSwissHashMap<string, int>.Create(comparer, capacity)));
        options.Converters.Add(new PooledHashSetAliasConverter<
            ComparerRobinHoodHashSet<string>, string,
            RobinHoodAlgorithm<string, string, ComparerHasher<string>, NoOpHashFinalizer>>(
            capacity => ComparerRobinHoodHashSet<string>.Create(comparer, capacity)));
        options.Converters.Add(new PooledTypesJsonConverterFactory());
        return options;
    }

    public sealed class AliasDto : IDisposable
    {
        public PooledHashMap<string, int>? Default { get; set; }
        public SwissHashMap<string, int>? Swiss { get; set; }
        public RobinHoodHashMap<int, string, FibonacciFinalizer>? RobinHood { get; set; }
        public ChainedHashMap<int, int, EqualityDefaultHasher<int>, XmxFinalizer>? Chained { get; set; }
        public PooledHashSet<string>? Tags { get; set; }
        public LinearHashSet<int, FibonacciFinalizer>? Numbers { get; set; }

        public void Dispose()
        {
            Default?.Dispose();
            Swiss?.Dispose();
            RobinHood?.Dispose();
            Chained?.Dispose();
            Tags?.Dispose();
            Numbers?.Dispose();
        }
    }

    public sealed class ComparerAliasDto : IDisposable
    {
        public ComparerSwissHashMap<string, int>? Map { get; set; }
        public ComparerRobinHoodHashSet<string>? Set { get; set; }

        public void Dispose()
        {
            Map?.Dispose();
            Set?.Dispose();
        }
    }
}
