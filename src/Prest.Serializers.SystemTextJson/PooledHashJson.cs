using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prest.Serializers.SystemTextJson;

/// <summary>Read/write logic shared by the generic and alias hash map / hash set converters.</summary>
static class PooledHashJson
{
    /// <summary>
    /// Key converter for <typeparamref name="TKey" />. Keys go through
    /// <see cref="JsonConverter{T}.ReadAsPropertyName" /> /
    /// <see cref="JsonConverter{T}.WriteAsPropertyName" />, the same path
    /// <see cref="Dictionary{TKey,TValue}" /> uses, so every key type System.Text.Json
    /// supports as a dictionary key works here too.
    /// </summary>
    internal static JsonConverter<TKey> KeyConverter<TKey>(JsonSerializerOptions options) =>
        (JsonConverter<TKey>)options.GetTypedTypeInfo<TKey>().Converter;

    internal static void Write<TKey, TValue, TAlgo>(
        Utf8JsonWriter writer,
        PooledHashMap<TKey, TValue, TAlgo> map,
        JsonSerializerOptions options)
        where TKey : notnull
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
    {
        writer.WriteStartObject();
        var keyConverter = KeyConverter<TKey>(options);
        var valueTypeInfo = options.GetTypedTypeInfo<TValue>();
        foreach (var kv in map)
        {
            keyConverter.WriteAsPropertyName(writer, kv.Key, options);
            JsonSerializer.Serialize(writer, kv.Value, valueTypeInfo);
        }

        writer.WriteEndObject();
    }

    internal static void WriteItems<T, TAlgo>(
        Utf8JsonWriter writer,
        PooledHashSet<T, TAlgo> set,
        JsonSerializerOptions options)
        where T : notnull
        where TAlgo : struct, IHashAlgorithm<T, T>
    {
        writer.WriteStartArray();
        var elementTypeInfo = options.GetTypedTypeInfo<T>();
        foreach (var item in set)
        {
            JsonSerializer.Serialize(writer, item, elementTypeInfo);
        }

        writer.WriteEndArray();
    }

    internal static NotSupportedException NoFactory(Type collectionType, string converterName) =>
        new($"Cannot deserialize {collectionType}: it has no public static Create(int) that returns it "
            + "(Comparer* aliases need their comparer, which is not part of the payload). Register "
            + $"{converterName} with a factory delegate, e.g. capacity => "
            + "ComparerSwissHashMap<string, int>.Create(StringComparer.OrdinalIgnoreCase, capacity), "
            + "ahead of PooledTypesJsonConverterFactory.");
}
