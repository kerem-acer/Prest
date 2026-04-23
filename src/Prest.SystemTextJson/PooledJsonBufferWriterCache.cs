namespace Prest.SystemTextJson;

/// <summary>
/// Threadstatic instance cache for <see cref="PooledJsonBufferWriter" />. Separate
/// slots for compact vs indented writers. Same-thread only — do not hold a rented
/// writer across <c>await</c> boundaries; use the <c>Prest.SystemTextJson.ObjectPool</c>
/// package for that.
/// </summary>
public static class PooledJsonBufferWriterCache
{
    [ThreadStatic] static PooledJsonBufferWriter? s_compactSlot;
    [ThreadStatic] static PooledJsonBufferWriter? s_indentedSlot;

    /// <summary>Returns a writer from the threadstatic slot matching <paramref name="indented" />, or allocates a new one.</summary>
    public static PooledJsonBufferWriter Rent(bool indented = false)
    {
        if (indented)
        {
            var cached = s_indentedSlot;
            s_indentedSlot = null;
            return cached ?? new PooledJsonBufferWriter(indented: true);
        }
        else
        {
            var cached = s_compactSlot;
            s_compactSlot = null;
            return cached ?? new PooledJsonBufferWriter(indented: false);
        }
    }

    /// <summary>Resets <paramref name="writer" /> and places it into the matching threadstatic slot.</summary>
    public static void Return(PooledJsonBufferWriter writer)
    {
        writer.Reset();
        if (writer.IsIndented)
        {
            s_indentedSlot = writer;
        }
        else
        {
            s_compactSlot = writer;
        }
    }
}
