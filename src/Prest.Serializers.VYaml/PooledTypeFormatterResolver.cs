#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;
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
/// <para>
/// Alias types (<c>PooledHashMap&lt;K,V&gt;</c>, <c>SwissHashMap&lt;K,V&gt;</c>,
/// <c>RobinHoodHashSet&lt;T,TFinalizer&gt;</c>, …) resolve to
/// <see cref="PooledHashMapAliasFormatter{TMap,TKey,TValue,TAlgo}" /> /
/// <see cref="PooledHashSetAliasFormatter{TSet,T,TAlgo}" />, which deserialize to the alias
/// itself through the alias's own <c>Create(int)</c>. <c>Comparer*</c> aliases serialize
/// normally, but reading one throws <see cref="NotSupportedException" /> because the comparer
/// is not in the payload — resolve an alias formatter constructed with a factory that supplies
/// it ahead of this resolver.
/// </para>
/// <para>
/// <b>Native AOT:</b> this resolver uses <see cref="Type.MakeGenericType(Type[])" />
/// and is therefore incompatible with AOT / trimmed deployments. Under AOT,
/// construct the concrete <c>PooledArrayFormatter&lt;T&gt;</c>,
/// <c>PooledListFormatter&lt;T&gt;</c>, <c>PooledHashMapFormatter&lt;K,V,TAlgo&gt;</c>,
/// <c>PooledHashSetFormatter&lt;T,TAlgo&gt;</c>, or for alias types
/// <c>PooledHashMapAliasFormatter&lt;TMap,K,V,TAlgo&gt;</c> /
/// <c>PooledHashSetAliasFormatter&lt;TSet,T,TAlgo&gt;</c> you need at compile time and
/// compose them into your own <see cref="IYamlFormatterResolver" />.
/// </para>
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
        var type = typeof(T);

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();

            if (genericDef == typeof(PooledArray<>))
            {
                var elementType = type.GetGenericArguments()[0];
                var formatterType = typeof(PooledArrayFormatter<>).MakeGenericType(elementType);
                return (IYamlFormatter<T>)(Activator.CreateInstance(formatterType) ?? throw new InvalidOperationException());
            }

            if (genericDef == typeof(PooledList<>))
            {
                var elementType = type.GetGenericArguments()[0];
                var formatterType = typeof(PooledListFormatter<>).MakeGenericType(elementType);
                return (IYamlFormatter<T>)(Activator.CreateInstance(formatterType) ?? throw new InvalidOperationException());
            }
        }

        if (WalkToClosedBase(type, typeof(PooledHashMap<,,>)) is { } mapBase)
        {
            var args = mapBase.GetGenericArguments();
            return type == mapBase
                ? Create<T>(typeof(PooledHashMapFormatter<,,>).MakeGenericType(args[0], args[1], args[2]))
                : CreateAlias<T>(typeof(PooledHashMapAliasFormatter<,,,>).MakeGenericType(type, args[0], args[1], args[2]));
        }

        if (WalkToClosedBase(type, typeof(PooledHashSet<,>)) is { } setBase)
        {
            var args = setBase.GetGenericArguments();
            return type == setBase
                ? Create<T>(typeof(PooledHashSetFormatter<,>).MakeGenericType(args[0], args[1]))
                : CreateAlias<T>(typeof(PooledHashSetAliasFormatter<,,>).MakeGenericType(type, args[0], args[1]));
        }

        return null;
    }

    static IYamlFormatter<T> Create<T>(Type formatterType) =>
        (IYamlFormatter<T>)(Activator.CreateInstance(formatterType) ?? throw new InvalidOperationException());

    /// <summary>
    /// Builds the alias formatter around the alias's own <c>Create(int)</c>; types without a
    /// usable one get a formatter that writes but throws <see cref="NotSupportedException" /> on read.
    /// </summary>
    static IYamlFormatter<T> CreateAlias<T>(Type formatterType) =>
        (IYamlFormatter<T>)((FindCapacityFactory(typeof(T)) is { } create
            ? Activator.CreateInstance(formatterType, create)
            : Activator.CreateInstance(formatterType, nonPublic: true))
            ?? throw new InvalidOperationException());

    /// <summary>
    /// Returns the alias's public static <c>Create(int)</c> declared on the alias itself and
    /// returning it, as a <c>Func&lt;int, TAlias&gt;</c>; null when there is none. The
    /// <c>Comparer*</c> aliases hide theirs behind <c>[Obsolete(error: true)]</c> because they
    /// cannot know their comparer, so they get null too.
    /// </summary>
    static Delegate? FindCapacityFactory(Type aliasType)
    {
        var create = aliasType.GetMethod(
            "Create",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
            binder: null,
            [typeof(int)],
            modifiers: null);
        return create is null
            || create.ReturnType != aliasType
            || create.GetCustomAttribute<ObsoleteAttribute>() is { IsError: true }
            ? null
            : create.CreateDelegate(typeof(Func<,>).MakeGenericType(typeof(int), aliasType));
    }

    /// <summary>Walks up the class hierarchy looking for the closed <paramref name="openGenericDef" /> base.</summary>
    static Type? WalkToClosedBase(Type t, Type openGenericDef)
    {
        var current = t;
        while (current is not null && current != typeof(object))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == openGenericDef)
            {
                return current;
            }
            current = current.BaseType;
        }
        return null;
    }
}
