using System.Text.Json;

namespace Prest.Serializers.SystemTextJson.Tests;

/// <summary>
/// Exercises the non-happy-path branches of <see cref="PooledHashMapConverter{TKey,TValue,TAlgo}" />
/// that the existing RoundTrip tests don't cover.
/// </summary>
public class ConverterDeserializationTests
{
    static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new PooledHashMapConverter<int, string, SwissTableAlgorithm<KeyValueSlot<int, string>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>());
        options.Converters.Add(new PooledHashMapConverter<int, int, SwissTableAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>());
        return options;
    }

    [Test]
    public async Task PooledHashMap_Serialize_EmptyMap_ProducesEmptyObject()
    {
        // Arrange
        var options = Options();
        using var empty = PooledHashMap<int, string, SwissTableAlgorithm<KeyValueSlot<int, string>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>.Create(0);

        // Act
        var json = JsonSerializer.Serialize(empty, options);

        // Assert
        await Assert.That(json).IsEqualTo("{}");
    }

    [Test]
    public async Task PooledHashMap_Serialize_LargeMap_ContainsAllKeys()
    {
        // Arrange — force the Write loop to emit many entries.
        const int n = 25;
        var options = Options();
        using var original = PooledHashMap<int, int, SwissTableAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>.Create(n);
        for (var i = 0; i < n; i++)
        {
            original.Add(i, i * 3);
        }

        // Act
        var json = JsonSerializer.Serialize(original, options);

        // Assert — every key should be present in the output.
        for (var i = 0; i < n; i++)
        {
            await Assert.That(json.Contains($"\"{i}\":")).IsTrue();
        }
    }
}
