namespace Prest.Examples;

/// <summary>
/// Minimal <c>PooledHashMap&lt;K,V&gt;</c> usage — add, look up, iterate, dispose.
/// </summary>
static class BasicMap
{
    public static void Run()
    {
        // `using` disposes the map and returns pooled arrays to ArrayPool.Shared.
        using var map = PooledHashMap<int, string>.Create(capacity: 16);

        map.Add(1, "one");
        map.Add(2, "two");
        map.Add(3, "three");

        Console.WriteLine($"Count: {map.Count}");
        Console.WriteLine($"map[2] = {map[2]}");

        if (map.TryGetValue(3, out var three))
        {
            Console.WriteLine($"TryGetValue(3) -> {three}");
        }

        map.Remove(1);
        Console.WriteLine($"After Remove(1), Count: {map.Count}, ContainsKey(1): {map.ContainsKey(1)}");
    }
}
