using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Prest;

/// <summary>
/// Growable last-in-first-out stack backed by <see cref="ArrayPool{T}.Shared" />.
/// Heap-allocated wrapper — can be stored in fields, passed by reference, and used
/// across <c>await</c> boundaries.
/// </summary>
/// <remarks>
/// Use <c>using var stack = new PooledStack&lt;T&gt;(8);</c> for automatic return.
/// For zero-allocation hot paths, prefer <see cref="StackOnlyPooledStack{T}" /> (ref struct).
/// </remarks>
[DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
public sealed class PooledStack<T> : IDisposable
{
    T[] _rented;
    int _count;
    readonly bool _clearOnReturn;

    public PooledStack(int initialCapacity = 8, bool clearOnReturn = false)
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

    /// <summary>
    /// Returns a span over the valid elements in push order — the bottom of the stack is at
    /// index <c>0</c>, the top at <c>Count - 1</c>. Enumeration runs the opposite way.
    /// </summary>
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

    /// <summary>Inserts <paramref name="item" /> at the top of the stack.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(T item)
    {
        var arr = _rented;
        var pos = _count;
        if ((uint)pos >= (uint)arr.Length)
        {
            PushWithGrow(item);
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
    void PushWithGrow(T item)
    {
        Grow();
        var arr = _rented;
        var pos = _count;
#if NET6_0_OR_GREATER
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arr), (nint)(uint)pos) = item;
#else
        arr[pos] = item;
#endif
        _count = pos + 1;
    }

    /// <summary>
    /// Removes and returns the element at the top of the stack. For reference-containing
    /// <typeparamref name="T" />, clears the vacated slot to release the reference for GC.
    /// </summary>
    /// <exception cref="InvalidOperationException">The stack is empty.</exception>
    public T Pop()
    {
        var arr = _rented;
        var pos = _count - 1;
        // pos == -1 wraps to uint.MaxValue, so one unsigned compare covers the empty case.
        if ((uint)pos >= (uint)arr.Length)
        {
            ThrowEmpty();
        }

        _count = pos;
#if NET6_0_OR_GREATER
        ref var slot = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arr), (nint)(uint)pos);
#else
        ref var slot = ref arr[pos];
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
        if (_count == 0)
        {
            result = default;
            return false;
        }

        result = Pop();
        return true;
    }

    /// <summary>Returns the element at the top of the stack without removing it.</summary>
    /// <exception cref="InvalidOperationException">The stack is empty.</exception>
    public T Peek()
    {
        var arr = _rented;
        var pos = _count - 1;
        if ((uint)pos >= (uint)arr.Length)
        {
            ThrowEmpty();
        }

#if NET6_0_OR_GREATER
        return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arr), (nint)(uint)pos);
#else
        return arr[pos];
#endif
    }

    /// <summary>
    /// Copies the element at the top of the stack into <paramref name="result" /> without
    /// removing it. Returns <see langword="false" /> when the stack is empty.
    /// </summary>
    public bool TryPeek([MaybeNullWhen(false)] out T result)
    {
        if (_count == 0)
        {
            result = default;
            return false;
        }

        result = Peek();
        return true;
    }

    /// <summary>
    /// Resets <see cref="Count" /> to zero. Keeps the rented buffer attached so
    /// subsequent <see cref="Push" /> calls skip a pool rent. For reference-containing
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
    /// Returns an enumerator that walks from the top of the stack to the bottom — the same
    /// order <see cref="Pop" /> would return the elements, matching <see cref="Stack{T}" />.
    /// </summary>
    public Enumerator GetEnumerator() => new(_rented, _count);

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    static void ThrowEmpty() =>
        throw new InvalidOperationException("Stack is empty.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    void Grow()
    {
        var arr = _rented;
        var newLen = arr.Length == 0 ? 4 : arr.Length * 2;
        var newArray = ArrayPool<T>.Shared.Rent(newLen);
        if (_count > 0)
        {
            arr.AsSpan(0, _count).CopyTo(newArray);
        }
        ArrayPool<T>.Shared.Return(arr, _clearOnReturn);
        _rented = newArray;
    }

    /// <summary>Value-type enumerator for <see cref="PooledStack{T}" />, top to bottom.</summary>
    public struct Enumerator
    {
        readonly T[] _rented;
        int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(T[] rented, int count)
        {
            _rented = rented;
            _index = count;
        }

        /// <summary>Mutable reference to the current element. Supports <c>foreach (ref var x in stack)</c>.</summary>
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
        public bool MoveNext() => --_index >= 0;
    }
}
