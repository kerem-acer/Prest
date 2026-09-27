using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Prest.Serializers.SystemTextJson;

static class JsonSerializerOptionsExtensions
{
    /// <summary>
    /// Typed <see cref="JsonTypeInfo{T}" /> lookup. Uses the generic
    /// <c>GetTypeInfo&lt;T&gt;()</c> added in .NET 11; earlier TFMs cast the
    /// untyped <see cref="JsonSerializerOptions.GetTypeInfo(Type)" /> result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JsonTypeInfo<T> GetTypedTypeInfo<T>(this JsonSerializerOptions options) =>
#if NET11_0_OR_GREATER
        options.GetTypeInfo<T>();
#else
        (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
#endif
}
