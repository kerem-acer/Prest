using System.Text.Json;

namespace Prest.Serializers.SystemTextJson.Tests;

public partial class ConverterRoundTripTests
{
    [Test]
    public async Task AliasConverters_RegisteredWithoutFactory_RoundTripIntoAliasTypes()
    {
        // Arrange — the reflection-free registration Native AOT apps use.
        const string Key = "a";
        const int Value = 1;
        const string Item = "x";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new PooledHashMapAliasConverter<
            PooledHashMap<string, int>, string, int,
            SwissTableAlgorithm<KeyValueSlot<string, int>, string, OptionalComparerHasher<string>, NoOpHashFinalizer>>(
            PooledHashMap<string, int>.Create));
        options.Converters.Add(new PooledHashSetAliasConverter<
            SwissHashSet<string>, string,
            SwissTableAlgorithm<string, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>>(
            SwissHashSet<string>.Create));
        using var map = PooledHashMap<string, int>.Create();
        map.Add(Key, Value);
        using var set = SwissHashSet<string>.Create();
        set.Add(Item);
        var mapJson = JsonSerializer.Serialize(map, options);
        var setJson = JsonSerializer.Serialize(set, options);

        // Act
        using var mapBack = JsonSerializer.Deserialize<PooledHashMap<string, int>>(mapJson, options)!;
        using var setBack = JsonSerializer.Deserialize<SwissHashSet<string>>(setJson, options)!;

        // Assert
        await Assert.That(mapBack.GetType()).IsEqualTo(typeof(PooledHashMap<string, int>));
        await Assert.That(mapBack[Key]).IsEqualTo(Value);
        await Assert.That(setBack.GetType()).IsEqualTo(typeof(SwissHashSet<string>));
        await Assert.That(setBack.Contains(Item)).IsTrue();
    }

    [Test]
    public async Task PooledHashMapConverter_StringKeys_RoundTrip()
    {
        // Arrange — keys go through the key converter's property-name path, so string keys
        // (which are not ISpanFormattable) work.
        const string Key = "name";
        const int Value = 3;
        const string ExpectedJson = "{\"name\":3}";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new PooledHashMapConverter<string, int,
            SwissTableAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>>());
        using var original = PooledHashMap<string, int, SwissTableAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>>.Create();
        original.Add(Key, Value);

        // Act
        var json = JsonSerializer.Serialize(original, options);
        using var deserialized = JsonSerializer.Deserialize<PooledHashMap<string, int, SwissTableAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>>>(json, options)!;

        // Assert
        await Assert.That(json).IsEqualTo(ExpectedJson);
        await Assert.That(deserialized[Key]).IsEqualTo(Value);
    }

    [Test]
    public async Task PooledHashMapAliasConverter_NullFactory_ThrowsArgumentNull()
    {
        // Arrange
        Func<int, PooledHashMap<string, int>> create = null!;

        // Act & Assert
        await Assert.That(() => _ = new PooledHashMapAliasConverter<
            PooledHashMap<string, int>, string, int,
            SwissTableAlgorithm<KeyValueSlot<string, int>, string, OptionalComparerHasher<string>, NoOpHashFinalizer>>(create))
            .ThrowsExactly<ArgumentNullException>();
    }
}
