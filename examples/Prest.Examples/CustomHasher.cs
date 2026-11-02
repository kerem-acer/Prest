namespace Prest.Examples;

/// <summary>
/// Plug a stateless struct <see cref="IHasher{T}" /> into a map or set with the hasher alias
/// (<c>{Algo}HashMap&lt;K,V,THasher,TFinalizer&gt;</c> / <c>{Algo}HashSet&lt;T,THasher,TFinalizer&gt;</c>).
/// Unlike an <see cref="IEqualityComparer{T}" />, the JIT inlines the struct hasher into every call.
/// </summary>
static class CustomHasher
{
    public static void Run()
    {
        using var headers = SwissHashMap<string, string, IgnoreCaseHasher, NoOpHashFinalizer>.Create(capacity: 8);
        headers.Add("Content-Type", "application/json");
        Console.WriteLine($"headers[\"content-type\"] = {headers["content-type"]}");

        using var seen = LinearHashSet<string, IgnoreCaseHasher, NoOpHashFinalizer>.Create();
        seen.Add("Alpha");
        var addedAgain = seen.Add("ALPHA");
        Console.WriteLine($"Add(\"ALPHA\") after \"Alpha\" returned: {addedAgain}");
    }

    /// <summary>Case-insensitive string hasher. Must be stateless: the alias uses <c>default(THasher)</c>.</summary>
    readonly struct IgnoreCaseHasher : IHasher<string>
    {
        public uint ComputeHash(string key) => (uint)string.GetHashCode(key, StringComparison.OrdinalIgnoreCase);

        public bool Equals(string x, string y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
    }
}
