using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prest.Serializers.SystemTextJson;

/// <summary>
/// Converter for the generic <see cref="PooledHashMap{TKey,TValue,TAlgo}" />. Alias types
/// (<c>PooledHashMap&lt;K,V&gt;</c>, <c>SwissHashMap&lt;K,V&gt;</c>, …) use
/// <see cref="PooledHashMapAliasConverter{TMap,TKey,TValue,TAlgo}" /> so deserialization
/// produces the alias type.
/// </summary>
/// <remarks>
/// Keys are written and read as JSON property names through the key type's converter, so any
/// key type System.Text.Json supports for <see cref="Dictionary{TKey,TValue}" /> keys works
/// (<see cref="string" />, numbers, <see cref="Guid" />, enums, …).
/// </remarks>
public sealed class PooledHashMapConverter<TKey, TValue, TAlgo> : JsonConverter<PooledHashMap<TKey, TValue, TAlgo>>
    where TKey : notnull
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

        var keyConverter = PooledHashJson.KeyConverter<TKey>(options);
        var valueTypeInfo = options.GetTypedTypeInfo<TValue>();

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
        => PooledHashJson.Write(writer, value, options);
}
