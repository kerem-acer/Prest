using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Prest.Serializers.VYaml;

public sealed class PooledListFormatter<T> : IYamlFormatter<PooledList<T>?>
{
    public PooledList<T>? Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        if (parser.IsNullScalar())
        {
            parser.Read();
            return null;
        }

        parser.ReadWithVerify(ParseEventType.SequenceStart);

        var list = new PooledList<T>();
        try
        {
            while (parser.CurrentEventType != ParseEventType.SequenceEnd)
            {
                list.Add(context.DeserializeWithAlias<T>(ref parser));
            }

            parser.ReadWithVerify(ParseEventType.SequenceEnd);
            return list;
        }
        catch
        {
            list.Dispose();
            throw;
        }
    }

    public void Serialize(ref Utf8YamlEmitter emitter, PooledList<T>? value, YamlSerializationContext context)
    {
        if (value is null)
        {
            emitter.WriteNull();
            return;
        }

        emitter.BeginSequence();
        var span = value.Span;
        for (var i = 0; i < span.Length; i++)
        {
            context.Serialize(ref emitter, span[i]);
        }

        emitter.EndSequence();
    }
}
