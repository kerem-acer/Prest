namespace Prest.Examples;

/// <summary>
/// Every alias's <c>Create</c> returns the alias itself, so maps and sets can live in fields
/// (and parameters) declared with the short type names.
/// </summary>
static class TypedFields
{
    public static void Run()
    {
        using var index = new FirstOccurrenceIndex();
        var words = "the quick brown fox jumps over the lazy dog The End".Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            index.Record(words[i], i);
        }

        index.Print();
    }

    /// <summary>Remembers the position where each word first appears, skipping stop words.</summary>
    sealed class FirstOccurrenceIndex : IDisposable
    {
        readonly PooledHashMap<string, int> _firstPosition = PooledHashMap<string, int>.Create(capacity: 16);
        readonly ComparerSwissHashSet<string> _stopWords =
            ComparerSwissHashSet<string>.Create(StringComparer.OrdinalIgnoreCase);

        public FirstOccurrenceIndex()
        {
            _stopWords.Add("the");
            _stopWords.Add("over");
        }

        public void Record(string word, int position)
        {
            if (!_stopWords.Contains(word))
            {
                _firstPosition.TryAdd(word, position);
            }
        }

        public void Print() => PrintPositions(_firstPosition);

        public void Dispose()
        {
            _firstPosition.Dispose();
            _stopWords.Dispose();
        }

        // A SwissHashMap<string, int> could be passed here too: it derives from PooledHashMap<string, int>.
        static void PrintPositions(PooledHashMap<string, int> positions)
        {
            foreach (var (word, position) in positions)
            {
                Console.WriteLine($"  {word}: {position}");
            }
        }
    }
}
