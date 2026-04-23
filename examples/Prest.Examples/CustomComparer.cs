namespace Prest.Examples;

/// <summary>
/// Wire a custom <see cref="IEqualityComparer{T}" /> into the map by passing a
/// <see cref="ComparerHasher{T}" /> to the factory.
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
    }
}
