using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Prest.Serializers.SystemTextJson;

public sealed class PooledHashSetConverter<T, TAlgo> : JsonConverter<PooledHashSet<T, TAlgo>>
    where T : notnull
    where TAlgo : struct, IHashAlgorithm<T, T>
{
    public override PooledHashSet<T, TAlgo> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null or JsonTokenType.None)
        {
            return PooledHashSet<T, TAlgo>.Create(0);
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray, got {reader.TokenType}");
        }

        var elementTypeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        var set = PooledHashSet<T, TAlgo>.Create(0);
        try
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                set.Add(JsonSerializer.Deserialize(ref reader, elementTypeInfo) ?? default!);
            }
            return set;
        }
        catch
        {
            set.Dispose();
            throw;
        }
    }

    public override void Write(
        Utf8JsonWriter writer, PooledHashSet<T, TAlgo> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        var elementTypeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        foreach (var item in value)
        {
            JsonSerializer.Serialize(writer, item, elementTypeInfo);
        }

        writer.WriteEndArray();
    }
}
