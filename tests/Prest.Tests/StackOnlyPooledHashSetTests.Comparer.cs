namespace Prest.Tests;

public partial class StackOnlyPooledHashSetTests
{
    [Test]
    public async Task Constructor_WithComparer_UsesItForEquality()
    {
        // Arrange
        const string Item = "Hello";
        const string OtherCaseItem = "HELLO";
        var comparer = StringComparer.OrdinalIgnoreCase;
        bool addedAgain, contains;
        int count;
        IEqualityComparer<string> reported;
        {
            using var set = new StackOnlyPooledHashSet<string>(4, comparer);

            // Act
            set.Add(Item);
            addedAgain = set.Add(OtherCaseItem);
            contains = set.Contains(OtherCaseItem);
            count = set.Count;
            reported = set.Comparer;
        }

        // Assert
        await Assert.That(addedAgain).IsFalse();
        await Assert.That(contains).IsTrue();
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(reported).IsSameReferenceAs(comparer);
    }

    [Test]
    public async Task Comparer_DefaultOrNullComparer_IsDefaultComparer()
    {
        // Arrange
        IEqualityComparer<string> fromDefault, fromNull;
        {
            // Act
            var unset = default(StackOnlyPooledHashSet<string>);
            using var withNull = new StackOnlyPooledHashSet<string>(4, null);
            fromDefault = unset.Comparer;
            fromNull = withNull.Comparer;
        }

        // Assert
        await Assert.That(fromDefault).IsSameReferenceAs(EqualityComparer<string>.Default);
        await Assert.That(fromNull).IsSameReferenceAs(EqualityComparer<string>.Default);
    }
}
