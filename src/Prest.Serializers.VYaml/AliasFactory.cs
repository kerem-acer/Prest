namespace Prest.Serializers.VYaml;

static class AliasFactory
{
    internal static NotSupportedException NoFactory(Type collectionType, string formatterName) =>
        new($"Cannot deserialize {collectionType}: it has no public static Create(int) that returns it "
            + "(Comparer* aliases need their comparer, which is not part of the payload). Construct "
            + $"{formatterName} with a factory delegate, e.g. capacity => "
            + "ComparerSwissHashMap<string, int>.Create(StringComparer.OrdinalIgnoreCase, capacity), "
            + "and resolve it ahead of PooledTypeFormatterResolver.");
}
