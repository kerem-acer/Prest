using System.Text.Json;

namespace Prest.Serializers.SystemTextJson.Tests;

public class ConverterRoundTripTests
{
    static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new PooledArrayConverter<int>());
        options.Converters.Add(new PooledArrayConverter<string>());
        options.Converters.Add(new PooledListConverter<int>());
        options.Converters.Add(new PooledListConverter<string>());
        options.Converters.Add(new PooledHashMapConverter<int, string, SwissTableAlgorithm<KeyValueSlot<int, string>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>());
        options.Converters.Add(new PooledHashMapConverter<Guid, int, SwissTableAlgorithm<KeyValueSlot<Guid, int>, Guid, EqualityDefaultHasher<Guid>, NoOpHashFinalizer>>());
        options.Converters.Add(new PooledHashSetConverter<int, SwissTableAlgorithm<int, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>());
        options.Converters.Add(new PooledHashSetConverter<string, SwissTableAlgorithm<string, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>>());
        return options;
    }

    [Test]
    public async Task PooledArrayInt_RoundTrip()
    {
        var options = Options();
        PooledArray<int> original = [1, 2, 3];

        var json = JsonSerializer.Serialize(original, options);
        await Assert.That(json).IsEqualTo("[1,2,3]");

        using var deserialized = JsonSerializer.Deserialize<PooledArray<int>>(json, options);
        await Assert.That(deserialized.Count).IsEqualTo(3);
        await Assert.That(deserialized.Span.ToArray()).IsEquivalentTo([1, 2, 3]);

        original.Dispose();
    }

    [Test]
    public async Task PooledArrayString_RoundTrip()
    {
        var options = Options();
        PooledArray<string> original = ["hello", "world"];

        var json = JsonSerializer.Serialize(original, options);

        using var deserialized = JsonSerializer.Deserialize<PooledArray<string>>(json, options);
        await Assert.That(deserialized.Count).IsEqualTo(2);
        await Assert.That(deserialized[0]).IsEqualTo("hello");
        await Assert.That(deserialized[1]).IsEqualTo("world");

        original.Dispose();
    }

    [Test]
    public async Task PooledArray_Empty_RoundTrip()
    {
        var options = Options();
        PooledArray<int> empty = [];

        var json = JsonSerializer.Serialize(empty, options);
        await Assert.That(json).IsEqualTo("[]");

        using var deserialized = JsonSerializer.Deserialize<PooledArray<int>>(json, options);
        await Assert.That(deserialized.IsEmpty).IsTrue();
    }

    [Test]
    public async Task PooledListInt_RoundTrip()
    {
        var options = Options();
        using var original = new PooledList<int>();
        original.Add(1);
        original.Add(2);
        original.Add(3);

        var json = JsonSerializer.Serialize(original, options);
        await Assert.That(json).IsEqualTo("[1,2,3]");

        using var deserialized = JsonSerializer.Deserialize<PooledList<int>>(json, options);
        await Assert.That(deserialized!.Count).IsEqualTo(3);
        await Assert.That(deserialized.Span.ToArray()).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task PooledListString_RoundTrip()
    {
        var options = Options();
        using var original = new PooledList<string>();
        original.Add("hello");
        original.Add("world");

        var json = JsonSerializer.Serialize(original, options);

        using var deserialized = JsonSerializer.Deserialize<PooledList<string>>(json, options);
        await Assert.That(deserialized!.Count).IsEqualTo(2);
        await Assert.That(deserialized[0]).IsEqualTo("hello");
        await Assert.That(deserialized[1]).IsEqualTo("world");
    }

    [Test]
    public async Task PooledList_Empty_RoundTrip()
    {
        var options = Options();
        using var empty = new PooledList<int>();

        var json = JsonSerializer.Serialize(empty, options);
        await Assert.That(json).IsEqualTo("[]");

        using var deserialized = JsonSerializer.Deserialize<PooledList<int>>(json, options);
        await Assert.That(deserialized!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PooledHashSetInt_RoundTrip()
    {
        var options = Options();
        using var original = PooledHashSet<int>.Create(4);
        original.Add(1);
        original.Add(2);
        original.Add(3);

        var json = JsonSerializer.Serialize(original, options);

        using var deserialized = JsonSerializer.Deserialize<PooledHashSet<int, SwissTableAlgorithm<int, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>>(json, options)!;
        await Assert.That(deserialized.Count).IsEqualTo(3);
        await Assert.That(deserialized.Contains(1)).IsTrue();
        await Assert.That(deserialized.Contains(2)).IsTrue();
        await Assert.That(deserialized.Contains(3)).IsTrue();
    }

    [Test]
    public async Task PooledHashSet_Empty_RoundTrip()
    {
        var options = Options();
        using var empty = PooledHashSet<int>.Create(0);

        var json = JsonSerializer.Serialize(empty, options);
        await Assert.That(json).IsEqualTo("[]");

        using var deserialized = JsonSerializer.Deserialize<PooledHashSet<int, SwissTableAlgorithm<int, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>>(json, options)!;
        await Assert.That(deserialized.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PooledMap_IntKey_StringValue_Serialize()
    {
        var options = Options();
        using var original = PooledHashMap<int, string, SwissTableAlgorithm<KeyValueSlot<int, string>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>.Create(4);
        original.Add(1, "one");
        original.Add(2, "two");

        var json = JsonSerializer.Serialize(original, options);
        await Assert.That(json).IsEqualTo("{\"1\":\"one\",\"2\":\"two\"}");
    }
}
