using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Prest.Serializers.SystemTextJson;

public sealed class PooledListConverter<T> : JsonConverter<PooledList<T>>
{
    public override PooledList<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null or JsonTokenType.None)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray, got {reader.TokenType}");
        }

        var elementTypeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        var list = new PooledList<T>();
        try
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                list.Add(JsonSerializer.Deserialize(ref reader, elementTypeInfo) ?? default!);
            }
            return list;
        }
        catch
        {
            list.Dispose();
            throw;
        }
    }

    public override void Write(Utf8JsonWriter writer, PooledList<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        var elementTypeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        var span = value.Span;
        foreach (var t in span)
        {
            JsonSerializer.Serialize(writer, t, elementTypeInfo);
        }

        writer.WriteEndArray();
    }
}
