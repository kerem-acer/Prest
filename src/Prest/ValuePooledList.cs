using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Prest;

/// <summary>
/// Growable list backed by <see cref="ArrayPool{T}.Shared" />, wrapped as a
/// <see langword="struct" />. Zero wrapper allocation — copies can be passed by value
/// and stored in fields.
/// </summary>
/// <remarks>
/// <para>
/// <b>Copy hazard:</b> mutating this struct (<see cref="Add" />, <see cref="Dispose" />,
/// <see cref="ToPooledArray" />) updates fields in the calling copy only. However,
/// copies share the same rented <c>T[]</c> — writing through one copy is visible to
/// all, and only one copy should <see cref="Dispose" /> the backing array. Single-consumer
/// ownership discipline required.
/// </para>
/// <para>
/// For compiler-enforced stack-only semantics use <see cref="StackOnlyPooledList{T}" />.
/// For heap-allocated, safely shareable semantics use <see cref="PooledList{T}" />.
/// </para>
/// </remarks>
[DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
public struct ValuePooledList<T> : IDisposable
{
    T[] _rented;
    int _count;
    readonly bool _clearOnReturn;

    public ValuePooledList(int initialCapacity = 8, bool clearOnReturn = false)
    {
        _rented = initialCapacity > 0
            ? ArrayPool<T>.Shared.Rent(initialCapacity)
            : [];
        _count = 0;
        _clearOnReturn = clearOnReturn;
    }

    /// <summary>Number of elements.</summary>
    public readonly int Count => _count;

    /// <summary>Total slots available before the next grow.</summary>
    public readonly int Capacity => _rented.Length;

    /// <summary>Returns a span over the valid elements. Branchless — empty when <see cref="Count" /> is zero.</summary>
    public readonly ReadOnlySpan<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
        get => MemoryMarshal.CreateReadOnlySpan(
            ref MemoryMarshal.GetArrayDataReference(_rented), _count);
#else
        get => _rented.AsSpan(0, _count);
#endif
    }

    /// <summary>Returns a <see cref="ReadOnlyMemory{T}" /> view over the valid elements.</summary>
    public readonly ReadOnlyMemory<T> AsMemory() => _rented.AsMemory(0, _count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        var arr = _rented;
        var pos = _count;
        if ((uint)pos >= (uint)arr.Length)
        {
            AddWithGrow(item);
            return;
        }

#if NET6_0_OR_GREATER
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arr), (nint)(uint)pos) = item;
#else
        arr[pos] = item;
#endif
        _count = pos + 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void AddWithGrow(T item)
    {
        Grow(0);
        var arr = _rented;
        var pos = _count;
#if NET6_0_OR_GREATER
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arr), (nint)(uint)pos) = item;
#else
        arr[pos] = item;
#endif
        _count = pos + 1;
    }

    /// <summary>Appends all elements of <paramref name="items" /> in one grow.</summary>
    public void AddRange(ReadOnlySpan<T> items)
    {
        if (items.IsEmpty)
        {
            return;
        }

        var needed = _count + items.Length;
        if (needed > _rented.Length)
        {
            Grow(needed);
        }

        items.CopyTo(_rented.AsSpan(_count));
        _count = needed;
    }

    public readonly ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_count)
            {
                ThrowIndexOutOfRange();
            }

#if NET6_0_OR_GREATER
            return ref Unsafe.Add(
                ref MemoryMarshal.GetArrayDataReference(_rented), (nint)(uint)index);
#else
            return ref _rented[index];
