using VYaml.Serialization;

namespace Prest.Serializers.VYaml.Tests;

public class FormatterEdgeCaseTests
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
    public async Task PooledArray_NullScalar_DeserializesToDefault()
    {
        // Arrange
        var options = Options();
        var yamlBytes = System.Text.Encoding.UTF8.GetBytes("null");

        // Act
        using var deserialized = YamlSerializer.Deserialize<PooledArray<int>>(yamlBytes, options);

        // Assert
        await Assert.That(deserialized.IsEmpty).IsTrue();
    }

    [Test]
    public async Task PooledArray_EmptySequence_RoundTrip()
    {
        // Arrange
        var options = Options();
        PooledArray<int> empty = [];
        var yaml = YamlSerializer.SerializeToString(empty, options);

        // Act
        using var deserialized = YamlSerializer.Deserialize<PooledArray<int>>(
            System.Text.Encoding.UTF8.GetBytes(yaml), options);

        // Assert
        await Assert.That(deserialized.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PooledArray_LargeSequence_ExercisesGrow()
    {
        // Arrange — default rent is 8 slots; 50 entries force the Grow path.
        const int n = 50;
        var options = Options();
        var buffer = new int[n];
        for (var i = 0; i < n; i++)
        {
            buffer[i] = i * 2;
        }
        var original = PooledArray.Create<int>(buffer);
        var yaml = YamlSerializer.SerializeToString(original, options);

        // Act
        using var deserialized = YamlSerializer.Deserialize<PooledArray<int>>(
            System.Text.Encoding.UTF8.GetBytes(yaml), options);

        // Assert
        await Assert.That(deserialized.Count).IsEqualTo(n);
        for (var i = 0; i < n; i++)
        {
            await Assert.That(deserialized[i]).IsEqualTo(i * 2);
        }

        original.Dispose();
    }

    [Test]
    public async Task PooledHashMap_NullScalar_DeserializesToEmpty()
    {
        // Arrange
        var options = Options();
        var yamlBytes = System.Text.Encoding.UTF8.GetBytes("null");

        // Act
        using var deserialized = YamlSerializer.Deserialize<PooledHashMap<string, int, SwissTableAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>>>(
            yamlBytes, options);

        // Assert
        await Assert.That(deserialized.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PooledHashMap_EmptyMapping_RoundTrip()
    {
        // Arrange
        var options = Options();
        using var empty = PooledHashMap<string, int, SwissTableAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>>.Create(0);
        var yaml = YamlSerializer.SerializeToString(empty, options);

        // Act
        using var deserialized = YamlSerializer.Deserialize<PooledHashMap<string, int, SwissTableAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>>>(
            System.Text.Encoding.UTF8.GetBytes(yaml), options);

        // Assert
        await Assert.That(deserialized.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PooledHashMap_LargeMap_ExercisesGrow()
    {
        // Arrange — 50 entries force the formatter's Grow path on deserialization.
        const int n = 50;
        var options = Options();
        using var original = PooledHashMap<string, int, SwissTableAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>>.Create(n);
        for (var i = 0; i < n; i++)
        {
            original.Add($"key{i}", i * 3);
        }
        var yaml = YamlSerializer.SerializeToString(original, options);

        // Act
        using var deserialized = YamlSerializer.Deserialize<PooledHashMap<string, int, SwissTableAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>>>(
            System.Text.Encoding.UTF8.GetBytes(yaml), options);

        // Assert
        await Assert.That(deserialized.Count).IsEqualTo(n);
        for (var i = 0; i < n; i++)
        {
            await Assert.That(deserialized[$"key{i}"]).IsEqualTo(i * 3);
        }
    }
}
