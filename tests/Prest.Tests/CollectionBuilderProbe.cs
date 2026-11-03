using System.Reflection;

namespace Prest.Tests;

/// <summary>
/// Finds a collection builder's <c>[Obsolete(error: true)]</c> overloads (the ones that turn comparer
/// misuse into compile errors) and calls each one. The calls go through delegates because
/// <see cref="MethodBase.Invoke(object, object[])" /> can't pass a <see cref="ReadOnlySpan{T}" />.
/// </summary>
static class CollectionBuilderProbe
{
    /// <summary>
    /// Closes every obsolete overload of <paramref name="builder" /> over <paramref name="typeArguments" />
    /// (plus <see cref="FibonacciFinalizer" /> for the finalizer shapes) and calls it with an empty span,
    /// a <see langword="null" /> comparer and zero capacity.
    /// </summary>
    internal static ObsoleteOverload[] ProbeObsoleteOverloads(Type builder, Type[] typeArguments)
        => builder.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => method.IsDefined(typeof(ObsoleteAttribute)))
            .Select(method => Probe(Close(method, typeArguments)))
            .ToArray();

    static MethodInfo Close(MethodInfo method, Type[] typeArguments)
    {
        Type[] closed = method.GetGenericArguments().Length == typeArguments.Length
            ? typeArguments
            : [.. typeArguments, typeof(FibonacciFinalizer)];
        return method.MakeGenericMethod(closed);
    }

    static ObsoleteOverload Probe(MethodInfo method)
    {
        var key = method.GetGenericArguments()[0];
        var item = method.GetParameters()[^1].ParameterType.GetGenericArguments()[0];
        var call = typeof(CollectionBuilderProbe)
            .GetMethod(nameof(Call), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(key, item);
        var thrown = (NotSupportedException?)call.Invoke(null, [method]);
        return new(method.ReturnType, method.GetCustomAttribute<ObsoleteAttribute>()!, thrown);
    }

    static NotSupportedException? Call<TKey, TItem>(MethodInfo method)
    {
        var leading = method.GetParameters()[..^1];
        Action call = leading.Length switch
        {
            0 => () => method.CreateDelegate<SpanFactory<TItem>>()(default),
            1 when leading[0].ParameterType == typeof(int) => () => method.CreateDelegate<CapacitySpanFactory<TItem>>()(0, default),
            1 => () => method.CreateDelegate<ComparerSpanFactory<TKey, TItem>>()(null, default),
            2 => () => method.CreateDelegate<ComparerCapacitySpanFactory<TKey, TItem>>()(null, 0, default),
            _ => throw new ArgumentException($"Unexpected builder overload {method}.", nameof(method)),
        };
        try
        {
            call();
            return null;
        }
        catch (NotSupportedException e)
        {
            return e;
        }
    }

    delegate object SpanFactory<TItem>(ReadOnlySpan<TItem> items);

    delegate object CapacitySpanFactory<TItem>(int capacity, ReadOnlySpan<TItem> items);

    delegate object ComparerSpanFactory<TKey, TItem>(IEqualityComparer<TKey>? comparer, ReadOnlySpan<TItem> items);

    delegate object ComparerCapacitySpanFactory<TKey, TItem>(IEqualityComparer<TKey>? comparer, int capacity, ReadOnlySpan<TItem> items);

    /// <summary>An obsolete builder overload: the alias it builds, its attribute, and what calling it threw.</summary>
    internal readonly record struct ObsoleteOverload(Type Builds, ObsoleteAttribute Obsolete, NotSupportedException? Thrown);
}
