using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Prest;

/// <summary>
/// Provides the <see cref="CollectionBuilderAttribute" /> factory for
/// <see cref="PooledArray{T}" /> collection expressions, plus additional
/// factory overloads.
/// </summary>
public static class PooledArray
{
    /// <summary>
    /// Creates a <see cref="PooledArray{T}" /> from a <see cref="ReadOnlySpan{T}" />.
    /// Used by collection expressions: <c>PooledArray&lt;int&gt; x = [1, 2, 3];</c>
    /// </summary>
    public static PooledArray<T> Create<T>(ReadOnlySpan<T> items)
    {
        if (items.Length == 0)
        {
            return default;
        }

        var array = ArrayPool<T>.Shared.Rent(items.Length);
        items.CopyTo(array);
        return new PooledArray<T>(array, items.Length);
    }

    /// <summary>
    /// Creates a <see cref="PooledArray{T}" /> of <paramref name="count" /> elements
    /// all initialized to <paramref name="value" />.
    /// </summary>
    public static PooledArray<T> Create<T>(int count, T value)
    {
        if (count <= 0)
        {
            return default;
        }

        var array = ArrayPool<T>.Shared.Rent(count);
        array.AsSpan(0, count).Fill(value);
        return new PooledArray<T>(array, count);
    }
}

/// <summary>
/// Fixed-size array backed by <see cref="ArrayPool{T}.Shared" />. The structure
/// (count and backing array) is fixed after construction; element contents can
/// be mutated via <see cref="Sort" /> / <see cref="Reverse" />. Disposing returns
/// the rented array and disposes elements if they implement <see cref="IDisposable" />.
/// </summary>
/// <remarks>
/// Default value is empty (<see cref="IsEmpty" /> = <see langword="true" />).
/// Ownership is single-consumer — do not dispose the same instance twice.
/// Supports collection expressions: <c>PooledArray&lt;int&gt; x = [1, 2, 3];</c>
/// </remarks>
[CollectionBuilder(typeof(PooledArray), nameof(PooledArray.Create))]
[DebuggerDisplay("Count = {Count}")]
public readonly struct PooledArray<T> : IDisposable, IEquatable<PooledArray<T>>
{
    static readonly bool IsElementDisposable = typeof(IDisposable).IsAssignableFrom(typeof(T));

    readonly T[]? _items;

    /// <summary>Wraps a rented array with a valid element count.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledArray(T[] rentedItems, int count)
    {
        _items = rentedItems;
        Count = count;
    }

    /// <summary>The empty instance. Equivalent to <c>default(PooledArray&lt;T&gt;)</c>; no allocation.</summary>
    public static PooledArray<T> Empty => default;

    /// <summary>Number of elements.</summary>
    public int Count { get; }

    /// <summary>True when this instance holds no elements.</summary>
    public bool IsEmpty => Count == 0;

    /// <summary>Returns a span over the valid elements.</summary>
    public ReadOnlySpan<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (Count == 0)
            {
                return [];
            }

#if NET6_0_OR_GREATER
            return MemoryMarshal.CreateReadOnlySpan(
                ref MemoryMarshal.GetArrayDataReference(_items!), Count);
#else
            return new ReadOnlySpan<T>(_items, 0, Count);
#endif
        }
    }

    /// <summary>Returns a <see cref="ReadOnlyMemory{T}" /> view over the valid elements.</summary>
    public ReadOnlyMemory<T> AsMemory() => _items.AsMemory(0, Count);

    /// <summary>Gets a readonly reference to the element at <paramref name="index" />.</summary>
    public ref readonly T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)Count)
            {
                ThrowIndexOutOfRange();
            }

#if NET6_0_OR_GREATER
            return ref Unsafe.Add(
                ref MemoryMarshal.GetArrayDataReference(_items!), (nint)(uint)index);
#else
            return ref _items![index];
