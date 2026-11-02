namespace Prest.Tests;

/// <summary>
/// Stateless struct hasher that treats integers with the same last decimal digit as equal,
/// so tests can tell whether a hasher alias really uses the supplied <see cref="IHasher{T}" />.
/// </summary>
readonly struct LastDigitHasher : IHasher<int>
{
    public uint ComputeHash(int key) => (uint)(key % 10);

    public bool Equals(int x, int y) => x % 10 == y % 10;
}
