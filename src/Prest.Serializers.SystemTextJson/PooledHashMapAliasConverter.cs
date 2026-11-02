using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prest.Serializers.SystemTextJson;

/// <summary>
/// Converter for a hash map alias type <typeparamref name="TMap" /> — for example
/// <c>PooledHashMap&lt;K,V&gt;</c>, <c>SwissHashMap&lt;K,V&gt;</c> or
/// <c>RobinHoodHashMap&lt;K,V,TFinalizer&gt;</c> — so that deserialization produces
/// <typeparamref name="TMap" /> rather than the generic base type.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PooledTypesJsonConverterFactory" /> creates these automatically. Construct one
/// yourself for Native AOT, or to deserialize a <c>Comparer*</c> alias with a known comparer
/// (register it ahead of the factory):
/// </para>
/// <code>
/// options.Converters.Add(new PooledHashMapAliasConverter&lt;
///     ComparerSwissHashMap&lt;string, int&gt;, string, int,
///     SwissTableAlgorithm&lt;KeyValueSlot&lt;string, int&gt;, string, ComparerHasher&lt;string&gt;, NoOpHashFinalizer&gt;&gt;(
///     capacity =&gt; ComparerSwissHashMap&lt;string, int&gt;.Create(StringComparer.OrdinalIgnoreCase, capacity)));
/// </code>
/// </remarks>
/// <typeparam name="TMap">The alias type to read and write.</typeparam>
/// <typeparam name="TKey">Key type of <typeparamref name="TMap" />.</typeparam>
/// <typeparam name="TValue">Value type of <typeparamref name="TMap" />.</typeparam>
/// <typeparam name="TAlgo">Algorithm of <typeparamref name="TMap" />'s generic base.</typeparam>
public sealed class PooledHashMapAliasConverter<TMap, TKey, TValue, TAlgo> : JsonConverter<TMap>
    where TMap : PooledHashMap<TKey, TValue, TAlgo>
    where TKey : notnull
    where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
{
    readonly Func<int, TMap>? _create;

    /// <summary>Creates a converter that builds maps with <paramref name="create" />.</summary>
    /// <param name="create">
    /// Returns an empty <typeparamref name="TMap" /> for a capacity hint — usually the alias's
    /// own <c>Create</c>, e.g. <c>PooledHashMap&lt;string, int&gt;.Create</c>.
    /// </param>
    public PooledHashMapAliasConverter(Func<int, TMap> create) =>
        _create = create ?? throw new ArgumentNullException(nameof(create));

    /// <summary>Write-only converter for types it cannot construct; reading throws.</summary>
    internal PooledHashMapAliasConverter()
    {
    }

    public override TMap Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var create = _create ?? throw PooledHashJson.NoFactory(typeof(TMap), nameof(PooledHashMapAliasConverter<,,,>));
        if (reader.TokenType is JsonTokenType.Null or JsonTokenType.None)
        {
            return create(0);
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected StartObject, got {reader.TokenType}");
        }

        var keyConverter = PooledHashJson.KeyConverter<TKey>(options);
        var valueTypeInfo = options.GetTypedTypeInfo<TValue>();
        var map = create(0);
        try
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                var key = keyConverter.ReadAsPropertyName(ref reader, typeof(TKey), options);
                reader.Read();
                map.Add(key, JsonSerializer.Deserialize(ref reader, valueTypeInfo) ?? default!);
            }
            return map;
        }
        catch
        {
            map.Dispose();
            throw;
        }
    }

    public override void Write(Utf8JsonWriter writer, TMap value, JsonSerializerOptions options)
        => PooledHashJson.Write(writer, value, options);
}
