using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Prest;

/// <summary>
/// Growable list backed by <see cref="ArrayPool{T}.Shared" />. Heap-allocated
/// wrapper — can be stored in fields, passed by reference, and used across
/// <c>await</c> boundaries.
/// </summary>
/// <remarks>
/// Use <c>using var list = new PooledList&lt;T&gt;(8);</c> for automatic return.
/// For zero-allocation hot paths, prefer
/// <see cref="StackOnlyPooledList{T}" /> (ref struct) or
/// <see cref="ValuePooledList{T}" /> (struct).
/// </remarks>
[DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
public sealed class PooledList<T> : IDisposable, IEquatable<PooledList<T>>
{
    T[] _rented;
    int _count;
    readonly bool _clearOnReturn;

    public PooledList(int initialCapacity = 8, bool clearOnReturn = false)
    {
        _rented = initialCapacity > 0
            ? ArrayPool<T>.Shared.Rent(initialCapacity)
            : [];
        _count = 0;
        _clearOnReturn = clearOnReturn;
    }

    /// <summary>Number of elements.</summary>
    public int Count => _count;

    /// <summary>Total slots available before the next grow. Not the same as <see cref="Count" />.</summary>
    public int Capacity => _rented.Length;

    /// <summary>Returns a span over the valid elements. Branchless — empty when <see cref="Count" /> is zero.</summary>
    public ReadOnlySpan<T> Span
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
    public ReadOnlyMemory<T> AsMemory() => _rented.AsMemory(0, _count);

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

    public ref T this[int index]
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
    public int IndexOf(T item) => Array.IndexOf(_rented, item, 0, _count);

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
        if (_count < 2)
        {
            return;
        }

#if NET6_0_OR_GREATER
        MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(_rented), _count).Sort();
#else
        Array.Sort(_rented, 0, _count);
#endif
    }

    /// <summary>Reverses the order of elements in-place. Mutates contents.</summary>
    public void Reverse()
    {
        if (_count < 2)
        {
            return;
        }

#if NET6_0_OR_GREATER
        MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(_rented), _count).Reverse();
#else
        Array.Reverse(_rented, 0, _count);
#endif
    }

    /// <summary>
    /// Resets <see cref="Count" /> to zero. Keeps the rented buffer attached so
    /// subsequent <see cref="Add" /> calls skip a pool rent. For reference-containing
    /// <typeparamref name="T" />, clears the slots to release element references for GC.
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
    public Enumerator GetEnumerator() => new(_rented, _count);

    /// <summary>
    /// Identity equality — two <see cref="PooledList{T}" /> instances are equal iff
    /// they refer to the same object. Matches default reference-type semantics;
    /// declared explicitly for API consistency with <see cref="PooledArray{T}" />.
    /// </summary>
    public bool Equals(PooledList<T>? other) => ReferenceEquals(this, other);

    public override bool Equals(object? obj) => ReferenceEquals(this, obj);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    public static bool operator ==(PooledList<T>? left, PooledList<T>? right) =>
        ReferenceEquals(left, right);

    public static bool operator !=(PooledList<T>? left, PooledList<T>? right) =>
        !ReferenceEquals(left, right);

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

    /// <summary>Value-type enumerator for <see cref="PooledList{T}" />.</summary>
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

        /// <summary>Mutable reference to the current element. Supports <c>foreach (ref var x in list)</c>.</summary>
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
