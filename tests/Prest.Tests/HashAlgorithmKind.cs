namespace Prest.Tests;

/// <summary>
/// Selects one of the four hashtable algorithms for tests that run the same body
/// against every algorithm via <c>[Arguments(HashAlgorithmKind.X)]</c>.
/// </summary>
public enum HashAlgorithmKind
{
    Swiss,
    RobinHood,
    Linear,
    Chained,
}
