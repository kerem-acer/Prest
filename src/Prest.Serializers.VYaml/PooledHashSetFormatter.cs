using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Prest.Serializers.VYaml;

public sealed class PooledHashSetFormatter<T, TAlgo> : IYamlFormatter<PooledHashSet<T, TAlgo>>
    where T : notnull
    where TAlgo : struct, IHashAlgorithm<T, T>
{
    public PooledHashSet<T, TAlgo> Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        if (parser.IsNullScalar())
        {
            parser.Read();
            return PooledHashSet<T, TAlgo>.Create(0);
        }

        parser.ReadWithVerify(ParseEventType.SequenceStart);

        var set = PooledHashSet<T, TAlgo>.Create(0);
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

    public void Serialize(ref Utf8YamlEmitter emitter, PooledHashSet<T, TAlgo> value, YamlSerializationContext context)
    {
        emitter.BeginSequence();
        foreach (var item in value)
        {
            context.Serialize(ref emitter, item);
        }

        emitter.EndSequence();
    }
}
