namespace Prest;

/// <summary>
/// Extracts the key stored inside a slot. <see langword="readonly struct" />
/// implementations fully inline at every call site.
/// </summary>
public interface ISlotKeyExtractor<TSlot, out TKey>
{
    TKey Extract(in TSlot slot);
}
