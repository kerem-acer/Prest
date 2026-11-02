namespace Prest.Examples;

/// <summary>
/// Wire a custom <see cref="IEqualityComparer{T}" /> into the map with a <c>Comparer*</c>
/// alias. <c>Create</c> takes the comparer; <c>Comparer</c> reads it back.
/// </summary>
static class CustomComparer
{
    public static void Run()
    {
        using var map = ComparerSwissHashMap<string, int>.Create(StringComparer.OrdinalIgnoreCase, capacity: 8);

        map.Add("Hello", 1);
        map.Add("World", 2);

        Console.WriteLine($"ContainsKey(\"HELLO\"): {map.ContainsKey("HELLO")}");
        Console.WriteLine($"map[\"world\"] = {map["world"]}");
        Console.WriteLine($"Comparer is OrdinalIgnoreCase: {map.Comparer == StringComparer.OrdinalIgnoreCase}");

        // Comparer + finalizer in one alias.
        using var ids = ComparerRobinHoodHashSet<string, FibonacciFinalizer>.Create(StringComparer.Ordinal);
        ids.Add("a");
        ids.Add("A");
        Console.WriteLine($"Ordinal set count after adding \"a\" and \"A\": {ids.Count}");
    }
}
