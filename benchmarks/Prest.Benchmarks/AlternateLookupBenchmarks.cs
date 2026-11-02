using System.Globalization;
using System.Text;
using System.Text.Unicode;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Prest.Benchmarks;

/// <summary>
/// Alternate-key lookups on <see cref="string" />-keyed maps, the way a parser does them: every
/// lookup key is a slice of one big buffer, so no <see cref="string" /> exists for it. Compares
/// <see cref="Dictionary{TKey,TValue}.GetAlternateLookup{TAlternate}" /> with Prest's
/// <c>GetAlternateLookup&lt;ReadOnlySpan&lt;char&gt;&gt;()</c> (through the map's comparer, like the
/// BCL) and <c>GetAlternateLookup&lt;TAlternate, TAltHasher&gt;()</c> (through a struct hasher, no
/// interface calls). The UTF-8 categories look up by <c>ReadOnlySpan&lt;byte&gt;</c>: the BCL needs a
/// dictionary built with a custom comparer for that, while Prest uses its default map plus a struct
/// hasher.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class AlternateLookupBenchmarks
{
    [Params(256, 4096, 65536)]
    public int N;

    char[] _hitChars = null!;
    char[] _missChars = null!;
    int[] _hitCharOffsets = null!;
    int[] _missCharOffsets = null!;
    byte[] _hitBytes = null!;
    byte[] _missBytes = null!;
    int[] _hitByteOffsets = null!;
    int[] _missByteOffsets = null!;

    Dictionary<string, int> _dict = null!;
    Dictionary<string, int> _dictUtf8 = null!;
    PooledHashMap<string, int, SwissTableAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>> _swiss = null!;
    PooledHashMap<string, int, RobinHoodAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>> _robin = null!;
    PooledHashMap<string, int, LinearProbingAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>> _linear = null!;
    PooledHashMap<string, int, ChainedAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>> _chained = null!;

    [GlobalSetup]
    public void Setup()
    {
        var present = new string[N];
        var missing = new string[N];
        for (var i = 0; i < N; i++)
        {
            present[i] = "key-" + i.ToString(CultureInfo.InvariantCulture);
            missing[i] = "absent-" + i.ToString(CultureInfo.InvariantCulture);
        }

        (_hitChars, _hitCharOffsets) = ConcatChars(present);
        (_missChars, _missCharOffsets) = ConcatChars(missing);
        (_hitBytes, _hitByteOffsets) = ConcatUtf8(present);
        (_missBytes, _missByteOffsets) = ConcatUtf8(missing);

        _dict = new Dictionary<string, int>(N);
        _dictUtf8 = new Dictionary<string, int>(N, new Utf8OrdinalComparer());
        _swiss = PooledHashMap<string, int>.Create(N);
        _robin = RobinHoodHashMap<string, int>.Create(N);
        _linear = LinearHashMap<string, int>.Create(N);
        _chained = ChainedHashMap<string, int>.Create(N);
        for (var i = 0; i < N; i++)
        {
            _dict.Add(present[i], i);
            _dictUtf8.Add(present[i], i);
            _swiss.Add(present[i], i);
            _robin.Add(present[i], i);
            _linear.Add(present[i], i);
            _chained.Add(present[i], i);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _swiss.Dispose();
        _robin.Dispose();
        _linear.Dispose();
        _chained.Dispose();
    }

    // ---- ReadOnlySpan<char>, hit ----

    [Benchmark(Baseline = true, Description = "Dictionary")]
    [BenchmarkCategory("Span hit")]
    public int Dictionary_SpanHit()
    {
        var lookup = _dict.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> chars = _hitChars;
        var offsets = _hitCharOffsets;
        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            if (lookup.TryGetValue(chars[offsets[i]..offsets[i + 1]], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    [Benchmark(Description = "Swiss")]
    [BenchmarkCategory("Span hit")]
    public int Swiss_SpanHit()
    {
        var lookup = _swiss.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> chars = _hitChars;
        var offsets = _hitCharOffsets;
        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            if (lookup.TryGetValue(chars[offsets[i]..offsets[i + 1]], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    [Benchmark(Description = "RobinHood")]
    [BenchmarkCategory("Span hit")]
    public int RobinHood_SpanHit()
    {
        var lookup = _robin.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> chars = _hitChars;
        var offsets = _hitCharOffsets;
        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            if (lookup.TryGetValue(chars[offsets[i]..offsets[i + 1]], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    [Benchmark(Description = "Linear")]
    [BenchmarkCategory("Span hit")]
    public int Linear_SpanHit()
    {
        var lookup = _linear.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> chars = _hitChars;
        var offsets = _hitCharOffsets;
        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            if (lookup.TryGetValue(chars[offsets[i]..offsets[i + 1]], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    [Benchmark(Description = "Chained")]
    [BenchmarkCategory("Span hit")]
    public int Chained_SpanHit()
    {
        var lookup = _chained.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> chars = _hitChars;
        var offsets = _hitCharOffsets;
        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            if (lookup.TryGetValue(chars[offsets[i]..offsets[i + 1]], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    [Benchmark(Description = "Swiss + struct hasher")]
    [BenchmarkCategory("Span hit")]
    public int SwissStructHasher_SpanHit()
    {
        var lookup = _swiss.GetAlternateLookup<ReadOnlySpan<char>, OrdinalSpanHasher>();
        ReadOnlySpan<char> chars = _hitChars;
        var offsets = _hitCharOffsets;
        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            if (lookup.TryGetValue(chars[offsets[i]..offsets[i + 1]], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    [Benchmark(Description = "RobinHood + struct hasher")]
    [BenchmarkCategory("Span hit")]
    public int RobinHoodStructHasher_SpanHit()
    {
        var lookup = _robin.GetAlternateLookup<ReadOnlySpan<char>, OrdinalSpanHasher>();
        ReadOnlySpan<char> chars = _hitChars;
        var offsets = _hitCharOffsets;
        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            if (lookup.TryGetValue(chars[offsets[i]..offsets[i + 1]], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    // ---- ReadOnlySpan<char>, miss ----

    [Benchmark(Baseline = true, Description = "Dictionary")]
    [BenchmarkCategory("Span miss")]
    public int Dictionary_SpanMiss()
    {
        var lookup = _dict.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> chars = _missChars;
        var offsets = _missCharOffsets;
        var count = 0;
        for (var i = 0; i < N; i++)
        {
            if (!lookup.TryGetValue(chars[offsets[i]..offsets[i + 1]], out _))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "Swiss")]
    [BenchmarkCategory("Span miss")]
    public int Swiss_SpanMiss()
    {
        var lookup = _swiss.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> chars = _missChars;
        var offsets = _missCharOffsets;
        var count = 0;
        for (var i = 0; i < N; i++)
        {
            if (!lookup.TryGetValue(chars[offsets[i]..offsets[i + 1]], out _))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "RobinHood")]
    [BenchmarkCategory("Span miss")]
    public int RobinHood_SpanMiss()
    {
        var lookup = _robin.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> chars = _missChars;
        var offsets = _missCharOffsets;
        var count = 0;
        for (var i = 0; i < N; i++)
        {
            if (!lookup.TryGetValue(chars[offsets[i]..offsets[i + 1]], out _))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "Linear")]
    [BenchmarkCategory("Span miss")]
    public int Linear_SpanMiss()
    {
        var lookup = _linear.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> chars = _missChars;
        var offsets = _missCharOffsets;
        var count = 0;
        for (var i = 0; i < N; i++)
        {
            if (!lookup.TryGetValue(chars[offsets[i]..offsets[i + 1]], out _))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "Chained")]
    [BenchmarkCategory("Span miss")]
    public int Chained_SpanMiss()
    {
        var lookup = _chained.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> chars = _missChars;
        var offsets = _missCharOffsets;
        var count = 0;
        for (var i = 0; i < N; i++)
        {
            if (!lookup.TryGetValue(chars[offsets[i]..offsets[i + 1]], out _))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "Swiss + struct hasher")]
    [BenchmarkCategory("Span miss")]
    public int SwissStructHasher_SpanMiss()
    {
        var lookup = _swiss.GetAlternateLookup<ReadOnlySpan<char>, OrdinalSpanHasher>();
        ReadOnlySpan<char> chars = _missChars;
        var offsets = _missCharOffsets;
        var count = 0;
        for (var i = 0; i < N; i++)
        {
            if (!lookup.TryGetValue(chars[offsets[i]..offsets[i + 1]], out _))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "RobinHood + struct hasher")]
    [BenchmarkCategory("Span miss")]
    public int RobinHoodStructHasher_SpanMiss()
    {
        var lookup = _robin.GetAlternateLookup<ReadOnlySpan<char>, OrdinalSpanHasher>();
        ReadOnlySpan<char> chars = _missChars;
        var offsets = _missCharOffsets;
        var count = 0;
        for (var i = 0; i < N; i++)
        {
            if (!lookup.TryGetValue(chars[offsets[i]..offsets[i + 1]], out _))
            {
                count++;
            }
        }
        return count;
    }

    // ---- UTF-8 ReadOnlySpan<byte>, hit ----

    [Benchmark(Baseline = true, Description = "Dictionary + UTF-8 comparer")]
    [BenchmarkCategory("UTF-8 hit")]
    public int Dictionary_Utf8Hit()
    {
        var lookup = _dictUtf8.GetAlternateLookup<ReadOnlySpan<byte>>();
        ReadOnlySpan<byte> bytes = _hitBytes;
        var offsets = _hitByteOffsets;
        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            if (lookup.TryGetValue(bytes[offsets[i]..offsets[i + 1]], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    [Benchmark(Description = "Swiss + UTF-8 struct hasher")]
    [BenchmarkCategory("UTF-8 hit")]
    public int Swiss_Utf8Hit()
    {
        var lookup = _swiss.GetAlternateLookup<ReadOnlySpan<byte>, Utf8OrdinalHasher>();
        ReadOnlySpan<byte> bytes = _hitBytes;
        var offsets = _hitByteOffsets;
        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            if (lookup.TryGetValue(bytes[offsets[i]..offsets[i + 1]], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    [Benchmark(Description = "RobinHood + UTF-8 struct hasher")]
    [BenchmarkCategory("UTF-8 hit")]
    public int RobinHood_Utf8Hit()
    {
        var lookup = _robin.GetAlternateLookup<ReadOnlySpan<byte>, Utf8OrdinalHasher>();
        ReadOnlySpan<byte> bytes = _hitBytes;
        var offsets = _hitByteOffsets;
        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            if (lookup.TryGetValue(bytes[offsets[i]..offsets[i + 1]], out var v))
            {
                sum += v;
            }
        }
        return sum;
    }

    // ---- UTF-8 ReadOnlySpan<byte>, miss ----

    [Benchmark(Baseline = true, Description = "Dictionary + UTF-8 comparer")]
    [BenchmarkCategory("UTF-8 miss")]
    public int Dictionary_Utf8Miss()
    {
        var lookup = _dictUtf8.GetAlternateLookup<ReadOnlySpan<byte>>();
        ReadOnlySpan<byte> bytes = _missBytes;
        var offsets = _missByteOffsets;
        var count = 0;
        for (var i = 0; i < N; i++)
        {
            if (!lookup.TryGetValue(bytes[offsets[i]..offsets[i + 1]], out _))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "Swiss + UTF-8 struct hasher")]
    [BenchmarkCategory("UTF-8 miss")]
    public int Swiss_Utf8Miss()
    {
        var lookup = _swiss.GetAlternateLookup<ReadOnlySpan<byte>, Utf8OrdinalHasher>();
        ReadOnlySpan<byte> bytes = _missBytes;
        var offsets = _missByteOffsets;
        var count = 0;
        for (var i = 0; i < N; i++)
        {
            if (!lookup.TryGetValue(bytes[offsets[i]..offsets[i + 1]], out _))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "RobinHood + UTF-8 struct hasher")]
    [BenchmarkCategory("UTF-8 miss")]
    public int RobinHood_Utf8Miss()
    {
        var lookup = _robin.GetAlternateLookup<ReadOnlySpan<byte>, Utf8OrdinalHasher>();
        ReadOnlySpan<byte> bytes = _missBytes;
        var offsets = _missByteOffsets;
        var count = 0;
        for (var i = 0; i < N; i++)
        {
            if (!lookup.TryGetValue(bytes[offsets[i]..offsets[i + 1]], out _))
            {
                count++;
            }
        }
        return count;
    }

    // Concatenates the keys into one buffer; key i is buffer[offsets[i]..offsets[i + 1]].
    static (char[] Chars, int[] Offsets) ConcatChars(string[] keys)
    {
        var offsets = new int[keys.Length + 1];
        for (var i = 0; i < keys.Length; i++)
        {
            offsets[i + 1] = offsets[i] + keys[i].Length;
        }
        return (string.Concat(keys).ToCharArray(), offsets);
    }

    static (byte[] Bytes, int[] Offsets) ConcatUtf8(string[] keys)
    {
        var offsets = new int[keys.Length + 1];
        for (var i = 0; i < keys.Length; i++)
        {
            offsets[i + 1] = offsets[i] + Encoding.UTF8.GetByteCount(keys[i]);
        }
        return (Encoding.UTF8.GetBytes(string.Concat(keys)), offsets);
    }

    /// <summary>
    /// <c>ReadOnlySpan&lt;char&gt;</c> hasher that agrees with <see cref="EqualityComparer{T}.Default" />
    /// for strings: same hash as <see cref="string.GetHashCode()" />, ordinal equality.
    /// </summary>
    readonly struct OrdinalSpanHasher : IAlternateHasher<ReadOnlySpan<char>, string>
    {
        public uint ComputeHash(ReadOnlySpan<char> alternate) => (uint)string.GetHashCode(alternate);

        public bool Equals(ReadOnlySpan<char> alternate, string other) => alternate.SequenceEqual(other);

        public string Create(ReadOnlySpan<char> alternate) => new(alternate);
    }

    /// <summary>
    /// UTF-8 hasher that agrees with <see cref="EqualityComparer{T}.Default" /> for strings: it
    /// transcodes to UTF-16 on the stack, then hashes and compares that.
    /// </summary>
    readonly struct Utf8OrdinalHasher : IAlternateHasher<ReadOnlySpan<byte>, string>
    {
        public uint ComputeHash(ReadOnlySpan<byte> alternate) => (uint)Utf8Ordinal.GetHashCode(alternate);

        public bool Equals(ReadOnlySpan<byte> alternate, string other) => Utf8Ordinal.Equals(alternate, other);

        public string Create(ReadOnlySpan<byte> alternate) => Encoding.UTF8.GetString(alternate);
    }

    /// <summary>The BCL equivalent of <see cref="Utf8OrdinalHasher" />, for <see cref="Dictionary{TKey,TValue}" />.</summary>
    sealed class Utf8OrdinalComparer : IEqualityComparer<string>, IAlternateEqualityComparer<ReadOnlySpan<byte>, string>
    {
        public bool Equals(string? x, string? y) => string.Equals(x, y, StringComparison.Ordinal);

        public int GetHashCode(string obj) => string.GetHashCode(obj.AsSpan());

        public bool Equals(ReadOnlySpan<byte> alternate, string other) => Utf8Ordinal.Equals(alternate, other);

        public int GetHashCode(ReadOnlySpan<byte> alternate) => Utf8Ordinal.GetHashCode(alternate);

        public string Create(ReadOnlySpan<byte> alternate) => Encoding.UTF8.GetString(alternate);
    }

    static class Utf8Ordinal
    {
        const int StackChars = 256;

        public static int GetHashCode(ReadOnlySpan<byte> utf8)
        {
            // A UTF-8 sequence never decodes to more UTF-16 chars than it has bytes.
            Span<char> buffer = utf8.Length <= StackChars ? stackalloc char[StackChars] : new char[utf8.Length];
            Utf8.ToUtf16(utf8, buffer, out _, out var written);
            return string.GetHashCode(buffer[..written]);
        }

        public static bool Equals(ReadOnlySpan<byte> utf8, string other)
        {
            // Each UTF-16 char takes 1–3 UTF-8 bytes.
            if (utf8.Length < other.Length || utf8.Length > other.Length * 3)
            {
                return false;
            }

            Span<char> buffer = utf8.Length <= StackChars ? stackalloc char[StackChars] : new char[utf8.Length];
            Utf8.ToUtf16(utf8, buffer, out _, out var written);
            return buffer[..written].SequenceEqual(other);
        }
    }
}
