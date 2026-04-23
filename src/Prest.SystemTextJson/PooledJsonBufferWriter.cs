using System.Text.Json;

namespace Prest.SystemTextJson;

/// <summary>
/// Pairs a <see cref="Utf8JsonWriter" /> with a <see cref="PooledBufferWriter{T}" />.
/// </summary>
/// <remarks>
/// <para>
/// Disposing this instance <b>resets</b> both the writer and the buffer (returning
/// the byte[] to <see cref="System.Buffers.ArrayPool{T}.Shared" />) but does not recycle
/// <c>this</c>. For instance-level pooling, use
/// <see cref="PooledJsonBufferWriterCache" /> (threadstatic, same-thread reuse) or
/// <see cref="Prest.SystemTextJson" />'s <c>.ObjectPool</c> sibling package.
/// </para>
/// </remarks>
public sealed class PooledJsonBufferWriter : IDisposable
{
    readonly PooledBufferWriter<byte> _buffer;
    readonly Utf8JsonWriter _writer;
    readonly bool _indented;

    public PooledJsonBufferWriter(bool indented = false)
    {
        _indented = indented;
        _buffer = new PooledBufferWriter<byte>();
        _writer = new Utf8JsonWriter(_buffer, new JsonWriterOptions { Indented = indented });
    }

    /// <summary>The wrapped JSON writer. Call <see cref="Utf8JsonWriter.Flush" /> before reading buffered bytes.</summary>
    public Utf8JsonWriter Writer => _writer;

    /// <summary>Bytes written so far (after calling <see cref="Utf8JsonWriter.Flush" />).</summary>
    public ReadOnlyMemory<byte> WrittenMemory => _buffer.WrittenMemory;

    /// <summary>True if this writer was constructed with <c>indented: true</c>.</summary>
    public bool IsIndented => _indented;

    /// <summary>Flushes the writer and returns a view over the buffered bytes.</summary>
    public ReadOnlyMemory<byte> FlushAndGetWrittenMemory()
    {
        _writer.Flush();
        return _buffer.WrittenMemory;
    }

    /// <summary>
    /// Flushes the writer and hands over ownership of the backing array to the caller.
    /// The caller is responsible for returning the array to <see cref="System.Buffers.ArrayPool{T}.Shared" />.
    /// </summary>
    public ArraySegment<byte> FlushAndDetachBuffer()
    {
        _writer.Flush();
        return _buffer.DetachBuffer();
    }

    /// <summary>
    /// Resets the writer and the buffer so this instance can be reused for another
    /// serialization. Returns any rented byte[] to the pool.
    /// </summary>
    public void Reset()
    {
        _writer.Reset();
        _buffer.Reset();
    }

    /// <summary>
    /// Equivalent to <see cref="Reset" /> — returns the byte[] to the pool. Does not
    /// recycle <c>this</c>; callers that want instance reuse must use one of the
    /// accessor classes (<see cref="PooledJsonBufferWriterCache" />).
    /// </summary>
    public void Dispose() => Reset();
}
