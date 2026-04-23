using VYaml.Serialization;

namespace Prest.Serializers.VYaml.Tests;

public class FormatterRoundTripTests
{
    static YamlSerializerOptions Options()
    {
        var options = YamlSerializerOptions.Standard;
        options.Resolver = CompositeResolver.Create(
            (IYamlFormatterResolver[])
            [
                PooledTypeFormatterResolver.Instance,
                StandardResolver.Instance
            ]);
        return options;
    }

    [Test]
    public async Task PooledArrayInt_RoundTrip()
    {
        var options = Options();
        PooledArray<int> original = [1, 2, 3];

        var yaml = YamlSerializer.SerializeToString(original, options);

        using var deserialized = YamlSerializer.Deserialize<PooledArray<int>>(
            System.Text.Encoding.UTF8.GetBytes(yaml), options);
        await Assert.That(deserialized.Count).IsEqualTo(3);
        await Assert.That(deserialized.Span.ToArray()).IsEquivalentTo([1, 2, 3]);

        original.Dispose();
    }

    [Test]
    public async Task PooledArrayString_RoundTrip()
    {
        var options = Options();
        PooledArray<string> original = ["hello", "world"];

        var yaml = YamlSerializer.SerializeToString(original, options);

        using var deserialized = YamlSerializer.Deserialize<PooledArray<string>>(
            System.Text.Encoding.UTF8.GetBytes(yaml), options);
        await Assert.That(deserialized.Count).IsEqualTo(2);
        await Assert.That(deserialized[0]).IsEqualTo("hello");
        await Assert.That(deserialized[1]).IsEqualTo("world");

        original.Dispose();
    }

    [Test]
    public async Task PooledMap_StringInt_RoundTrip()
    {
        var options = Options();
        using var original = PooledHashMap<string, int, SwissTableAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>>.Create(4);
        original.Add("a", 10);
        original.Add("b", 20);

        var yaml = YamlSerializer.SerializeToString(original, options);

        using var deserialized = YamlSerializer.Deserialize<PooledHashMap<string, int, SwissTableAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>>>(
            System.Text.Encoding.UTF8.GetBytes(yaml), options);
        await Assert.That(deserialized.Count).IsEqualTo(2);
        await Assert.That(deserialized["a"]).IsEqualTo(10);
        await Assert.That(deserialized["b"]).IsEqualTo(20);
    }

    [Test]
    public async Task PooledList_RoundTrip()
    {
        var options = Options();
        using var original = new PooledList<int>();
        original.Add(1);
        original.Add(2);
        original.Add(3);

        var yaml = YamlSerializer.SerializeToString(original, options);

        using var deserialized = YamlSerializer.Deserialize<PooledList<int>>(
            System.Text.Encoding.UTF8.GetBytes(yaml), options);
        await Assert.That(deserialized!.Count).IsEqualTo(3);
        await Assert.That(deserialized.Span.ToArray()).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task PooledHashSet_RoundTrip()
    {
        var options = Options();
        using var original = PooledHashSet<int>.Create(4);
        original.Add(1);
        original.Add(2);
        original.Add(3);

        var yaml = YamlSerializer.SerializeToString(original, options);

        using var deserialized = YamlSerializer.Deserialize<PooledHashSet<int, SwissTableAlgorithm<int, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>>(
            System.Text.Encoding.UTF8.GetBytes(yaml), options);
        await Assert.That(deserialized.Count).IsEqualTo(3);
        await Assert.That(deserialized.Contains(1)).IsTrue();
        await Assert.That(deserialized.Contains(2)).IsTrue();
        await Assert.That(deserialized.Contains(3)).IsTrue();
    }

    [Test]
    public async Task Resolver_FormatterForUnrelatedType_ReturnsNull()
    {
        var resolver = PooledTypeFormatterResolver.Instance;
        await Assert.That(resolver.GetFormatter<int>()).IsNull();
        await Assert.That(resolver.GetFormatter<List<int>>()).IsNull();
    }
}
