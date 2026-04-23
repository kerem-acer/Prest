namespace Prest.Examples;

/// <summary>
/// The four pluggable algorithms. Same API, different internals — the wrapper
/// specializes on the algorithm struct and the JIT monomorphizes every call end-to-end.
/// </summary>
/// <remarks>
/// Rough guidance:
/// <list type="bullet">
///   <item><description><b>Swiss</b> — default. SIMD 16-byte group scan. Best all-round.</description></item>
///   <item><description><b>Linear</b> — simplest. Scalar probe. Good for small N or debugging.</description></item>
///   <item><description><b>RobinHood</b> — backward-shift deletion keeps probe distances monotonic. Best under heavy churn.</description></item>
///   <item><description><b>Chained</b> — separate chaining with dense entries. Best for iteration-heavy loads.</description></item>
/// </list>
/// </remarks>
static class Algorithms
{
    public static void Run()
    {
        PopulateAndDump("Swiss", PooledHashMap<int, int>.Create(32));
        PopulateAndDump("Linear", LinearHashMap<int, int>.Create(32));
        PopulateAndDump("RobinHood", RobinHoodHashMap<int, int>.Create(32));
        PopulateAndDump("Chained", ChainedHashMap<int, int>.Create(32));
    }

    static void PopulateAndDump<TAlgo>(string label, PooledHashMap<int, int, TAlgo> map)
        where TAlgo : struct, IHashAlgorithm<KeyValueSlot<int, int>, int>
    {
        using (map)
        {
            for (var i = 0; i < 10; i++)
            {
                map.Add(i, i * i);
            }

            var sum = 0;
            foreach (var kvp in map)
            {
                sum += kvp.Value;
            }
            Console.WriteLine($"{label,-10} count={map.Count} sum(v)={sum}");
        }
    }
}