#endif
        }
    }

    /// <summary>
    /// Returns the zero-based index of the first occurrence of <paramref name="item" />,
    /// or <c>-1</c> if not found. Uses <see cref="EqualityComparer{T}.Default" />.
    /// </summary>
    public readonly int IndexOf(T item) => Array.IndexOf(_rented, item, 0, _count);

    /// <summary>
    /// Returns <see langword="true" /> if <paramref name="item" /> is present.
    /// Uses <see cref="EqualityComparer{T}.Default" />.
    /// </summary>
    public readonly bool Contains(T item) => IndexOf(item) >= 0;

    /// <summary>
    /// Copies the elements to a freshly-allocated array. The returned array is owned
    /// by the caller — not tied to this instance's pool lifetime.
    /// </summary>
    public readonly T[] ToArray() => Span.ToArray();

    /// <summary>
    /// Sorts the elements in-place using <see cref="Comparer{T}.Default" />. Mutates contents
    /// visible through all copies that share the backing array.
    /// </summary>
    public readonly void Sort()
    {
        if (_count < 2)
        {
            return;
        }

        Array.Sort(_rented, 0, _count);
    }

    /// <summary>
    /// Reverses the order of elements in-place. Mutates contents visible through all
    /// copies that share the backing array.
    /// </summary>
    public readonly void Reverse()
    {
        if (_count < 2)
        {
            return;
        }

        Array.Reverse(_rented, 0, _count);
    }

    /// <summary>
    /// Resets <see cref="Count" /> to zero. Keeps the rented buffer attached so subsequent
    /// <see cref="Add" /> calls skip a pool rent. For reference-containing <typeparamref name="T" />,
    /// clears slots to release element references for GC.
    /// </summary>
    public void Clear()
    {
        var count = _count;
        _count = 0;
        if (count == 0)
        {
            return;
        }

#if NET
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(_rented, 0, count);
        }
#else
        Array.Clear(_rented, 0, count);
#endif
    }

    public void Dispose()
    {
        var arr = _rented;
        _rented = [];
        _count = 0;
        ArrayPool<T>.Shared.Return(arr, _clearOnReturn);
    }

    /// <summary>
    /// Transfers ownership of the backing array to a <see cref="PooledArray{T}" />.
    /// This instance becomes empty — do not use after calling.
    /// </summary>
    public PooledArray<T> ToPooledArray()
    {
        var arr = _rented;
        var count = _count;
        _rented = [];
        _count = 0;
        return count == 0 ? default : new PooledArray<T>(arr, count);
    }

    /// <summary>
    /// Hands over the rented backing array and count. The list becomes empty —
    /// do not use after calling. The caller is responsible for returning
    /// <paramref name="rentedArray"/> to <see cref="ArrayPool{T}.Shared" />.
    /// </summary>
    public void DetachArray(out T[] rentedArray, out int count)
    {
        rentedArray = _rented;
        count = _count;
        _rented = [];
        _count = 0;
    }

    /// <summary>Returns an enumerator over the valid elements.</summary>
    public readonly Enumerator GetEnumerator() => new(_rented, _count);

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    static void ThrowIndexOutOfRange() =>
        throw new ArgumentOutOfRangeException("index");

    [MethodImpl(MethodImplOptions.NoInlining)]
    void Grow(int minCapacity)
    {
        var arr = _rented;
        var doubled = arr.Length == 0 ? 4 : arr.Length * 2;
        var newLen = Math.Max(doubled, minCapacity);
        var newArray = ArrayPool<T>.Shared.Rent(newLen);
        if (_count > 0)
        {
            arr.AsSpan(0, _count).CopyTo(newArray);
        }
        ArrayPool<T>.Shared.Return(arr, _clearOnReturn);
        _rented = newArray;
    }

    /// <summary>Value-type enumerator for <see cref="ValuePooledList{T}" />.</summary>
    public struct Enumerator
    {
        readonly T[] _rented;
        readonly int _count;
        int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(T[] rented, int count)
        {
            _rented = rented;
            _count = count;
            _index = -1;
        }

        /// <summary>Mutable reference to the current element.</summary>
        public readonly ref T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
            get => ref Unsafe.Add(
                ref MemoryMarshal.GetArrayDataReference(_rented), (nint)(uint)_index);
#else
            get => ref _rented[_index];
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => ++_index < _count;
    }
}
