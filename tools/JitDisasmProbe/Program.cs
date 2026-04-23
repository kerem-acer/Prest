// JIT disassembly probe.
// Runs with DOTNET_JitDisasm set to match the Swiss/DenseMap lookup methods.

using System.Runtime.CompilerServices;
using Faster.Map.Core;
using Prest;

var map = PooledHashMap.Swiss<int, int>(256);
var dense = new DenseMap<int, int>(256u);

for (var i = 0; i < 200; i++)
{
    map.Add(i, i * 2);
    dense.Insert(i, i * 2);
}

int sum = 0;
for (var i = 0; i < 200; i++)
{
    sum += Probe.SwissLookup(map, i);
    sum += Probe.DenseLookup(dense, i);
}
Console.WriteLine($"sum: {sum}");
map.Dispose();

static class Probe
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int SwissLookup(
        PooledHashMap<int, int, SwissTableAlgorithm<KeyValueSlot<int, int>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>> map,
        int key)
    {
        return map.TryGetValue(key, out var v) ? v : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int DenseLookup(DenseMap<int, int> dense, int key)
    {
        return dense.Get(key, out var v) ? v : 0;
    }
}
