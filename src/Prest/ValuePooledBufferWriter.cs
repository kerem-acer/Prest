using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Prest;

/// <summary>
/// Stack-only growable buffer writer backed by an inline <see cref="Span{T}" /> (typically
/// <c>stackalloc</c>) that promotes to <see cref="ArrayPool{T}.Shared" /> on overflow.
/// Compiler-enforced single-consumer: cannot be stored in a field, boxed, or captured
/// across <c>await</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Copy hazard:</b> assigning this struct to another local copies all fields. Two copies
/// can share the same rented array with independent <see cref="WrittenCount" /> counters and
/// corrupt state. Keep a single stack-local instance; pass it only as <see langword="ref" />
/// to helpers.
/// </para>
/// <para>
/// <b>Modes:</b>
/// <list type="number">
/// <item>
/// <see cref="ValuePooledBufferWriter{T}(Span{T})" /> — write into the provided span first,
/// then rent from <see cref="ArrayPool{T}.Shared" /> on overflow.
/// </item>
/// <item>
/// <see cref="ValuePooledBufferWriter{T}(int)" /> — rent immediately (same growth policy as
/// <see cref="PooledBufferWriter{T}" />).
/// </item>
/// </list>
/// </para>
/// <para>
/// On .NET 10+, implements <see cref="IBufferWriter{T}" /> for use in generic contexts where
/// <c>where TWriter : allows ref struct, IBufferWriter&lt;T&gt;</c>. On older runtimes, use
/// <see cref="PooledBufferWriter{T}" /> where an interface instance is required.
/// </para>
/// </remarks>
[DebuggerDisplay("Written = {WrittenCount}, Capacity = {Capacity}")]
#if NET10_0_OR_GREATER
public ref struct ValuePooledBufferWriter<T> : IBufferWriter<T>, IDisposable
#else
public ref struct ValuePooledBufferWriter<T> : IDisposable
#endif
{
    const int DefaultInitialCapacity = 16;

    Span<T> _span;
    T[]? _rented;
    int _written;

    /// <summary>
    /// Creates a writer with no initial buffer. The first write rents from
    /// <see cref="ArrayPool{T}.Shared" />.
    /// </summary>
    public ValuePooledBufferWriter()
    {
        _span = default;
        _rented = null;
        _written = 0;
    }

    /// <summary>
    /// Rents an initial buffer of at least <paramref name="initialCapacity" /> elements
    /// from <see cref="ArrayPool{T}.Shared" />.
    /// </summary>
    public ValuePooledBufferWriter(int initialCapacity)
    {
        if (initialCapacity > 0)
        {
            _rented = ArrayPool<T>.Shared.Rent(initialCapacity);
            _span = _rented;
        }
        else
        {
            _rented = null;
            _span = default;
        }
        _written = 0;
    }

    /// <summary>
    /// Writes first into <paramref name="initialInline" /> (typically <c>stackalloc</c>);
    /// on overflow promotes to a pooled array.
    /// </summary>
    public ValuePooledBufferWriter(Span<T> initialInline)
    {
        _span = initialInline;
        _rented = null;
        _written = 0;
    }

    /// <summary>Number of elements written so far.</summary>
    public readonly int WrittenCount => _written;

    /// <summary>Total slots available before the next grow.</summary>
    public readonly int Capacity => _span.Length;

    /// <summary>
    /// <see langword="true" /> when the writer has promoted into a pooled array;
    /// <see langword="false" /> when output still fits in the initial inline span.
    /// </summary>
    public readonly bool UsesPooledBacking => _rented is not null;

    /// <summary>Read-only view of the written elements.</summary>
    public readonly ReadOnlySpan<T> WrittenSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
        get => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetReference(_span), _written);
#else
        get => _span[.._written];
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count) => _written += count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan(int sizeHint = 0)
    {
        var span = _span;
        var written = _written;
        var needed = sizeHint <= 0 ? 1 : sizeHint;
        if (needed <= span.Length - written)
        {
#if NET6_0_OR_GREATER
            return MemoryMarshal.CreateSpan(
                ref Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint)(uint)written),
                span.Length - written);
#else
            return span[written..];
#endif
        }

        return GetSpanSlow(sizeHint);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    Span<T> GetSpanSlow(int sizeHint)
    {
        Grow(sizeHint);
#if NET6_0_OR_GREATER
        return MemoryMarshal.CreateSpan(
            ref Unsafe.Add(ref MemoryMarshal.GetReference(_span), (nint)(uint)_written),
            _span.Length - _written);
#else
        return _span[_written..];
#endif
    }

