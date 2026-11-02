using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Prest.Serializers.VYaml;

/// <summary>
/// Formatter for a hash map alias type <typeparamref name="TMap" /> — for example
/// <c>PooledHashMap&lt;K,V&gt;</c>, <c>SwissHashMap&lt;K,V&gt;</c> or
/// <c>RobinHoodHashMap&lt;K,V,TFinalizer&gt;</c> — so that deserialization produces
/// <typeparamref name="TMap" /> rather than the generic base type.
/// </summary>
/// <remarks>
/// <see cref="PooledTypeFormatterResolver" /> creates these automatically. Construct one
/// yourself for Native AOT, or to deserialize a <c>Comparer*</c> alias with a known comparer,
/// passing e.g.
/// <c>capacity =&gt; ComparerSwissHashMap&lt;string, int&gt;.Create(StringComparer.OrdinalIgnoreCase, capacity)</c>.
/// </remarks>
/// <typeparam name="TMap">The alias type to read and write.</typeparam>
/// <typeparam name="TKey">Key type of <typeparamref name="TMap" />.</typeparam>
/// <typeparam name="TValue">Value type of <typeparamref name="TMap" />.</typeparam>
/// <typeparam name="TAlgo">Algorithm of <typeparamref name="TMap" />'s generic base.</typeparam>
public sealed class PooledHashMapAliasFormatter<TMap, TKey, TValue, TAlgo> : IYamlFormatter<TMap?>
    where TMap : PooledHashMap<TKey, TValue, TAlgo>
    where TKey : notnull
    where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
{
    readonly Func<int, TMap>? _create;

    /// <summary>Creates a formatter that builds maps with <paramref name="create" />.</summary>
    /// <param name="create">
    /// Returns an empty <typeparamref name="TMap" /> for a capacity hint — usually the alias's
    /// own <c>Create</c>, e.g. <c>PooledHashMap&lt;string, int&gt;.Create</c>.
    /// </param>
    public PooledHashMapAliasFormatter(Func<int, TMap> create) =>
        _create = create ?? throw new ArgumentNullException(nameof(create));

    /// <summary>Write-only formatter for types it cannot construct; reading throws.</summary>
    internal PooledHashMapAliasFormatter()
    {
    }

    public TMap Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        var create = _create ?? throw AliasFactory.NoFactory(typeof(TMap), "PooledHashMapAliasFormatter");
        if (parser.IsNullScalar())
        {
            parser.Read();
            return create(0);
        }

        parser.ReadWithVerify(ParseEventType.MappingStart);

        var map = create(0);
        try
        {
            while (parser.CurrentEventType != ParseEventType.MappingEnd)
            {
                var key = context.DeserializeWithAlias<TKey>(ref parser);
                map.Add(key, context.DeserializeWithAlias<TValue>(ref parser));
            }

            parser.ReadWithVerify(ParseEventType.MappingEnd);
            return map;
        }
        catch
        {
            map.Dispose();
            throw;
        }
    }

    public void Serialize(ref Utf8YamlEmitter emitter, TMap? value, YamlSerializationContext context)
    {
        if (value is null)
        {
            emitter.WriteNull();
            return;
        }

        emitter.BeginMapping();
        foreach (var kv in value)
        {
            context.Serialize(ref emitter, kv.Key);
            context.Serialize(ref emitter, kv.Value);
        }

        emitter.EndMapping();
    }
}
