using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Prest.Serializers.SystemTextJson;

public sealed class PooledArrayConverter<T> : JsonConverter<PooledArray<T>>
{
#if NET
#pragma warning disable RCS1213
    [System.Runtime.CompilerServices.InlineArray(8)]
    struct InlineBuffer
    {
        T _element0;
    }
#pragma warning restore RCS1213
#endif

    public override PooledArray<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null or JsonTokenType.None)
        {
            return default;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray, got {reader.TokenType}");
        }

        var elementTypeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
#if NET
        InlineBuffer buffer = default;
        using var list = new StackOnlyPooledList<T>(buffer);
#else
        using var list = new StackOnlyPooledList<T>(8);
#endif
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            list.Add(JsonSerializer.Deserialize(ref reader, elementTypeInfo) ?? default!);
        }

        return list.ToPooledArray();
    }

    public override void Write(Utf8JsonWriter writer, PooledArray<T> value, JsonSerializerOptions options)
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
