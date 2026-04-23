using System.Buffers;
using System.Collections.Immutable;

namespace Prest.Immutable;

/// <summary>
/// Extension members that provide two-way conversion between
/// <see cref="PooledArray{T}" /> and <see cref="ImmutableArray{T}" />.
/// </summary>
public static class PooledArrayExtensions
{
    extension<T>(PooledArray<T> array)
    {
        /// <summary>
        /// Copies the elements into a new <see cref="ImmutableArray{T}" />. The
        /// returned immutable array is independent of <paramref name="array" />'s
        /// rented backing — disposing <paramref name="array" /> does not invalidate
        /// the returned copy.
        /// </summary>
        public ImmutableArray<T> ToImmutableArray()
        {
            var span = array.Span;
            if (span.Length == 0)
            {
                return [];
            }

#if NET8_0_OR_GREATER
            return ImmutableArray.Create(span);
#else
            var builder = ImmutableArray.CreateBuilder<T>(span.Length);
            for (var i = 0; i < span.Length; i++)
            {
                builder.Add(span[i]);
            }
            return builder.MoveToImmutable();
#endif
        }
    }

    extension(PooledArray)
    {
        /// <summary>
        /// Creates a <see cref="PooledArray{T}" /> by copying the contents of an
        /// <see cref="ImmutableArray{T}" />. The returned instance owns its rented
        /// backing array independently of <paramref name="items" />.
        /// </summary>
        public static PooledArray<T> FromImmutableArray<T>(ImmutableArray<T> items)
        {
            if (items.IsDefaultOrEmpty)
            {
                return default;
            }

            var array = ArrayPool<T>.Shared.Rent(items.Length);
            items.CopyTo(array);
            return new PooledArray<T>(array, items.Length);
        }
    }
}
