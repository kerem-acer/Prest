using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Prest;

/// <summary>
/// Stack-only, growable list backed by <see cref="ArrayPool{T}.Shared" />.
/// Compiler-enforced single-consumer: cannot be stored in a field, boxed, or
/// captured across <c>await</c>.
/// </summary>
/// <remarks>
/// <para>
/// Use <c>using var list = new StackOnlyPooledList&lt;T&gt;(8);</c> for automatic return.
/// Accepts an inline <see cref="Span{T}"/> (typically <c>stackalloc</c>) that avoids any
/// pool interaction until the inline buffer overflows.
/// </para>
/// <para>
/// For pool-backed storage that survives <c>await</c> boundaries or needs to live in a
/// field, use <see cref="PooledList{T}" /> (class) or <see cref="ValuePooledList{T}" /> (struct).
/// </para>
/// </remarks>
[DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
public ref struct StackOnlyPooledList<T> : IDisposable
{
    Span<T> _span;
    T[]? _rented;
    readonly bool _clearOnReturn;

    public StackOnlyPooledList(int initialCapacity, bool clearOnReturn = false)
    {
        _rented = initialCapacity > 0
            ? ArrayPool<T>.Shared.Rent(initialCapacity)
            : null;
        _span = _rented;
        Count = 0;
        _clearOnReturn = clearOnReturn;
    }

    /// <summary>
    /// Creates a list that uses <paramref name="initialBuffer"/> (typically stackalloc)
    /// and only rents from <see cref="ArrayPool{T}.Shared"/> when the buffer is full.
    /// </summary>
    public StackOnlyPooledList(Span<T> initialBuffer, bool clearOnReturn = false)
    {
        _span = initialBuffer;
        _rented = null;
        Count = 0;
        _clearOnReturn = clearOnReturn;
    }

    /// <summary>Number of elements.</summary>
    public int Count { get; private set; }

    /// <summary>Total slots available before the next grow.</summary>
    public readonly int Capacity => _span.Length;

    /// <summary>Returns a span over the valid elements.</summary>
    public readonly ReadOnlySpan<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
        get => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetReference(_span), Count);
#else
        get => _span[..Count];
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        var span = _span;
        var pos = Count;
        if ((uint)pos >= (uint)span.Length)
        {
            AddWithGrow(item);
            return;
        }

#if NET6_0_OR_GREATER
        Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint)(uint)pos) = item;
#else
        span[pos] = item;
#endif
        Count = pos + 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void AddWithGrow(T item)
    {
        Grow(0);
        var span = _span;
        var pos = Count;
#if NET6_0_OR_GREATER
        Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint)(uint)pos) = item;
#else
        span[pos] = item;
#endif
        Count = pos + 1;
    }

    /// <summary>Appends all elements of <paramref name="items" /> in one grow.</summary>
    public void AddRange(ReadOnlySpan<T> items)
    {
        if (items.IsEmpty)
        {
            return;
        }

        var needed = Count + items.Length;
        if (needed > _span.Length)
        {
            Grow(needed);
        }

        items.CopyTo(_span.Slice(Count));
        Count = needed;
    }

    public readonly ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)Count)
            {
                ThrowIndexOutOfRange();
            }

#if NET6_0_OR_GREATER
            return ref Unsafe.Add(ref MemoryMarshal.GetReference(_span), (nint)(uint)index);
#else
            return ref _span[index];
