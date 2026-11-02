using VYaml.Annotations;

// Outside Prest.Serializers: the VYaml source generator emits unqualified `VYaml.*` names,
// which would bind to the Prest.Serializers.VYaml namespace from inside it.
namespace Prest.YamlTestModels;

[YamlObject]
public sealed partial class AliasDocument : IDisposable
{
    public PooledHashMap<string, int>? Default { get; set; }
    public SwissHashMap<string, int>? Swiss { get; set; }
    public RobinHoodHashMap<string, int, FibonacciFinalizer>? RobinHood { get; set; }
    public ChainedHashMap<int, int, EqualityDefaultHasher<int>, XmxFinalizer>? Chained { get; set; }
    public PooledHashSet<string>? Tags { get; set; }
    public LinearHashSet<int, FibonacciFinalizer>? Numbers { get; set; }

    public void Dispose()
    {
        Default?.Dispose();
        Swiss?.Dispose();
        RobinHood?.Dispose();
        Chained?.Dispose();
        Tags?.Dispose();
        Numbers?.Dispose();
    }
}

[YamlObject]
public sealed partial class ComparerAliasDocument : IDisposable
{
    public ComparerSwissHashMap<string, int>? Map { get; set; }
    public ComparerRobinHoodHashSet<string>? Set { get; set; }

    public void Dispose()
    {
        Map?.Dispose();
        Set?.Dispose();
    }
}