#if NET10_0_OR_GREATER
    /// <inheritdoc />
    /// <remarks>
    /// Throws when the writer is backed by an inline (non-array) span. Use
    /// <see cref="GetSpan" /> from <see cref="ValuePooledBufferWriter{T}" /> directly,
    /// or construct with the <see cref="ValuePooledBufferWriter{T}(int)" /> ctor to
    /// guarantee array backing.
    /// </remarks>
    Memory<T> IBufferWriter<T>.GetMemory(int sizeHint)
    {
        var needed = sizeHint <= 0 ? 1 : sizeHint;
        if (_rented is null || needed > _rented.Length - _written)
        {
            Grow(sizeHint);
        }

        if (_rented is null)
        {
            throw new NotSupportedException(
                "GetMemory is not supported for inline-backed ValuePooledBufferWriter<T>. Use GetSpan or rent up front.");
        }

        return _rented.AsMemory(_written);
    }
#endif

    /// <summary>
    /// Returns the written region as an <see cref="ArraySegment{T}" /> and empties the
    /// writer. Caller owns the returned array — pool it via
    /// <see cref="ArrayPool{T}.Return(T[], bool)" /> when finished. When the writer is
    /// inline-backed, copies the written elements into a freshly rented array.
    /// Subsequent <see cref="Dispose" /> is a safe no-op.
    /// </summary>
    public ArraySegment<T> DetachBuffer()
    {
        var written = _written;
        var rented = _rented;

        if (written == 0)
        {
            if (rented is not null)
            {
#if NET
                ArrayPool<T>.Shared.Return(rented, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
#else
                ArrayPool<T>.Shared.Return(rented, clearArray: true);
#endif
            }

            _rented = null;
            _span = default;
            _written = 0;
            return new ArraySegment<T>([]);
        }

        if (rented is not null)
        {
            var seg = new ArraySegment<T>(rented, 0, written);
            _rented = null;
            _span = default;
            _written = 0;
            return seg;
        }

        var copy = ArrayPool<T>.Shared.Rent(written);
        _span[..written].CopyTo(copy);
        _span = default;
        _written = 0;
        return new ArraySegment<T>(copy, 0, written);
    }

    /// <summary>
    /// Rewinds the written position to zero without releasing the buffer. For
    /// reference-containing <typeparamref name="T" />, clears the slots to release
    /// element references for GC. For full teardown use <see cref="Reset" /> or
    /// <see cref="Dispose" />.
    /// </summary>
    public void Clear()
    {
        var written = _written;
        _written = 0;
        if (written == 0)
        {
            return;
        }

#if NET
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            _span[..written].Clear();
        }
#else
        _span[..written].Clear();
#endif
    }

    /// <summary>
    /// Returns any rented buffer to <see cref="ArrayPool{T}.Shared" />, drops the inline
    /// buffer, and resets write position to zero. Unlike <see cref="Dispose" />, the
    /// instance remains usable — the next write rents a fresh buffer.
    /// </summary>
    public void Reset()
    {
        var arr = _rented;
        _rented = null;
        _span = default;
        _written = 0;
        if (arr is not null)
        {
#if NET
            ArrayPool<T>.Shared.Return(arr, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
#else
            ArrayPool<T>.Shared.Return(arr, clearArray: true);
#endif
        }
    }

    public void Dispose()
    {
        var arr = _rented;
        _rented = null;
        _span = default;
        _written = 0;
        if (arr is not null)
        {
#if NET
            ArrayPool<T>.Shared.Return(arr, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
#else
            ArrayPool<T>.Shared.Return(arr, clearArray: true);
#endif
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void Grow(int sizeHint)
    {
        if (sizeHint <= 0)
        {
            sizeHint = 1;
        }

        var currentLen = _span.Length;
        var doubled = currentLen == 0 ? DefaultInitialCapacity : currentLen * 2;
        var newSize = Math.Max(doubled, _written + sizeHint);
        var newBuf = ArrayPool<T>.Shared.Rent(newSize);

        if (_written > 0)
        {
            _span[.._written].CopyTo(newBuf);
        }

        if (_rented is not null)
        {
#if NET
            ArrayPool<T>.Shared.Return(_rented, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
#else
            ArrayPool<T>.Shared.Return(_rented, clearArray: true);
#endif
        }

        _rented = newBuf;
        _span = newBuf;
    }
}
