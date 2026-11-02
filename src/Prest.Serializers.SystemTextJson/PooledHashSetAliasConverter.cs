using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prest.Serializers.SystemTextJson;

/// <summary>
/// Converter for a hash set alias type <typeparamref name="TSet" /> — for example
/// <c>PooledHashSet&lt;T&gt;</c>, <c>SwissHashSet&lt;T&gt;</c> or
/// <c>LinearHashSet&lt;T,TFinalizer&gt;</c> — so that deserialization produces
/// <typeparamref name="TSet" /> rather than the generic base type.
/// </summary>
/// <remarks>
/// <see cref="PooledTypesJsonConverterFactory" /> creates these automatically. Construct one
/// yourself for Native AOT, or to deserialize a <c>Comparer*</c> alias with a known comparer
/// (register it ahead of the factory), passing e.g.
/// <c>capacity =&gt; ComparerSwissHashSet&lt;string&gt;.Create(StringComparer.OrdinalIgnoreCase, capacity)</c>.
/// </remarks>
/// <typeparam name="TSet">The alias type to read and write.</typeparam>
/// <typeparam name="T">Item type of <typeparamref name="TSet" />.</typeparam>
/// <typeparam name="TAlgo">Algorithm of <typeparamref name="TSet" />'s generic base.</typeparam>
public sealed class PooledHashSetAliasConverter<TSet, T, TAlgo> : JsonConverter<TSet>
    where TSet : PooledHashSet<T, TAlgo>
    where T : notnull
    where TAlgo : struct, IHashAlgorithm<T, T>
{
    readonly Func<int, TSet>? _create;

    /// <summary>Creates a converter that builds sets with <paramref name="create" />.</summary>
    /// <param name="create">
    /// Returns an empty <typeparamref name="TSet" /> for a capacity hint — usually the alias's
    /// own <c>Create</c>, e.g. <c>PooledHashSet&lt;string&gt;.Create</c>.
    /// </param>
    public PooledHashSetAliasConverter(Func<int, TSet> create) =>
        _create = create ?? throw new ArgumentNullException(nameof(create));

    /// <summary>Write-only converter for types it cannot construct; reading throws.</summary>
    internal PooledHashSetAliasConverter()
    {
    }

    public override TSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var create = _create ?? throw PooledHashJson.NoFactory(typeof(TSet), nameof(PooledHashSetAliasConverter<,,>));
        if (reader.TokenType is JsonTokenType.Null or JsonTokenType.None)
        {
            return create(0);
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray, got {reader.TokenType}");
        }

        var elementTypeInfo = options.GetTypedTypeInfo<T>();
        var set = create(0);
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

    public override void Write(Utf8JsonWriter writer, TSet value, JsonSerializerOptions options)
        => PooledHashJson.WriteItems(writer, value, options);
}
