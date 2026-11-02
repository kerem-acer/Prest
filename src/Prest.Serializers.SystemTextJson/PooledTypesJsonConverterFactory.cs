using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prest.Serializers.SystemTextJson;

/// <summary>
/// Single-registration <see cref="JsonConverterFactory" /> that produces converters
/// for every Prest pooled generic type — <see cref="PooledArray{T}" />,
/// <see cref="PooledList{T}" />, <c>PooledHashMap&lt;K,V,TAlgo&gt;</c>, and
/// <c>PooledHashSet&lt;T,TAlgo&gt;</c> — at deserialization time. The detected
/// generic arguments (including any <see cref="IHashAlgorithm{TSlot,TKey}" />
/// closure) are threaded through to the closed converter's generics.
/// </summary>
/// <remarks>
/// <para>
/// Alias types (<c>PooledHashMap&lt;K,V&gt;</c>, <c>SwissHashMap&lt;K,V&gt;</c>,
/// <c>RobinHoodHashSet&lt;T,TFinalizer&gt;</c>, …) deserialize to the alias itself, built with
/// the alias's own <c>Create(int)</c>. <c>Comparer*</c> aliases serialize normally, but reading
/// one throws <see cref="NotSupportedException" /> because the comparer is not in the payload —
/// register a <see cref="PooledHashMapAliasConverter{TMap,TKey,TValue,TAlgo}" /> /
/// <see cref="PooledHashSetAliasConverter{TSet,T,TAlgo}" /> with a factory that supplies it,
/// ahead of this factory.
/// </para>
/// <para>
/// <b>Native AOT:</b> this factory uses <see cref="Type.MakeGenericType(Type[])" />
/// and is therefore incompatible with AOT / trimmed deployments. Under AOT, register
/// each concrete converter instead (see <c>PooledArrayConverter&lt;T&gt;</c>,
/// <c>PooledListConverter&lt;T&gt;</c>, <c>PooledHashMapConverter&lt;K,V,TAlgo&gt;</c>,
/// <c>PooledHashSetConverter&lt;T,TAlgo&gt;</c>, and for alias types
/// <c>PooledHashMapAliasConverter&lt;TMap,K,V,TAlgo&gt;</c> /
/// <c>PooledHashSetAliasConverter&lt;TSet,T,TAlgo&gt;</c>) — each is itself AOT-safe when
/// closed over concrete generic arguments.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var options = new JsonSerializerOptions();
/// options.Converters.Add(new PooledTypesJsonConverterFactory());
/// // Now any PooledArray&lt;T&gt; / PooledList&lt;T&gt; / PooledHashMap&lt;K,V,TAlgo&gt;
/// // / PooledHashSet&lt;T,TAlgo&gt; serializes and deserializes with no further setup.
/// </code>
/// </example>
[RequiresDynamicCode("Constructs closed converter types via reflection. Not compatible with Native AOT.")]
[RequiresUnreferencedCode("Constructs closed converter types via reflection; trimming may remove the converter implementations.")]
public sealed class PooledTypesJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        MatchArrayOrList(typeToConvert) is not null
        || MatchHashMapBase(typeToConvert) is not null
        || MatchHashSetBase(typeToConvert) is not null;

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (MatchArrayOrList(typeToConvert) is ({ } def, { } args))
        {
            Type converterOpen = def == typeof(PooledArray<>)
                ? typeof(PooledArrayConverter<>)
                : typeof(PooledListConverter<>);
            return (JsonConverter)Activator.CreateInstance(converterOpen.MakeGenericType(args[0]))!;
        }

        if (MatchHashMapBase(typeToConvert) is { } mapBase)
        {
            var a = mapBase.GetGenericArguments();
            return typeToConvert == mapBase
                ? (JsonConverter)Activator.CreateInstance(typeof(PooledHashMapConverter<,,>).MakeGenericType(a[0], a[1], a[2]))!
                : CreateAliasConverter(
                    typeof(PooledHashMapAliasConverter<,,,>).MakeGenericType(typeToConvert, a[0], a[1], a[2]),
                    typeToConvert);
        }

        if (MatchHashSetBase(typeToConvert) is { } setBase)
        {
            var a = setBase.GetGenericArguments();
            return typeToConvert == setBase
                ? (JsonConverter)Activator.CreateInstance(typeof(PooledHashSetConverter<,>).MakeGenericType(a[0], a[1]))!
                : CreateAliasConverter(
                    typeof(PooledHashSetAliasConverter<,,>).MakeGenericType(typeToConvert, a[0], a[1]),
                    typeToConvert);
        }

        return null;
    }

    /// <summary>
    /// Builds the alias converter around the alias's own <c>Create(int)</c>. Types without a
    /// usable one (the <c>Comparer*</c> aliases hide theirs behind <c>[Obsolete(error: true)]</c>
    /// because they cannot know their comparer) get a converter that writes but throws
    /// <see cref="NotSupportedException" /> on read.
    /// </summary>
    static JsonConverter CreateAliasConverter(Type converterType, Type aliasType) =>
        FindCapacityFactory(aliasType) is { } create
            ? (JsonConverter)Activator.CreateInstance(converterType, create)!
            : (JsonConverter)Activator.CreateInstance(converterType, nonPublic: true)!;

    /// <summary>
    /// Returns the alias's public static <c>Create(int)</c> declared on the alias itself and
    /// returning it, as a <c>Func&lt;int, TAlias&gt;</c>; null when there is none.
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

    /// <summary>Returns (openDef, args) if <paramref name="t" /> is a closed <c>PooledArray&lt;&gt;</c> or <c>PooledList&lt;&gt;</c>; null otherwise.</summary>
    static (Type openDef, Type[] args)? MatchArrayOrList(Type t)
    {
        if (!t.IsGenericType)
        {
            return null;
        }
        var def = t.GetGenericTypeDefinition();
        return def == typeof(PooledArray<>) || def == typeof(PooledList<>)
            ? (def, t.GetGenericArguments())
            : null;
    }

    /// <summary>Walks up the class hierarchy looking for the closed <c>PooledHashMap&lt;,,&gt;</c> base. Handles the 2-arg default alias transparently.</summary>
    static Type? MatchHashMapBase(Type t) => WalkToClosedBase(t, typeof(PooledHashMap<,,>));

    /// <summary>Walks up the class hierarchy looking for the closed <c>PooledHashSet&lt;,&gt;</c> base. Handles the 1-arg default alias transparently.</summary>
    static Type? MatchHashSetBase(Type t) => WalkToClosedBase(t, typeof(PooledHashSet<,>));

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