#endif
        }
    }

    /// <summary>
    /// Returns the zero-based index of the first occurrence of <paramref name="item" />,
    /// or <c>-1</c> if not found. Uses <see cref="EqualityComparer{T}.Default" />.
    /// </summary>
    public readonly int IndexOf(T item)
    {
        var span = _span;
        var count = Count;
        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < count; i++)
        {
            if (comparer.Equals(span[i], item))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Returns <see langword="true" /> if <paramref name="item" /> is present.
    /// Uses <see cref="EqualityComparer{T}.Default" />.
    /// </summary>
    public readonly bool Contains(T item) => IndexOf(item) >= 0;

    /// <summary>
    /// Copies the elements to a freshly-allocated array. The returned array is
    /// owned by the caller — not tied to this instance's pool lifetime.
    /// </summary>
    public readonly T[] ToArray() => Span.ToArray();

    /// <summary>
    /// Sorts the elements in-place using <see cref="Comparer{T}.Default" />. Mutates contents.
    /// </summary>
    public readonly void Sort()
    {
        if (Count < 2)
        {
            return;
        }

#if NET5_0_OR_GREATER
        _span[..Count].Sort();
#else
        SortFallback();
#endif
    }

    /// <summary>Reverses the order of elements in-place. Mutates contents.</summary>
    public readonly void Reverse()
    {
        if (Count < 2)
        {
            return;
        }

#if NETSTANDARD2_0
        ReverseFallback();
#else
        _span[..Count].Reverse();
#endif
    }

#if !NET5_0_OR_GREATER
    readonly void SortFallback()
    {
        var rented = _rented;
        if (rented is not null)
        {
            Array.Sort(rented, 0, Count);
            return;
        }

        var tmp = ArrayPool<T>.Shared.Rent(Count);
        _span[..Count].CopyTo(tmp);
        Array.Sort(tmp, 0, Count);
        tmp.AsSpan(0, Count).CopyTo(_span);
        ArrayPool<T>.Shared.Return(tmp, _clearOnReturn);
    }
#endif

#if NETSTANDARD2_0
    readonly void ReverseFallback()
    {
        var rented = _rented;
        if (rented is not null)
        {
            Array.Reverse(rented, 0, Count);
            return;
        }

        var tmp = ArrayPool<T>.Shared.Rent(Count);
        _span[..Count].CopyTo(tmp);
        Array.Reverse(tmp, 0, Count);
        tmp.AsSpan(0, Count).CopyTo(_span);
        ArrayPool<T>.Shared.Return(tmp, _clearOnReturn);
    }
#endif

    /// <summary>
    /// Resets <see cref="Count" /> to zero. Keeps the current buffer (stack or rented).
    /// For reference-containing <typeparamref name="T" />, clears the slots to release
    /// element references for GC.
    /// </summary>
    public void Clear()
    {
        var count = Count;
        Count = 0;
        if (count == 0)
        {
            return;
        }

#if NET
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            _span[..count].Clear();
        }
#else
        _span[..count].Clear();
#endif
    }

    public void Dispose()
    {
        var arr = _rented;
        _rented = null;
        _span = default;
        Count = 0;
        if (arr is not null)
        {
            ArrayPool<T>.Shared.Return(arr, _clearOnReturn);
        }
    }

    /// <summary>
    /// Transfers ownership of the backing array to a <see cref="PooledArray{T}" />.
    /// This instance becomes empty — do not use after calling.
    /// </summary>
    public PooledArray<T> ToPooledArray()
    {
        if (_rented is null)
        {
            if (Count == 0)
            {
                _span = default;
                return default;
            }

            var arr = ArrayPool<T>.Shared.Rent(Count);
            _span[..Count].CopyTo(arr);
            var result = new PooledArray<T>(arr, Count);
            _span = default;
            Count = 0;
            return result;
        }
        else
        {
            var result = new PooledArray<T>(_rented, Count);
            _rented = null;
            _span = default;
            Count = 0;
            return result;
        }
    }

    /// <summary>
    /// Hands over the rented backing array and count. The list becomes empty —
    /// do not use after calling. The caller is responsible for returning
    /// <paramref name="rentedArray"/> to <see cref="ArrayPool{T}.Shared" />.
    /// When the list was backed by a stack buffer, <paramref name="rentedArray"/>
    /// is <see langword="null" />.
    /// </summary>
    public void DetachArray(out T[]? rentedArray, out int count)
    {
        rentedArray = _rented;
        count = Count;
        _rented = null;
        _span = default;
        Count = 0;
    }

    /// <summary>Returns an enumerator over the valid elements.</summary>
    public readonly Enumerator GetEnumerator() => new(_span, Count);

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    static void ThrowIndexOutOfRange() =>
        throw new ArgumentOutOfRangeException("index");

    [MethodImpl(MethodImplOptions.NoInlining)]
    void Grow(int minCapacity)
    {
        var doubled = _span.Length == 0 ? 4 : _span.Length * 2;
        var newLen = Math.Max(doubled, minCapacity);
        var newArray = ArrayPool<T>.Shared.Rent(newLen);
        if (Count > 0)
        {
            _span[..Count].CopyTo(newArray);
        }
        if (_rented is not null)
        {
            ArrayPool<T>.Shared.Return(_rented, _clearOnReturn);
        }

        _rented = newArray;
        _span = newArray;
    }

    /// <summary>Stack-only enumerator for <see cref="StackOnlyPooledList{T}" />.</summary>
    public ref struct Enumerator
    {
        readonly Span<T> _span;
        readonly int _count;
        int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(Span<T> span, int count)
        {
            _span = span;
            _count = count;
            _index = -1;
        }

        /// <summary>Mutable reference to the current element.</summary>
        public readonly ref T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
            get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_span), (nint)(uint)_index);
#else
            get => ref _span[_index];
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => ++_index < _count;
    }
}
