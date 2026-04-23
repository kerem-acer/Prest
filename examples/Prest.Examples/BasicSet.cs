namespace Prest.Examples;

/// <summary>
/// Minimal <c>PooledHashSet&lt;T&gt;</c> usage.
/// </summary>
static class BasicSet
{
    public static void Run()
    {
        using var set = PooledHashSet<string>.Create(capacity: 8);

        set.Add("apple");
        set.Add("banana");
        set.Add("cherry");
        var dup = set.Add("apple"); // false — already present
        Console.WriteLine($"Add(\"apple\") again returned: {dup}");

        Console.WriteLine($"Count: {set.Count}");
        Console.WriteLine($"Contains(\"banana\"): {set.Contains("banana")}");
        Console.WriteLine($"Contains(\"grape\"): {set.Contains("grape")}");

        set.Remove("cherry");
        Console.WriteLine($"After Remove(\"cherry\"), Count: {set.Count}");
    }
}
