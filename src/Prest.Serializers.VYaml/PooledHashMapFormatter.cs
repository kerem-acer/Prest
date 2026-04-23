using System.Buffers;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Prest.Serializers.VYaml;

public sealed class PooledHashMapFormatter<TKey, TValue, TAlgo> : IYamlFormatter<PooledHashMap<TKey, TValue, TAlgo>>
    where TKey : notnull
    where TAlgo : struct, IHashAlgorithm<KeyValueSlot<TKey, TValue>, TKey>
{
    public PooledHashMap<TKey, TValue, TAlgo> Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        if (parser.IsNullScalar())
        {
            parser.Read();
            return PooledHashMap<TKey, TValue, TAlgo>.Create(0);
        }

        parser.ReadWithVerify(ParseEventType.MappingStart);

        var keys = ArrayPool<TKey>.Shared.Rent(8);
        var values = ArrayPool<TValue>.Shared.Rent(8);
        var count = 0;
        try
        {
            while (parser.CurrentEventType != ParseEventType.MappingEnd)
            {
                if (count >= keys.Length)
                {
                    GrowArray(ref keys, count);
                    GrowArray(ref values, count);
                }

                keys[count] = context.DeserializeWithAlias<TKey>(ref parser);
                values[count] = context.DeserializeWithAlias<TValue>(ref parser);
                count++;
            }

            parser.ReadWithVerify(ParseEventType.MappingEnd);
            return PooledHashMap<TKey, TValue, TAlgo>.Create(keys, values, count);
        }
        catch
        {
            ArrayPool<TKey>.Shared.Return(keys, clearArray: true);
            ArrayPool<TValue>.Shared.Return(values, clearArray: true);
            throw;
        }
    }

    public void Serialize(ref Utf8YamlEmitter emitter, PooledHashMap<TKey, TValue, TAlgo> value, YamlSerializationContext context)
    {
        emitter.BeginMapping();
        foreach (var kv in value)
        {
            context.Serialize(ref emitter, kv.Key);
            context.Serialize(ref emitter, kv.Value);
        }

        emitter.EndMapping();
    }

    static void GrowArray<TItem>(ref TItem[] array, int count)
    {
        var newArray = ArrayPool<TItem>.Shared.Rent(array.Length * 2);
        Array.Copy(array, newArray, count);
        ArrayPool<TItem>.Shared.Return(array, clearArray: true);
        array = newArray;
    }
}
