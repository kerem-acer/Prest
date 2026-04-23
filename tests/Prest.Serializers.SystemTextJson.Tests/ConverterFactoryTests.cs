using System.Text.Json;

namespace Prest.Serializers.SystemTextJson.Tests;

/// <summary>
/// Exercises <see cref="PooledTypesJsonConverterFactory" /> — a single registration
/// should cover every pooled generic type with any algorithm closure.
/// </summary>
public class ConverterFactoryTests
{
    static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new PooledTypesJsonConverterFactory());
        return options;
    }

    [Test]
    public async Task Factory_PooledArray_RoundTrip()
    {
        var options = Options();
        PooledArray<int> original = [1, 2, 3];

        var json = JsonSerializer.Serialize(original, options);
        await Assert.That(json).IsEqualTo("[1,2,3]");

        using var deserialized = JsonSerializer.Deserialize<PooledArray<int>>(json, options);
        await Assert.That(deserialized.Count).IsEqualTo(3);

        original.Dispose();
    }

    [Test]
    public async Task Factory_PooledList_RoundTrip()
    {
        var options = Options();
        using var original = new PooledList<int>();
        original.Add(1);
        original.Add(2);

        var json = JsonSerializer.Serialize(original, options);

        using var deserialized = JsonSerializer.Deserialize<PooledList<int>>(json, options);
        await Assert.That(deserialized!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Factory_PooledHashSet_RoundTripWithSwissAlgorithm()
    {
        var options = Options();
        using var original = PooledHashSet<int>.Create(4);
        original.Add(1);
        original.Add(2);

        var json = JsonSerializer.Serialize(original, options);

        using var deserialized = JsonSerializer.Deserialize<PooledHashSet<int, SwissTableAlgorithm<int, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>>(json, options)!;
        await Assert.That(deserialized.Count).IsEqualTo(2);
        await Assert.That(deserialized.Contains(1)).IsTrue();
        await Assert.That(deserialized.Contains(2)).IsTrue();
    }

    [Test]
    public async Task Factory_PooledHashMap_RoundTripWithSwissAlgorithm()
    {
        var options = Options();
        using var original = PooledHashMap<int, string>.Create(4);
        original.Add(1, "one");
        original.Add(2, "two");

        var json = JsonSerializer.Serialize(original, options);

        using var deserialized = JsonSerializer.Deserialize<PooledHashMap<int, string, SwissTableAlgorithm<KeyValueSlot<int, string>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>>(json, options)!;
        await Assert.That(deserialized.Count).IsEqualTo(2);
        await Assert.That(deserialized[1]).IsEqualTo("one");
    }

    [Test]
    public async Task Factory_CanConvert_RejectsUnrelatedTypes()
    {
        var factory = new PooledTypesJsonConverterFactory();
        await Assert.That(factory.CanConvert(typeof(int))).IsFalse();
        await Assert.That(factory.CanConvert(typeof(List<int>))).IsFalse();
        await Assert.That(factory.CanConvert(typeof(Dictionary<int, string>))).IsFalse();
    }

    [Test]
    public async Task Factory_CanConvert_AcceptsAllPooledTypes()
    {
        var factory = new PooledTypesJsonConverterFactory();
        await Assert.That(factory.CanConvert(typeof(PooledArray<int>))).IsTrue();
        await Assert.That(factory.CanConvert(typeof(PooledList<int>))).IsTrue();
        await Assert.That(factory.CanConvert(typeof(PooledHashSet<int>))).IsTrue();
        await Assert.That(factory.CanConvert(typeof(PooledHashMap<int, string>))).IsTrue();
    }
}
