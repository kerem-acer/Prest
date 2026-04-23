#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using VYaml.Serialization;

namespace Prest.Serializers.VYaml;

/// <summary>
/// Resolves <see cref="IYamlFormatter{T}" /> for Prest's pooled generic types
/// (<see cref="PooledArray{T}" />, <see cref="PooledList{T}" />,
/// <c>PooledHashMap&lt;TKey,TValue,TAlgo&gt;</c>, and
/// <c>PooledHashSet&lt;T,TAlgo&gt;</c>) by constructing closed formatters via
/// reflection at lookup time. Any <see cref="IHashAlgorithm{TSlot,TKey}" />
/// closure is supported — the detected <c>TAlgo</c> is threaded through to the
/// formatter's generic arguments.
/// </summary>
/// <remarks>
/// <b>Native AOT:</b> this resolver uses <see cref="Type.MakeGenericType(Type[])" />
/// and is therefore incompatible with AOT / trimmed deployments. Under AOT,
/// construct the concrete <c>PooledArrayFormatter&lt;T&gt;</c>,
/// <c>PooledListFormatter&lt;T&gt;</c>, <c>PooledHashMapFormatter&lt;K,V,TAlgo&gt;</c>,
/// or <c>PooledHashSetFormatter&lt;T,TAlgo&gt;</c> you need at compile time and
/// compose them into your own <see cref="IYamlFormatterResolver" />.
/// </remarks>
#if NET
[RequiresDynamicCode("Constructs closed formatter types via reflection. Not compatible with Native AOT.")]
[RequiresUnreferencedCode("Constructs closed formatter types via reflection; trimming may remove the formatter implementations.")]
#endif
public sealed class PooledTypeFormatterResolver : IYamlFormatterResolver
{
    public static readonly PooledTypeFormatterResolver Instance = new();

    public IYamlFormatter<T>? GetFormatter<T>()
    {
        if (!typeof(T).IsGenericType)
        {
            return null;
        }

        var genericDef = typeof(T).GetGenericTypeDefinition();

        if (genericDef == typeof(PooledArray<>))
        {
            var elementType = typeof(T).GetGenericArguments()[0];
            var formatterType = typeof(PooledArrayFormatter<>).MakeGenericType(elementType);
            return (IYamlFormatter<T>)(Activator.CreateInstance(formatterType) ?? throw new InvalidOperationException());
        }

        if (genericDef == typeof(PooledList<>))
        {
            var elementType = typeof(T).GetGenericArguments()[0];
            var formatterType = typeof(PooledListFormatter<>).MakeGenericType(elementType);
            return (IYamlFormatter<T>)(Activator.CreateInstance(formatterType) ?? throw new InvalidOperationException());
        }

        if (genericDef == typeof(PooledHashMap<,,>))
        {
            var args = typeof(T).GetGenericArguments();
            var formatterType = typeof(PooledHashMapFormatter<,,>).MakeGenericType(args[0], args[1], args[2]);
            return (IYamlFormatter<T>)(Activator.CreateInstance(formatterType) ?? throw new InvalidOperationException());
        }

        if (genericDef == typeof(PooledHashSet<,>))
        {
            var args = typeof(T).GetGenericArguments();
            var formatterType = typeof(PooledHashSetFormatter<,>).MakeGenericType(args[0], args[1]);
            return (IYamlFormatter<T>)(Activator.CreateInstance(formatterType) ?? throw new InvalidOperationException());
        }

        return null;
    }
}
