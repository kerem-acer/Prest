namespace Prest.Examples;

/// <summary>
/// Hash finalizers are bit-mixing stages applied after the key's raw
/// <see cref="object.GetHashCode" />. Useful when keys have poor distribution
/// (sequential ints, identity-hashed refs, structs hashing just one field).
/// </summary>
/// <remarks>
/// Finalizers ship under <c>Prest.*Finalizer</c>:
/// <list type="bullet">
///   <item><description><b>NoOpHashFinalizer</b> — no mixing (default). Fast when keys are already well-distributed.</description></item>
///   <item><description><b>FibonacciFinalizer</b> — single multiply by the golden ratio. Cheap + effective for sequential ints.</description></item>
///   <item><description><b>Lowbias32Finalizer</b> — Wellons' two-multiply variant. Stronger mixing.</description></item>
///   <item><description><b>XmxFinalizer</b> — xorshift-multiply-xorshift. Strongest; use for adversarial input.</description></item>
/// </list>
/// </remarks>
static class Finalizers
{
    public static void Run()
    {
        // Stride-heavy keys that would cluster with raw GetHashCode — Fibonacci spreads them out.
        using var fib = SwissHashMap<int, int, FibonacciFinalizer>.Create(capacity: 64);
        for (var i = 0; i < 32; i++)
        {
            fib.Add(i * 1024, i);
        }
        Console.WriteLine($"Fibonacci finalizer — count: {fib.Count}");

        using var xmx = RobinHoodHashMap<int, int, XmxFinalizer>.Create(capacity: 64);
        for (var i = 0; i < 32; i++)
        {
            xmx.Add(i, i);
        }
        Console.WriteLine($"XMX finalizer (RobinHood) — count: {xmx.Count}");
    }
}