#endif
        }
    }

    /// <summary>
    /// Returns the zero-based index of the first occurrence of <paramref name="item" />,
    /// or <c>-1</c> if not found. Uses <see cref="EqualityComparer{T}.Default" />.
    /// </summary>
    public int IndexOf(T item)
    {
        var items = _items;
        return items is null ? -1 : Array.IndexOf(items, item, 0, Count);
    }

    /// <summary>
    /// Returns <see langword="true" /> if <paramref name="item" /> is present.
    /// Uses <see cref="EqualityComparer{T}.Default" />.
    /// </summary>
    public bool Contains(T item) => IndexOf(item) >= 0;

    /// <summary>
    /// Copies the elements to a freshly-allocated array. The returned array is owned
    /// by the caller — not tied to this instance's pool lifetime.
    /// </summary>
    public T[] ToArray() => Span.ToArray();

    /// <summary>
    /// Sorts the elements in-place using <see cref="Comparer{T}.Default" />. Mutates contents.
    /// </summary>
    public void Sort()
    {
        if (Count < 2)
        {
            return;
        }

#if NET6_0_OR_GREATER
        MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(_items!), Count).Sort();
#else
        Array.Sort(_items, 0, Count);
#endif
    }

    /// <summary>Reverses the order of elements in-place. Mutates contents.</summary>
    public void Reverse()
    {
        if (Count < 2)
        {
            return;
        }

#if NET6_0_OR_GREATER
        MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(_items!), Count).Reverse();
#else
        Array.Reverse(_items, 0, Count);
#endif
    }

    /// <summary>
    /// Disposes each element (if <typeparamref name="T" /> implements <see cref="IDisposable" />)
    /// and returns the backing array to <see cref="ArrayPool{T}.Shared" />.
    /// </summary>
    public void Dispose()
    {
        var items = _items;
        if (items is null)
        {
            return;
        }

        if (IsElementDisposable)
        {
#if NET6_0_OR_GREATER
            ref var first = ref MemoryMarshal.GetArrayDataReference(items);
            for (var i = 0; i < Count; i++)
            {
                (Unsafe.Add(ref first, (nint)(uint)i) as IDisposable)?.Dispose();
            }
#else
            for (var i = 0; i < Count; i++)
            {
                (items[i] as IDisposable)?.Dispose();
            }
#endif
        }

#if NET
        ArrayPool<T>.Shared.Return(items, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
#else
        ArrayPool<T>.Shared.Return(items, clearArray: true);
#endif
    }

    /// <summary>Returns an enumerator over the valid elements.</summary>
    public Enumerator GetEnumerator() => new(_items, Count);

    /// <summary>
    /// Identity equality — two <see cref="PooledArray{T}" /> instances are equal
    /// iff they wrap the same backing array reference and have the same
    /// <see cref="Count" />. For content-based comparison, use
    /// <c>a.Span.SequenceEqual(b.Span)</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(PooledArray<T> other) =>
        ReferenceEquals(_items, other._items) && Count == other.Count;

    public override bool Equals(object? obj) =>
        obj is PooledArray<T> other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(RuntimeHelpers.GetHashCode(_items), Count);

    public static bool operator ==(PooledArray<T> left, PooledArray<T> right) =>
        left.Equals(right);

    public static bool operator !=(PooledArray<T> left, PooledArray<T> right) =>
        !left.Equals(right);

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    static void ThrowIndexOutOfRange() =>
        throw new ArgumentOutOfRangeException("index");

    /// <summary>Value-type enumerator for <see cref="PooledArray{T}" />.</summary>
    public struct Enumerator
    {
        readonly T[]? _items;
        readonly int _count;
        int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(T[]? items, int count)
        {
            _items = items;
            _count = count;
            _index = -1;
        }

        /// <summary>Readonly reference to the current element. Avoids per-iteration copy for large <typeparamref name="T" />.</summary>
        public readonly ref readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
            get => ref Unsafe.Add(
                ref MemoryMarshal.GetArrayDataReference(_items!), (nint)(uint)_index);
#else
            get => ref _items![_index];
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => ++_index < _count;
    }
}
