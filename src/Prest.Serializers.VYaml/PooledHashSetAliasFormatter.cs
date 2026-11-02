using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Prest.Serializers.VYaml;

/// <summary>
/// Formatter for a hash set alias type <typeparamref name="TSet" /> — for example
/// <c>PooledHashSet&lt;T&gt;</c>, <c>SwissHashSet&lt;T&gt;</c> or
/// <c>LinearHashSet&lt;T,TFinalizer&gt;</c> — so that deserialization produces
/// <typeparamref name="TSet" /> rather than the generic base type.
/// </summary>
/// <remarks>
/// <see cref="PooledTypeFormatterResolver" /> creates these automatically. Construct one
/// yourself for Native AOT, or to deserialize a <c>Comparer*</c> alias with a known comparer,
/// passing e.g.
/// <c>capacity =&gt; ComparerSwissHashSet&lt;string&gt;.Create(StringComparer.OrdinalIgnoreCase, capacity)</c>.
/// </remarks>
/// <typeparam name="TSet">The alias type to read and write.</typeparam>
/// <typeparam name="T">Item type of <typeparamref name="TSet" />.</typeparam>
/// <typeparam name="TAlgo">Algorithm of <typeparamref name="TSet" />'s generic base.</typeparam>
public sealed class PooledHashSetAliasFormatter<TSet, T, TAlgo> : IYamlFormatter<TSet?>
    where TSet : PooledHashSet<T, TAlgo>
    where T : notnull
    where TAlgo : struct, IHashAlgorithm<T, T>
{
    readonly Func<int, TSet>? _create;

    /// <summary>Creates a formatter that builds sets with <paramref name="create" />.</summary>
    /// <param name="create">
    /// Returns an empty <typeparamref name="TSet" /> for a capacity hint — usually the alias's
    /// own <c>Create</c>, e.g. <c>PooledHashSet&lt;string&gt;.Create</c>.
    /// </param>
    public PooledHashSetAliasFormatter(Func<int, TSet> create) =>
        _create = create ?? throw new ArgumentNullException(nameof(create));

    /// <summary>Write-only formatter for types it cannot construct; reading throws.</summary>
    internal PooledHashSetAliasFormatter()
    {
    }

    public TSet Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        var create = _create ?? throw AliasFactory.NoFactory(typeof(TSet), "PooledHashSetAliasFormatter");
        if (parser.IsNullScalar())
        {
            parser.Read();
            return create(0);
        }

        parser.ReadWithVerify(ParseEventType.SequenceStart);

        var set = create(0);
        try
        {
            while (parser.CurrentEventType != ParseEventType.SequenceEnd)
            {
                set.Add(context.DeserializeWithAlias<T>(ref parser));
            }

            parser.ReadWithVerify(ParseEventType.SequenceEnd);
            return set;
        }
        catch
        {
            set.Dispose();
            throw;
        }
    }

    public void Serialize(ref Utf8YamlEmitter emitter, TSet? value, YamlSerializationContext context)
    {
        if (value is null)
        {
            emitter.WriteNull();
            return;
        }

        emitter.BeginSequence();
        foreach (var item in value)
        {
            context.Serialize(ref emitter, item);
        }

        emitter.EndSequence();
    }
}
