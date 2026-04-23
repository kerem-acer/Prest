using System.Buffers;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Prest.Serializers.VYaml;

public sealed class PooledArrayFormatter<T> : IYamlFormatter<PooledArray<T>>
{
    public PooledArray<T> Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        if (parser.IsNullScalar())
        {
            parser.Read();
            return default;
        }

        parser.ReadWithVerify(ParseEventType.SequenceStart);

        var items = ArrayPool<T>.Shared.Rent(8);
        var count = 0;
        try
        {
            while (parser.CurrentEventType != ParseEventType.SequenceEnd)
            {
                if (count >= items.Length)
                {
                    GrowArray(ref items, count);
                }

                items[count++] = context.DeserializeWithAlias<T>(ref parser);
            }

            parser.ReadWithVerify(ParseEventType.SequenceEnd);
            return new PooledArray<T>(items, count);
        }
        catch
        {
            ArrayPool<T>.Shared.Return(items, clearArray: true);
            throw;
        }
    }

    public void Serialize(ref Utf8YamlEmitter emitter, PooledArray<T> value, YamlSerializationContext context)
    {
        emitter.BeginSequence();
        var span = value.Span;
        for (var i = 0; i < span.Length; i++)
        {
            context.Serialize(ref emitter, span[i]);
        }

        emitter.EndSequence();
    }

    static void GrowArray(ref T[] array, int count)
    {
        var newArray = ArrayPool<T>.Shared.Rent(array.Length * 2);
        Array.Copy(array, newArray, count);
        ArrayPool<T>.Shared.Return(array, clearArray: true);
        array = newArray;
    }
}
