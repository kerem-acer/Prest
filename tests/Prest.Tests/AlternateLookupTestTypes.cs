using System.Text;
using System.Text.Unicode;

namespace Prest.Tests;

/// <summary>
/// Looks up <see cref="string" /> keys by their UTF-8 bytes. Agrees with
/// <see cref="EqualityComparer{T}.Default" /> and <see cref="StringComparer.Ordinal" /> because it hashes
/// and compares the UTF-16 form of the bytes. Test keys are short, so the stack buffer always fits.
/// </summary>
readonly struct Utf8StringHasher : IAlternateHasher<ReadOnlySpan<byte>, string>
{
    const int MaxChars = 128;

    public uint ComputeHash(ReadOnlySpan<byte> alternate)
    {
        Span<char> buffer = stackalloc char[MaxChars];
        Utf8.ToUtf16(alternate, buffer, out _, out var written);
        return (uint)string.GetHashCode(buffer[..written]);
    }

    public bool Equals(ReadOnlySpan<byte> alternate, string other)
    {
        Span<char> buffer = stackalloc char[MaxChars];
        Utf8.ToUtf16(alternate, buffer, out _, out var written);
        return buffer[..written].SequenceEqual(other);
    }

    public string Create(ReadOnlySpan<byte> alternate) => Encoding.UTF8.GetString(alternate);
}

/// <summary>
/// Custom struct hasher for <see cref="string" /> keys, hashing like <see cref="EqualityComparer{T}.Default" />.
/// Prest can't see a comparer behind it, so only struct-hasher alternate lookups work with it.
/// </summary>
readonly struct OrdinalStringHasher : IHasher<string>
{
    public uint ComputeHash(string key) => (uint)string.GetHashCode(key.AsSpan());

    public bool Equals(string x, string y) => string.Equals(x, y, StringComparison.Ordinal);
}

/// <summary>Ordinal string comparer with span support that counts how many keys it creates.</summary>
sealed class CountingSpanComparer : IEqualityComparer<string>, IAlternateEqualityComparer<ReadOnlySpan<char>, string>
{
    public int CreateCount { get; private set; }

    public bool Equals(string? x, string? y) => string.Equals(x, y, StringComparison.Ordinal);

    public int GetHashCode(string obj) => string.GetHashCode(obj.AsSpan());

    public bool Equals(ReadOnlySpan<char> alternate, string other) => alternate.SequenceEqual(other);

    public int GetHashCode(ReadOnlySpan<char> alternate) => string.GetHashCode(alternate);

    public string Create(ReadOnlySpan<char> alternate)
    {
        CreateCount++;
        return alternate.ToString();
    }
}

/// <summary>
/// Compares <see cref="ReadOnlyMemory{T}" /> keys by their bytes and finds them by a
/// <see cref="ReadOnlySpan{T}" /> of the same bytes. Counts how many keys it creates.
/// </summary>
sealed class ByteContentComparer : IEqualityComparer<ReadOnlyMemory<byte>>, IAlternateEqualityComparer<ReadOnlySpan<byte>, ReadOnlyMemory<byte>>
{
    public int CreateCount { get; private set; }

    public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y) => x.Span.SequenceEqual(y.Span);

    public int GetHashCode(ReadOnlyMemory<byte> obj) => GetHashCode(obj.Span);

    public bool Equals(ReadOnlySpan<byte> alternate, ReadOnlyMemory<byte> other) => alternate.SequenceEqual(other.Span);

    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        var hash = new HashCode();
        hash.AddBytes(alternate);
        return hash.ToHashCode();
    }

    public ReadOnlyMemory<byte> Create(ReadOnlySpan<byte> alternate)
    {
        CreateCount++;
        return alternate.ToArray();
    }
}

/// <summary>Ordinal string comparer that does not implement <see cref="IAlternateEqualityComparer{TAlternate,T}" />.</summary>
sealed class NoAlternateComparer : IEqualityComparer<string>
{
    public bool Equals(string? x, string? y) => string.Equals(x, y, StringComparison.Ordinal);

    public int GetHashCode(string obj) => string.GetHashCode(obj.AsSpan());
}
