namespace Prest.Examples;

/// <summary>
/// Enumerating entries, keys, and values. The map's <c>GetEnumerator</c> returns
/// a value-type <see cref="PooledHashMap{TKey,TValue,TAlgo}.Enumerator" /> — no
/// allocation, no boxing.
/// </summary>
static class Enumeration
{
    public static void Run()
    {
        using var map = PooledHashMap<string, int>.Create(capacity: 8);
        map.Add("alpha", 1);
        map.Add("beta", 2);
        map.Add("gamma", 3);

        Console.WriteLine("foreach (kvp):");
        foreach (var kvp in map)
        {
            Console.WriteLine($"  {kvp.Key} = {kvp.Value}");
        }

        Console.WriteLine("Keys:");
        foreach (var key in map.Keys)
        {
            Console.WriteLine($"  {key}");
        }

        Console.WriteLine("Values:");
        foreach (var value in map.Values)
        {
            Console.WriteLine($"  {value}");
        }
    }
}
