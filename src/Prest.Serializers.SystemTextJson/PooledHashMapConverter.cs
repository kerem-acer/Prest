using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Prest.Serializers.SystemTextJson;

public sealed class PooledHashMapConverter<TKey, TValue, TAlgo> : JsonConverter<PooledHashMap<TKey, TValue, TAlgo>>
    where TKey : ISpanFormattable, IUtf8SpanFormattable
    where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
{
    public override PooledHashMap<TKey, TValue, TAlgo> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null or JsonTokenType.None)
        {
            return PooledHashMap<TKey, TValue, TAlgo>.Create(0);
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected StartObject, got {reader.TokenType}");
        }

        var keyTypeInfo = (JsonTypeInfo<TKey>)options.GetTypeInfo(typeof(TKey));
        var keyConverter = (JsonConverter<TKey>)keyTypeInfo.Converter;
        var valueTypeInfo = (JsonTypeInfo<TValue>)options.GetTypeInfo(typeof(TValue));

        // PooledList always rents from ArrayPool — we need a rented array to hand off
        // to PooledHashMap's (rentedKeys, rentedValues, count) ctor.
        var keyList = new PooledList<TKey>();
        var valList = new PooledList<TValue>();
        try
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                // JSON property names are string tokens — ReadAsPropertyName handles the
                // parse (numeric/Guid converters override it; custom types need an override).
                keyList.Add(keyConverter.ReadAsPropertyName(ref reader, typeof(TKey), options));
                reader.Read();
                valList.Add(JsonSerializer.Deserialize(ref reader, valueTypeInfo) ?? default!);
            }

            keyList.DetachArray(out var keyArr, out var keyCount);
            valList.DetachArray(out var valArr, out _);
            if (keyCount == 0)
            {
                ArrayPool<TKey>.Shared.Return(keyArr, clearArray: true);
                ArrayPool<TValue>.Shared.Return(valArr, clearArray: true);
                return PooledHashMap<TKey, TValue, TAlgo>.Create(0);
            }

            return PooledHashMap<TKey, TValue, TAlgo>.Create(keyArr, valArr, keyCount);
        }
        catch
        {
            keyList.Dispose();
            valList.Dispose();
            throw;
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        PooledHashMap<TKey, TValue, TAlgo> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        var valueTypeInfo = (JsonTypeInfo<TValue>)options.GetTypeInfo(typeof(TValue));
        foreach (var kv in value)
        {
            WriteKey(writer, kv.Key);
            JsonSerializer.Serialize(writer, kv.Value, valueTypeInfo);
        }

        writer.WriteEndObject();
    }

    static void WriteKey(Utf8JsonWriter writer, TKey key)
    {
        Span<byte> buf = stackalloc byte[256];
        key.TryFormat(
            buf,
            out var written,
            default,
            null);

        writer.WritePropertyName(buf[..written]);
    }
}
