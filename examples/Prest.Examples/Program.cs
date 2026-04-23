using Prest.Examples;

var examples = new (string Name, Action Run)[]
{
    ("basic-map", BasicMap.Run),
    ("basic-set", BasicSet.Run),
    ("algorithms", Algorithms.Run),
    ("custom-comparer", CustomComparer.Run),
    ("finalizers", Finalizers.Run),
    ("enumeration", Enumeration.Run),
};

if (args.Length == 0)
{
    foreach (var (name, runAll) in examples)
    {
        Console.WriteLine($"=== {name} ===");
        runAll();
        Console.WriteLine();
    }
    return;
}

var (_, run) = Array.Find(examples, e => e.Name == args[0]);
run();
