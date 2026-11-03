namespace Prest.Tests;

public partial class StackOnlyPooledHashMapTests
{
    [Test]
    public async Task Constructor_WithComparer_UsesItForKeyEquality()
    {
        // Arrange
        const string Key = "Hello";
        const string OtherCaseKey = "HELLO";
        const int Value = 1;
        var comparer = StringComparer.OrdinalIgnoreCase;
        bool found, addedAgain;
        int value, count;
        IEqualityComparer<string> reported;
        {
            using var map = new StackOnlyPooledHashMap<string, int>(4, comparer);

            // Act
            map.Add(Key, Value);
            found = map.TryGetValue(OtherCaseKey, out value);
            addedAgain = map.TryAdd(OtherCaseKey, Value);
            count = map.Count;
            reported = map.Comparer;
        }

        // Assert
        await Assert.That(found).IsTrue();
        await Assert.That(value).IsEqualTo(Value);
        await Assert.That(addedAgain).IsFalse();
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(reported).IsSameReferenceAs(comparer);
    }

    [Test]
    public async Task Comparer_DefaultOrNullComparer_IsDefaultComparer()
    {
        // Arrange
        IEqualityComparer<string> fromDefault, fromCapacity, fromNull;
        {
            // Act
            var unset = default(StackOnlyPooledHashMap<string, int>);
            using var sized = new StackOnlyPooledHashMap<string, int>(4);
            using var withNull = new StackOnlyPooledHashMap<string, int>(4, null);
            fromDefault = unset.Comparer;
            fromCapacity = sized.Comparer;
            fromNull = withNull.Comparer;
        }

        // Assert
        await Assert.That(fromDefault).IsSameReferenceAs(EqualityComparer<string>.Default);
        await Assert.That(fromCapacity).IsSameReferenceAs(EqualityComparer<string>.Default);
        await Assert.That(fromNull).IsSameReferenceAs(EqualityComparer<string>.Default);
    }
}
