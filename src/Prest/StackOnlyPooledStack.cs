using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Prest;

/// <summary>
/// Stack-only, growable last-in-first-out stack backed by <see cref="ArrayPool{T}.Shared" />.
/// Compiler-enforced single-consumer: cannot be stored in a field, boxed, or captured
/// across <c>await</c>.
/// </summary>
/// <remarks>
/// <para>
/// Use <c>using var stack = new StackOnlyPooledStack&lt;T&gt;(8);</c> for automatic return.
/// Accepts an inline <see cref="Span{T}"/> (typically <c>stackalloc</c>) that avoids any
/// pool interaction until the inline buffer overflows — an iterative tree walk of typical
/// depth never touches the pool.
/// </para>
/// <para>
/// For pool-backed storage that survives <c>await</c> boundaries or needs to live in a
/// field, use <see cref="PooledStack{T}" />.
/// </para>
/// </remarks>
[DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
public ref struct StackOnlyPooledStack<T> : IDisposable
{
    Span<T> _span;
    T[]? _rented;
    readonly bool _clearOnReturn;

    public StackOnlyPooledStack(int initialCapacity, bool clearOnReturn = false)
    {
        _rented = initialCapacity > 0
            ? ArrayPool<T>.Shared.Rent(initialCapacity)
            : null;
        _span = _rented;
        Count = 0;
        _clearOnReturn = clearOnReturn;
    }

    /// <summary>
    /// Creates a stack that uses <paramref name="initialBuffer"/> (typically stackalloc)
    /// and only rents from <see cref="ArrayPool{T}.Shared"/> when the buffer is full.
    /// </summary>
    public StackOnlyPooledStack(Span<T> initialBuffer, bool clearOnReturn = false)
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

    /// <summary>
    /// Returns a span over the valid elements in push order — the bottom of the stack is at
    /// index <c>0</c>, the top at <c>Count - 1</c>. Enumeration runs the opposite way.
    /// </summary>
    public readonly ReadOnlySpan<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
        get => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetReference(_span), Count);
#else
        get => _span[..Count];
#endif
    }

    /// <summary>Inserts <paramref name="item" /> at the top of the stack.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(T item)
    {
        var span = _span;
        var pos = Count;
        if ((uint)pos >= (uint)span.Length)
        {
            PushWithGrow(item);
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
    void PushWithGrow(T item)
    {
        Grow();
        var span = _span;
        var pos = Count;
#if NET6_0_OR_GREATER
        Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint)(uint)pos) = item;
#else
        span[pos] = item;
#endif
        Count = pos + 1;
    }

    /// <summary>
    /// Removes and returns the element at the top of the stack. For reference-containing
    /// <typeparamref name="T" />, clears the vacated slot to release the reference for GC.
    /// </summary>
    /// <exception cref="InvalidOperationException">The stack is empty.</exception>
    public T Pop()
    {
        var span = _span;
        var pos = Count - 1;
        // pos == -1 wraps to uint.MaxValue, so one unsigned compare covers the empty case.
        if ((uint)pos >= (uint)span.Length)
        {
            ThrowEmpty();
        }

        Count = pos;
#if NET6_0_OR_GREATER
        ref var slot = ref Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint)(uint)pos);
#else
        ref var slot = ref span[pos];
#endif
        var item = slot;
#if NET
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            slot = default;
        }
#else
        slot = default;
#endif
        return item;
    }

    /// <summary>
    /// Removes the element at the top of the stack into <paramref name="result" />.
    /// Returns <see langword="false" /> when the stack is empty.
    /// </summary>
    public bool TryPop([MaybeNullWhen(false)] out T result)
    {
        if (Count == 0)
        {
            result = default;
            return false;
        }

        result = Pop();
        return true;
    }

    /// <summary>Returns the element at the top of the stack without removing it.</summary>
    /// <exception cref="InvalidOperationException">The stack is empty.</exception>
    public readonly T Peek()
    {
        var span = _span;
        var pos = Count - 1;
        if ((uint)pos >= (uint)span.Length)
        {
            ThrowEmpty();
        }

#if NET6_0_OR_GREATER
        return Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint)(uint)pos);
#else
        return span[pos];
#endif
    }

    /// <summary>
    /// Copies the element at the top of the stack into <paramref name="result" /> without
    /// removing it. Returns <see langword="false" /> when the stack is empty.
    /// </summary>
    public readonly bool TryPeek([MaybeNullWhen(false)] out T result)
    {
        if (Count == 0)
        {
            result = default;
            return false;
        }

        result = Peek();
        return true;
    }

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
    /// Returns an enumerator that walks from the top of the stack to the bottom — the same
    /// order <see cref="Pop" /> would return the elements, matching <see cref="Stack{T}" />.
    /// </summary>
    public readonly Enumerator GetEnumerator() => new(_span, Count);

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    static void ThrowEmpty() =>
        throw new InvalidOperationException("Stack is empty.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    void Grow()
    {
        var newLen = _span.Length == 0 ? 4 : _span.Length * 2;
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

    /// <summary>Stack-only enumerator for <see cref="StackOnlyPooledStack{T}" />, top to bottom.</summary>
    public ref struct Enumerator
    {
        readonly Span<T> _span;
        int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(Span<T> span, int count)
        {
            _span = span;
            _index = count;
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
        public bool MoveNext() => --_index >= 0;
    }
}
