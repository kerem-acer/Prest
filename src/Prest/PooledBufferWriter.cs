using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
#if !NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Prest;

/// <summary>
/// <see cref="IBufferWriter{T}" /> backed by <see cref="ArrayPool{T}.Shared" />.
/// Disposing returns the rented array to the pool.
/// </summary>
/// <remarks>
/// Exposes a <see cref="Rent" />/<see cref="Return" /> pair backed by a
/// <see cref="ThreadStaticAttribute" /> slot for single-consumer, same-thread
/// instance reuse. For pooling that survives <c>await</c> boundaries, use the
/// <c>Prest.ObjectPool</c> package.
/// </remarks>
[DebuggerDisplay("Written = {_written}, Capacity = {Capacity}")]
public sealed class PooledBufferWriter<T> : IBufferWriter<T>, IDisposable
{
    const int DefaultInitialCapacity = 16;

    [ThreadStatic]
    static PooledBufferWriter<T>? _threadCache;

    T[]? _buffer;
    int _written;
    bool _disposed;

    public PooledBufferWriter(int initialCapacity)
    {
        if (initialCapacity <= 0)
        {
            return;
        }

        _buffer = ArrayPool<T>.Shared.Rent(initialCapacity);
    }

    public PooledBufferWriter() { }

    /// <summary>
    /// Returns a writer from the per-thread cache (or allocates a new one if the
    /// cache is empty). Pair with <see cref="Return" />.
    /// </summary>
    public static PooledBufferWriter<T> Rent()
    {
        var cached = _threadCache;
        _threadCache = null;
        return cached ?? new PooledBufferWriter<T>();
    }

    /// <summary>
    /// Resets <paramref name="writer" /> and places it into the per-thread cache
    /// for later reuse by <see cref="Rent" />. Do not pass a disposed writer.
    /// </summary>
    public static void Return(PooledBufferWriter<T> writer)
    {
        writer.Reset();
        _threadCache = writer;
    }

    /// <summary>Capacity of the rented buffer (element count). Returns 0 when no buffer is rented.</summary>
    public int Capacity => _buffer?.Length ?? 0;

    public ArraySegment<T> Buffer
    {
        get
        {
            ThrowIfDisposed();
            return _buffer is null
                ? new ArraySegment<T>([])
                : new ArraySegment<T>(_buffer, 0, _written);
        }
    }

    public ReadOnlyMemory<T> WrittenMemory
    {
        get
        {
            ThrowIfDisposed();
            return _buffer.AsMemory(0, _written);
        }
    }

    /// <summary>Read-only view of the written elements.</summary>
    public ReadOnlySpan<T> WrittenSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            if (_written == 0)
            {
                return [];
            }

#if NET6_0_OR_GREATER
            return MemoryMarshal.CreateReadOnlySpan(
                ref MemoryMarshal.GetArrayDataReference(_buffer!), _written);
#else
            return new ReadOnlySpan<T>(_buffer, 0, _written);
#endif
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        ThrowIfDisposed();
        _written += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        ThrowIfDisposed();
        var buf = _buffer;
        var needed = sizeHint <= 0 ? 1 : sizeHint;
        if (buf is not null && needed <= buf.Length - _written)
        {
            return buf.AsMemory(_written);
        }

        return GetMemorySlow(sizeHint);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    Memory<T> GetMemorySlow(int sizeHint)
    {
        Grow(sizeHint);
        return _buffer.AsMemory(_written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan(int sizeHint = 0)
    {
        ThrowIfDisposed();
        var buf = _buffer;
        var needed = sizeHint <= 0 ? 1 : sizeHint;
        if (buf is not null && needed <= buf.Length - _written)
        {
#if NET6_0_OR_GREATER
            return MemoryMarshal.CreateSpan(
                ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buf), (nint)(uint)_written),
                buf.Length - _written);
#else
            return buf.AsSpan(_written);
#endif
        }

        return GetSpanSlow(sizeHint);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    Span<T> GetSpanSlow(int sizeHint)
    {
        Grow(sizeHint);
#if NET6_0_OR_GREATER
        var buf = _buffer!;
        return MemoryMarshal.CreateSpan(
            ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buf), (nint)(uint)_written),
            buf.Length - _written);
#else
        return _buffer.AsSpan(_written);
#endif
    }

    /// <summary>
    /// Returns the underlying buffer as an <see cref="ArraySegment{T}"/> and
    /// relinquishes ownership. The caller is responsible for returning the
    /// array to <see cref="ArrayPool{T}.Shared"/>. Subsequent <see cref="Dispose"/>
    /// becomes a safe no-op.
    /// </summary>
    public ArraySegment<T> DetachBuffer()
    {
        ThrowIfDisposed();

        if (_buffer is null)
        {
            return new ArraySegment<T>([]);
        }

        var segment = new ArraySegment<T>(_buffer, 0, _written);
        _buffer = null;
        _written = 0;
        return segment;
    }

    /// <summary>
    /// Rewinds the written position to zero without releasing the rented buffer.
    /// Subsequent writes reuse the same backing storage. For full teardown use
    /// <see cref="Reset" /> (releases buffer to pool) or <see cref="Dispose" />.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();
        _written = 0;
    }

    /// <summary>
    /// Returns any rented buffer to <see cref="ArrayPool{T}.Shared"/> and resets
    /// write position to zero. Unlike <see cref="Dispose"/>, the instance remains
    /// usable — the next write rents a fresh buffer.
    /// </summary>
    public void Reset()
    {
        ThrowIfDisposed();
        var buf = _buffer;
        _buffer = null;
        _written = 0;
        if (buf is not null)
        {
#if NET
            ArrayPool<T>.Shared.Return(buf, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
#else
            ArrayPool<T>.Shared.Return(buf, clearArray: true);
#endif
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var buf = _buffer;
        _buffer = null;
        _written = 0;
        if (buf is not null)
        {
#if NET
            ArrayPool<T>.Shared.Return(buf, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
#else
            ArrayPool<T>.Shared.Return(buf, clearArray: true);
#endif
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ThrowIfDisposed()
    {
#if NET8_0_OR_GREATER
        ObjectDisposedException.ThrowIf(_disposed, this);
#else
        if (_disposed)
        {
            ThrowDisposed();
        }
#endif
    }

#if !NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    static void ThrowDisposed() =>
        throw new ObjectDisposedException(nameof(PooledBufferWriter<>));
#endif

    [MethodImpl(MethodImplOptions.NoInlining)]
    void Grow(int sizeHint)
    {
        if (sizeHint <= 0)
        {
            sizeHint = 1;
        }

        var currentLen = _buffer?.Length ?? 0;
        var doubled = currentLen == 0 ? DefaultInitialCapacity : currentLen * 2;
        var newSize = Math.Max(doubled, _written + sizeHint);
        var newBuf = ArrayPool<T>.Shared.Rent(newSize);

        var oldBuf = _buffer;
        if (oldBuf is not null)
        {
            if (_written > 0)
            {
                oldBuf.AsSpan(0, _written).CopyTo(newBuf);
            }
#if NET
            ArrayPool<T>.Shared.Return(oldBuf, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
#else
            ArrayPool<T>.Shared.Return(oldBuf, clearArray: true);
#endif
        }

        _buffer = newBuf;
    }
}
