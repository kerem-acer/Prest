# Prest

[![NuGet](https://img.shields.io/nuget/v/Prest.svg?label=nuget)](https://www.nuget.org/packages/Prest)
[![Downloads](https://img.shields.io/nuget/dt/Prest.svg)](https://www.nuget.org/packages/Prest)
[![CI](https://github.com/kerem-acer/Prest/actions/workflows/ci.yml/badge.svg)](https://github.com/kerem-acer/Prest/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](./LICENSE)

Zero-allocation pooled collections for .NET with pluggable, zero-cost hash-table algorithms.

## Highlights

- **Pooled backing store.** Arrays come from `ArrayPool<T>.Shared`; `Dispose` returns them. Single-consumer ownership — no locking, no concurrent mutation.
- **Pluggable hash algorithm.** `PooledHashMap<K,V>` and `PooledHashSet<T>` pick a probe strategy at the type-parameter level: **Swiss** (SIMD group scan; default, matches .NET 9's `Dictionary`), **RobinHood** (displacement-tracking linear, backward-shift on erase — no tombstones), **Linear** (simplest), **Chained** (dense entry storage, iteration-friendly). The algorithm is a `struct` generic — the JIT monomorphizes every call end-to-end. No virtual dispatch, no boxing.
- **Short, first-class type names.** `PooledHashMap<K,V>`, `SwissHashMap<K,V>`, `RobinHoodHashMap<K,V,TFinalizer>`, `ComparerLinearHashMap<K,V>`, … are real types with their own `Create`, so fields and parameters never need the full generic spelling.
- **Auto-return-on-`Dispose`.** `PooledHashMap<K,V>.Create()` rents from a per-thread cache; `using` disposes back into that cache so the next rental on the same thread skips allocation entirely.
- **Concurrent pool for `await` code.** `Prest.ObjectPool` ships `PooledHashMapPool<K,V>` / `PooledHashSetPool<T>` / `PooledBufferWriterPool<T>` backed by `Microsoft.Extensions.ObjectPool.DefaultObjectPool` — safe across threads, survives `await`, `Dispose` auto-returns to the pool.
- **Zero-allocation enumeration.** `foreach`, `.Keys`, `.Values` all return value-type enumerators — no `IEnumerator<T>` boxing.
- **Serializer integrations.** `System.Text.Json` converters and `VYaml` formatters for `PooledArray<T>` and `PooledHashMap<K,V>`.
- **Native AOT-compatible.** All shipping packages build with `IsAotCompatible=true` on .NET 8+. The reflection-based factory / resolver are flagged with `RequiresDynamicCode` so AOT analyzers point you at the concrete per-type converter/formatter as the AOT-safe alternative.

## Packages

| Package | Version | Source | Purpose |
|---|---|---|---|
| [`Prest`](https://www.nuget.org/packages/Prest) | [![NuGet](https://img.shields.io/nuget/v/Prest.svg)](https://www.nuget.org/packages/Prest) | [`src/Prest`](./src/Prest) | Core — `PooledHashMap<K,V>`, `PooledHashSet<T>`, algorithm/finalizer/hasher structs, `PooledArray<T>`, `PooledList<T>`, `PooledStack<T>`, `PooledBufferWriter<T>` |
| [`Prest.ObjectPool`](https://www.nuget.org/packages/Prest.ObjectPool) | [![NuGet](https://img.shields.io/nuget/v/Prest.ObjectPool.svg)](https://www.nuget.org/packages/Prest.ObjectPool) | [`src/Prest.ObjectPool`](./src/Prest.ObjectPool) | `DefaultObjectPool`-backed `PooledHashMapPool<K,V>`, `PooledHashSetPool<T>`, `PooledBufferWriterPool<T>` for `await`-safe rent/return |
| [`Prest.SystemTextJson`](https://www.nuget.org/packages/Prest.SystemTextJson) | [![NuGet](https://img.shields.io/nuget/v/Prest.SystemTextJson.svg)](https://www.nuget.org/packages/Prest.SystemTextJson) | [`src/Prest.SystemTextJson`](./src/Prest.SystemTextJson) | `PooledJsonBufferWriter` (pairs `Utf8JsonWriter` with `PooledBufferWriter<byte>`) + threadstatic cache |
| [`Prest.SystemTextJson.ObjectPool`](https://www.nuget.org/packages/Prest.SystemTextJson.ObjectPool) | [![NuGet](https://img.shields.io/nuget/v/Prest.SystemTextJson.ObjectPool.svg)](https://www.nuget.org/packages/Prest.SystemTextJson.ObjectPool) | [`src/Prest.SystemTextJson.ObjectPool`](./src/Prest.SystemTextJson.ObjectPool) | `DefaultObjectPool`-backed accessor for `PooledJsonBufferWriter` |
| [`Prest.Serializers.SystemTextJson`](https://www.nuget.org/packages/Prest.Serializers.SystemTextJson) | [![NuGet](https://img.shields.io/nuget/v/Prest.Serializers.SystemTextJson.svg)](https://www.nuget.org/packages/Prest.Serializers.SystemTextJson) | [`src/Prest.Serializers.SystemTextJson`](./src/Prest.Serializers.SystemTextJson) | `JsonConverter` implementations + `PooledTypesJsonConverterFactory` (one registration covers `PooledArray<T>`, `PooledList<T>`, `PooledHashMap<K,V>`, `PooledHashSet<T>` for any algorithm) |
| [`Prest.Serializers.VYaml`](https://www.nuget.org/packages/Prest.Serializers.VYaml) | [![NuGet](https://img.shields.io/nuget/v/Prest.Serializers.VYaml.svg)](https://www.nuget.org/packages/Prest.Serializers.VYaml) | [`src/Prest.Serializers.VYaml`](./src/Prest.Serializers.VYaml) | `IYamlFormatter` implementations for the same four types + `PooledTypeFormatterResolver` |
| [`Prest.Immutable`](https://www.nuget.org/packages/Prest.Immutable) | [![NuGet](https://img.shields.io/nuget/v/Prest.Immutable.svg)](https://www.nuget.org/packages/Prest.Immutable) | [`src/Prest.Immutable`](./src/Prest.Immutable) | `PooledArray<T>` ↔ `ImmutableArray<T>` conversion extensions |

## Install

Core package — pooled collections, `PooledHashMap`, `PooledHashSet`:

```bash
dotnet add package Prest
```

Optional packages, install only what you need:

```bash
# Await-safe object pools (PooledHashMapPool, PooledHashSetPool, PooledBufferWriterPool)
dotnet add package Prest.ObjectPool

# System.Text.Json integration — PooledJsonBufferWriter
dotnet add package Prest.SystemTextJson
dotnet add package Prest.SystemTextJson.ObjectPool   # + await-safe accessor

# JsonConverter / IYamlFormatter for pooled collection types
dotnet add package Prest.Serializers.SystemTextJson
dotnet add package Prest.Serializers.VYaml

# ImmutableArray<T> ↔ PooledArray<T> interop (net10.0+)
dotnet add package Prest.Immutable
```

All packages target **`net11.0`, `net10.0`, `net8.0`, `netstandard2.1`, `netstandard2.0`** (except `Prest.Serializers.SystemTextJson` which is `net8.0+` and `Prest.Immutable` which is `net10.0+`). See [**Target frameworks**](#target-frameworks) for the full matrix.

## Quick start

```csharp
using Prest;

// `using` disposes the map and returns pooled arrays to ArrayPool.Shared.
using var map = PooledHashMap<int, string>.Create(capacity: 16);

map.Add(1, "one");
map.Add(2, "two");
map.Add(3, "three");

Console.WriteLine($"Count: {map.Count}");          // 3
Console.WriteLine($"map[2] = {map[2]}");            // two

if (map.TryGetValue(3, out var three))
{
    Console.WriteLine($"TryGetValue(3) -> {three}");
}

map.Remove(1);
Console.WriteLine($"ContainsKey(1): {map.ContainsKey(1)}");  // False
```

## Tour

### Hash map / hash set

The default `PooledHashMap<K,V>` / `PooledHashSet<T>` close over SwissTable — the same algorithm .NET 9's `Dictionary` moved to, chosen as a reasonable starting point for mixed workloads. Like `Dictionary`, they take an optional comparer (see [**Custom equality comparer**](#custom-equality-comparer)). `Create(capacity)` rents from a per-thread cache, `Dispose` returns to it. See [**Choosing an algorithm**](#choosing-an-algorithm) for when to pick a different one.

```csharp
using var map = PooledHashMap<int, string>.Create(capacity: 16);
map.Add(1, "one");

using var set = PooledHashSet<string>.Create(capacity: 8);
set.Add("apple");
var wasNew = set.Add("apple");  // false — already present
```

Every alias also works with collection expressions. They go through the alias's own `Create`, so they rent from and return to the same per-thread cache:

```csharp
using PooledHashMap<string, int> empty = [];
using PooledHashMap<string, int> stock = [new("apples", 3), new("pears", 5)];
using PooledHashMap<string, int> more = [.. stock, new("plums", 2)];
using PooledHashSet<string> tags = ["red", "green", "red"];  // 2 items — duplicates are ignored

// C# 15 collection arguments: pre-size the table.
using RobinHoodHashMap<int, int> sized = [with(capacity: 256)];
```

- `[]`, `[a, b]` and spreads work on C# 12 and later. `[with(...)]` needs C# 15 (`<LangVersion>preview</LangVersion>` on the .NET 11 SDK).
- A key that appears twice in a map expression doesn't throw: the last entry wins.
- Map elements are `KeyValuePair<K,V>` values (`new("apples", 3)`). Dictionary expressions (`["apples": 3]`) aren't in the compiler yet.

Every alias's `Create` returns the alias itself, so the short name works for fields, parameters and return types:

```csharp
sealed class WordCounter : IDisposable
{
    readonly PooledHashMap<string, int> _counts = PooledHashMap<string, int>.Create(capacity: 64);
    readonly ComparerSwissHashSet<string> _stopWords =
        ComparerSwissHashSet<string>.Create(StringComparer.OrdinalIgnoreCase);

    public void Dispose()
    {
        _counts.Dispose();
        _stopWords.Dispose();
    }
}
```

The generic form `PooledHashMap<K,V,TAlgo>` / `PooledHashSet<T,TAlgo>` is still there for code that is generic over the algorithm; every alias derives from it.

### Pick an algorithm

Same API surface on every alias — swap the type name, nothing else:

```csharp
using var swiss  = SwissHashMap<int, int>.Create(32);
using var robin  = RobinHoodHashMap<int, int>.Create(32);
using var linear = LinearHashMap<int, int>.Create(32);
using var chain  = ChainedHashMap<int, int>.Create(32);
```

`PooledHashMap<K,V>` and `SwissHashMap<K,V>` both use SwissTable, but they are separate types. `PooledHashMap<K,V>` takes an optional comparer, so every hash and equality check first tests whether it has one. `SwissHashMap<K,V>` takes no comparer and skips that test, which matters for integer keys: on ARM64, int-key hits measured 29–39% faster and misses 4–15% faster. For string keys the difference is small next to hashing the string (0–12%). See [**Choosing an algorithm**](#choosing-an-algorithm) below.

Each algorithm comes in five shapes (sets mirror maps, with `T` in place of `K,V`):

| Alias | Configuration |
|---|---|
| `{Algo}HashMap<K,V>` | default hasher, no finalizer |
| `{Algo}HashMap<K,V,TFinalizer>` | default hasher + [finalizer](#custom-hash-finalizer) |
| `{Algo}HashMap<K,V,THasher,TFinalizer>` | your own stateless [struct hasher](#custom-struct-hasher) + finalizer |
| `Comparer{Algo}HashMap<K,V>` | runtime [`IEqualityComparer<K>`](#custom-equality-comparer) |
| `Comparer{Algo}HashMap<K,V,TFinalizer>` | runtime `IEqualityComparer<K>` + finalizer |

`{Algo}` is `Swiss`, `RobinHood`, `Linear` or `Chained`.

### Custom equality comparer

The default `PooledHashMap<K,V>` / `PooledHashSet<T>` take a runtime `IEqualityComparer<T>` the way `Dictionary` does. Each algorithm also ships a `Comparer{Algo}HashMap<K,V>` / `Comparer{Algo}HashSet<T>` variant for a comparer with a specific algorithm or finalizer:

```csharp
using var byName = PooledHashMap<string, int>.Create(StringComparer.OrdinalIgnoreCase);
using PooledHashSet<string> seen = [with(StringComparer.OrdinalIgnoreCase)];

using var map = ComparerSwissHashMap<string, int>.Create(
    StringComparer.OrdinalIgnoreCase, capacity: 8);

map.Add("Hello", 1);
Console.WriteLine(map.ContainsKey("HELLO"));  // True
Console.WriteLine(map.Comparer == StringComparer.OrdinalIgnoreCase);  // True

// Comparer + finalizer.
using var ids = ComparerRobinHoodHashMap<string, int, FibonacciFinalizer>.Create(
    StringComparer.Ordinal);

// Collection expressions (C# 15) take the comparer, and optionally a capacity, in with(...).
using ComparerSwissHashMap<string, int> m = [with(StringComparer.OrdinalIgnoreCase)];
using ComparerSwissHashSet<string> words =
    [with(StringComparer.OrdinalIgnoreCase, 16), "Hello", "HELLO"];  // 1 item
```

- On `PooledHashMap<K,V>` / `PooledHashSet<T>` the comparer is optional: `Create()`, `[]` and `Create(null)` use `EqualityComparer<T>.Default`.
- On the `Comparer*` aliases, `Create(comparer)` or `[with(comparer), ...]` creates one. Without a comparer (`Create()`, `Create(capacity)`, `[]`, `[with(capacity)]`) it's a compile error on these aliases. Passing `null` uses `EqualityComparer<T>.Default`.
- The `{Algo}HashMap<K,V>` / `{Algo}HashMap<K,V,TFinalizer>` aliases (and their sets) always use `EqualityComparer<T>.Default`. `Create(comparer)` or `[with(comparer)]` on them is a compile error that names the type to declare instead.
- `Comparer` returns the comparer the map or set uses.
- The per-thread cache only hands a disposed instance back to a `Create` call with the **same comparer instance** (reference equality — singletons like `StringComparer.OrdinalIgnoreCase` qualify). A different comparer gets a new instance. This applies to `PooledHashMap<K,V>` / `PooledHashSet<T>` too.

### Custom hash finalizer

Bit-mixing stage applied after `GetHashCode`. Useful when keys have poor distribution (sequential ints, identity-hashed refs, single-field structs). Pick a finalizer via the second generic-arity alias:

```csharp
// Sequential stride keys — Fibonacci spreads them out.
using var fib = SwissHashMap<int, int, FibonacciFinalizer>.Create(capacity: 64);
for (var i = 0; i < 32; i++) fib.Add(i * 1024, i);

// Adversarial input — XMX has the strongest mixing.
using var xmx = RobinHoodHashMap<int, int, XmxFinalizer>.Create(capacity: 64);
```

See [**Choosing a finalizer**](#choosing-a-finalizer) below.

### Custom struct hasher

For custom equality without the virtual call of an `IEqualityComparer<T>`, implement `IHasher<T>` on a stateless `readonly struct` and plug it in with the four-argument alias (three for sets). The JIT inlines it like the default hasher:

```csharp
readonly struct IgnoreCaseHasher : IHasher<string>
{
    public uint ComputeHash(string key) => (uint)string.GetHashCode(key, StringComparison.OrdinalIgnoreCase);
    public bool Equals(string x, string y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
}

using var headers = SwissHashMap<string, string, IgnoreCaseHasher, NoOpHashFinalizer>.Create();
using var seen = LinearHashSet<string, IgnoreCaseHasher, NoOpHashFinalizer>.Create();
```

The alias always uses `default(THasher)`, so the hasher can't carry state. `ComparerHasher<T>` needs a comparer instance and is rejected with `NotSupportedException`; use a `Comparer*` alias instead.

### Enumeration (zero allocation)

`foreach`, `.Keys`, and `.Values` return value-type enumerators — no boxing, no `IEnumerator<T>` allocation.

```csharp
foreach (var kvp in map)             { /* kvp.Key, kvp.Value */ }
foreach (var key   in map.Keys)      { /* ... */ }
foreach (var value in map.Values)    { /* ... */ }
```

The heap-allocated collections also implement the read-only interfaces, so they can be passed to APIs and LINQ: `PooledHashMap` → `IReadOnlyDictionary<K,V>`, `PooledList<T>` → `IReadOnlyList<T>`, `PooledHashSet` and `PooledStack<T>` → `IReadOnlyCollection<T>`. Enumerating through an interface boxes the enumerator; `foreach` on the concrete type does not. Enumerators don't detect modification — don't add or remove items while enumerating (a hash table grow returns the old buffers to the pool).

### Alternate-key lookup (.NET 10+)

Look up `string` keys by `ReadOnlySpan<char>` without allocating a string, the same way as `Dictionary<TKey,TValue>.GetAlternateLookup`:

```csharp
using var map = PooledHashMap<string, int>.Create();
map.Add("apple", 1);

var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
ReadOnlySpan<char> line = "apple=5";
var name = line[..line.IndexOf('=')];

lookup.TryGetValue(name, out var count);  // true, 1: no string allocated
lookup.TryAdd("pear".AsSpan(), 2);        // creates the "pear" string, because it is a new key
lookup.Remove(name);
```

The lookup has `TryGetValue` (also with the stored key), `ContainsKey`, the indexer getter, `TryAdd` and `Remove`. Sets have one too, with `Add`, `Contains`, `Remove` and `TryGetValue`. It works with the default hasher and with any comparer that implements `IAlternateEqualityComparer<TAlternate, TKey>`. All the built-in `StringComparer`s do, so `PooledHashMap<string, int>.Create(StringComparer.OrdinalIgnoreCase)` supports span lookups as well. If the comparer doesn't, `GetAlternateLookup` throws `InvalidOperationException` and `TryGetAlternateLookup` returns `false`. As with `Dictionary`, each hash or equality check is one interface call to the comparer.

To skip those calls, or to look up by a type no comparer supports, pass a struct `IAlternateHasher<TAlternate, TKey>` and the JIT specializes the lookup for it. This one finds `string` keys by their UTF-8 bytes in a default map:

```csharp
readonly struct Utf8Hasher : IAlternateHasher<ReadOnlySpan<byte>, string>
{
    // Hash and compare the UTF-16 form, so the hash matches the map's string hasher.
    public uint ComputeHash(ReadOnlySpan<byte> utf8)
    {
        Span<char> chars = utf8.Length <= 128 ? stackalloc char[128] : new char[utf8.Length];
        Utf8.ToUtf16(utf8, chars, out _, out var length);
        return (uint)string.GetHashCode(chars[..length]);
    }

    public bool Equals(ReadOnlySpan<byte> utf8, string key)
    {
        Span<char> chars = utf8.Length <= 128 ? stackalloc char[128] : new char[utf8.Length];
        Utf8.ToUtf16(utf8, chars, out _, out var length);
        return chars[..length].SequenceEqual(key);
    }

    public string Create(ReadOnlySpan<byte> utf8) => Encoding.UTF8.GetString(utf8);
}

var utf8 = map.GetAlternateLookup<ReadOnlySpan<byte>, Utf8Hasher>();
utf8.TryGetValue("apple"u8, out var count);
```

The hasher has to agree with the map's hasher: an alternate key must get the same raw hash (before the finalizer) as the key it stands for, and `Create` must return that key. Prest can't check this. If they disagree, lookups miss entries that are present and `TryAdd` can add duplicate keys. A map built on your own [struct hasher](#custom-struct-hasher) has no comparer to ask, so it supports only this overload.

Alternate lookup is in the net10.0 and net11.0 builds. It relies on `allows ref struct`, which needs .NET 9+, so the net8.0 and netstandard builds don't have it.

### Ref access to values

`PooledCollectionsMarshal` is the counterpart of `CollectionsMarshal` for maps. It has `GetValueRefOrNullRef` and `GetValueRefOrAddDefault`, which let you read or update a value in place with one lookup:

```csharp
using var counts = PooledHashMap<string, int>.Create();
foreach (var word in words)
{
    PooledCollectionsMarshal.GetValueRefOrAddDefault(counts, word, out _)++;
}

ref var count = ref PooledCollectionsMarshal.GetValueRefOrNullRef(counts, "apple");
if (!Unsafe.IsNullRef(ref count))
{
    count = 0;
}
```

Both methods take any map type (`PooledHashMap<K,V>`, `ComparerSwissHashMap<K,V>`, …). On net10.0+ they also take an alternate lookup, so `ReadOnlyMemory<byte>` keys can be found or added by a `ReadOnlySpan<byte>`:

```csharp
var bySpan = symbols.GetAlternateLookup<ReadOnlySpan<byte>>();
ref var symbol = ref PooledCollectionsMarshal.GetValueRefOrNullRef(bySpan, address.Span);
```

`GetValueRefOrAddDefault` creates the key (through the comparer or `IAlternateHasher.Create`) only when it adds the entry. As with `CollectionsMarshal`, the reference is valid only until the map changes. Adding or removing an entry, `Clear`, `Dispose` or `Return` can move entries or give the array back to the pool. After that, the reference points into memory someone else may be using.

### Pooled arrays and lists

```csharp
// Collection-expression literal over ArrayPool.Shared.
using PooledArray<int> numbers = [1, 2, 3];

// Growable, heap-allocated wrapper. Implements IReadOnlyList<T>, so it can be handed
// to APIs and LINQ; foreach still uses the allocation-free struct enumerator.
using var list = new PooledList<int>(initialCapacity: 16);
list.Add(42);
list.RemoveAt(0);

// Struct variant — zero wrapper allocation. Copies share rented buffers; Dispose one.
using var value = new ValuePooledList<int>(16);
value.Add(7);

// Ref-struct variant — compiler-enforced stack-only; an inline Span<T>
// can hold items before the pool rental happens.
Span<int> inline = stackalloc int[8];
using var stack = new StackOnlyPooledList<int>(inline);
stack.Add(99);
```

All three lists expose `.Span` (read-only) and `AsSpan()`, a writable `Span<T>` over `[0, Count)` — the counterpart of `CollectionsMarshal.AsSpan(List<T>)`. Use it for in-place work the list has no method for:

```csharp
using var words = new PooledList<string>();
words.AddRange(["banana", "fig", "kiwi"]);
words.AsSpan().Sort((a, b) => a.Length.CompareTo(b.Length)); // fig, kiwi, banana
```

Don't hold the span across calls that change the list (`Add`, `AddRange`, `RemoveAt`, `Clear`, `Dispose`, `ToPooledArray`, `DetachArray`) — get a new one afterwards. A grow returns the old array to `ArrayPool<T>.Shared`, and writing through a stale span would corrupt whatever code rented it next.

### Pooled stacks

`PooledStack<T>` (class) and `StackOnlyPooledStack<T>` (ref struct) mirror `Stack<T>`: `Push`, `Pop` / `TryPop`, `Peek` / `TryPeek`. `foreach` walks top to bottom — the order `Pop` returns — while `.Span` exposes elements in push order (bottom at index 0).

```csharp
// Iterative depth-first walk. The inline buffer covers typical depths,
// so the stack never touches the pool or the heap.
using var pending = new StackOnlyPooledStack<int>(stackalloc int[32]);
pending.Push(rootIndex);
while (pending.TryPop(out var index))
{
    Visit(index);
    foreach (var child in children[index])
    {
        pending.Push(child);
    }
}

// Heap-allocated variant — can live in a field or cross an await.
using var undo = new PooledStack<EditCommand>();
undo.Push(command);
```

### Stack-only hash maps and sets

`StackOnlyPooledHashMap<K,V>` and `StackOnlyPooledHashSet<T>` are `ref struct` versions of the default map and set (SwissTable, default equality, same API core). The table lives inline in the struct, so there is no class instance and no per-thread cache; `Dispose` returns the arrays to `ArrayPool<T>.Shared`. `default` and `new()` rent on the first add.

```csharp
// A temporary index inside one method.
using var firstSeen = new StackOnlyPooledHashMap<string, int>(16);
for (var i = 0; i < words.Length; i++)
{
    firstSeen.TryAdd(words[i], i); // keeps the first position of each word
}

using var seen = new StackOnlyPooledHashSet<int>();
foreach (var id in ids)
{
    if (!seen.Add(id))
    {
        Console.WriteLine($"duplicate {id}");
    }
}

// The defaults take an optional comparer, like PooledHashMap<K,V>.
using var ignoreCase = new StackOnlyPooledHashMap<string, int>(16, StringComparer.OrdinalIgnoreCase);

// The generic form takes any algorithm; pass it in when the hasher has state.
using var robin = new StackOnlyPooledHashMap<string, int,
    RobinHoodAlgorithm<KeyValueSlot<string, int>, string, ComparerHasher<string>, NoOpHashFinalizer>>(
    16, new(new ComparerHasher<string>(StringComparer.Ordinal), default));
```

Pick them over `PooledHashMap` / `PooledHashSet` for maps that live and die inside one synchronous method: nothing is allocated even when several are alive at once (the class's per-thread cache holds one instance per type), and there is no cache to return to. Pick the class types when the map has to cross an `await`, live in a field, be captured by a lambda, or be handed out as `IReadOnlyDictionary<K,V>` / `IReadOnlyCollection<T>` — a ref struct can do none of these. The saving is allocations, not time: renting and returning the arrays on every use costs about as much as reusing a cached `PooledHashMap`, and somewhat more for very small maps (`StackOnlyPooledHashMapBenchmarks.cs`).

Don't copy one: `var b = a;` gives two tables over the same arrays with separate counts, and disposing both returns the arrays twice. Pass it to helpers by `ref` (not `in`, which mutates a hidden copy). A `using var` local can't be passed by `ref`, so dispose in `finally` there:

```csharp
var map = new StackOnlyPooledHashMap<int, int>();
try
{
    Fill(ref map);
}
finally
{
    map.Dispose();
}
```

### `IBufferWriter<T>`

Same-thread reuse via a threadstatic cache:

```csharp
var writer = PooledBufferWriter<byte>.Rent();
try
{
    var span = writer.GetSpan(128);
    // ... write bytes, call writer.Advance(n)
    ReadOnlyMemory<byte> bytes = writer.WrittenMemory;
}
finally
{
    PooledBufferWriter<byte>.Return(writer);
}
```

For pooling that survives `await`, install `Prest.ObjectPool`:

```csharp
using Prest.ObjectPool;

var writer = PooledBufferWriterPool<byte>.Rent();
try
{
    await Task.Yield();    // writer is still valid on any continuation thread
    // ...
}
finally
{
    PooledBufferWriterPool<byte>.Return(writer);
}
```

### JSON

```csharp
using System.Text.Json;
using Prest;
using Prest.Serializers.SystemTextJson;

// One registration covers every pooled type for any algorithm closure.
var options = new JsonSerializerOptions
{
    Converters = { new PooledTypesJsonConverterFactory() },
};

PooledArray<int> values = [1, 2, 3];
string json = JsonSerializer.Serialize(values, options);
using var roundTripped = JsonSerializer.Deserialize<PooledArray<int>>(json, options);
```

Alias-typed properties deserialize to the alias type — a `PooledHashMap<string, int>` property comes back as a `PooledHashMap<string, int>`, a `RobinHoodHashSet<int, FibonacciFinalizer>` as exactly that:

```csharp
public sealed class Order
{
    public PooledHashMap<string, int>? Quantities { get; set; }
    public SwissHashSet<string>? Tags { get; set; }
}

var order = JsonSerializer.Deserialize<Order>(json, options);
```

Map keys are written and read as JSON property names through the key type's converter, so every key type `System.Text.Json` supports for `Dictionary<K,V>` keys works (`string`, numbers, `Guid`, enums, …).

`Comparer*` aliases serialize normally, but the JSON doesn't carry the comparer, so the factory can't deserialize them and throws `NotSupportedException`. Register an alias converter that supplies the comparer, ahead of the factory:

```csharp
options.Converters.Insert(0, new PooledHashMapAliasConverter<
    ComparerSwissHashMap<string, int>, string, int,
    SwissTableAlgorithm<KeyValueSlot<string, int>, string, ComparerHasher<string>, NoOpHashFinalizer>>(
    capacity => ComparerSwissHashMap<string, int>.Create(StringComparer.OrdinalIgnoreCase, capacity)));
```

If you prefer to register converters individually — e.g. to restrict which closed types
are supported — the factory-less form is still there:

```csharp
options.Converters.Add(new PooledArrayConverter<int>());
options.Converters.Add(new PooledListConverter<int>());
options.Converters.Add(new PooledHashSetConverter<int,
    SwissTableAlgorithm<int, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>());
options.Converters.Add(new PooledHashMapConverter<int, string,
    SwissTableAlgorithm<KeyValueSlot<int, string>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>());

// Alias types: pass the alias's own Create.
options.Converters.Add(new PooledHashMapAliasConverter<PooledHashMap<string, int>, string, int,
    SwissTableAlgorithm<KeyValueSlot<string, int>, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>>(
    PooledHashMap<string, int>.Create));
```

### YAML

```csharp
using VYaml.Serialization;
using Prest;
using Prest.Serializers.VYaml;

var options = YamlSerializerOptions.Standard;
options.Resolver = CompositeResolver.Create(
    (IYamlFormatterResolver[])
    [
        PooledTypeFormatterResolver.Instance,
        StandardResolver.Instance,
    ]);

PooledArray<string> items = ["alpha", "beta"];
string yaml = YamlSerializer.SerializeToString(items, options);
```

The resolver covers `PooledArray<T>`, `PooledList<T>`, `PooledHashMap<K,V>`, and `PooledHashSet<T>` in one registration — no need to add each formatter individually. Alias types round-trip as themselves, as with JSON. `Comparer*` aliases need a `PooledHashMapAliasFormatter` / `PooledHashSetAliasFormatter` constructed with a comparer-supplying factory, placed ahead of the resolver (for example as the first argument of `CompositeResolver.Create(formatters, resolvers)`).

### Immutable interop

```csharp
using Prest;
using Prest.Immutable;

PooledArray<int> pooled = [1, 2, 3];
ImmutableArray<int> immutable = pooled.ToImmutableArray();

using var back = PooledArray.FromImmutableArray(immutable);
```

## Choosing an algorithm

| Algorithm | Probe | Best for | Notes |
|---|---|---|---|
| **Swiss** *(default)* | Triangular, 16-byte SIMD group scan | Mixed read/write workloads with well-distributed keys | Matches .NET 9's `Dictionary`. Uses tombstones on erase, which can degrade under heavy churn if keys hash densely — pair with a finalizer or pick RobinHood if you're churning sequential-int keys. SIMD path is fastest on x86 (single-instruction `PMOVMSKB`); ARM64 synthesizes `ExtractMostSignificantBits` in several instructions, so the Swiss-vs-alternatives gap narrows there. |
| **RobinHood** | Linear with displacement tracking | Integer-keyed hot paths; churn-heavy workloads; ARM64 | Backward-shift on erase keeps displacements monotonic — no tombstones, no pathological churn. Fastest algorithm for integer-keyed lookup on ARM64 in our benchmarks (~37% faster than `Dictionary`, ~37% faster than Swiss). |
| **Linear** | One slot at a time, scalar | Tiny maps, debugging, a baseline for comparisons | Simplest; no SIMD win but no SIMD cost on cold code paths either. |
| **Chained** | Separate chaining with dense entry array | Iteration-heavy workloads | Entries are packed `0..Count-1`, so `foreach` walks a dense array. |

**Quick rule of thumb:**

- Start with the default `PooledHashMap<K,V>` (Swiss, optional comparer). For integer-keyed hot paths, use `SwissHashMap<K,V>`: the same algorithm without the comparer check. It is within a few percent of `Dictionary` on lookup workloads and roughly 10–20% faster than `Dictionary` on `Add` (thanks to pooled arrays — no allocation). The comparer check made `PooledHashMap<int, int>` hits 29–39% slower than `SwissHashMap<int, int>` on ARM64.
- If you do heavy add/remove churn on `int`/`long` keys, switch to `RobinHood` **or** keep Swiss and pair it with `FibonacciFinalizer`. Sequential or tightly-packed integer keys combined with identity hashing + tombstones is the one workload where Swiss can regress badly.
- If your app is read-mostly and iterates maps often, `Chained` gives you the densest enumeration path.

## Choosing a finalizer

| Finalizer | Mixing cost | Use when… |
|---|---|---|
| **NoOpHashFinalizer** *(default)* | Free (identity) | Keys already have good `GetHashCode` distribution. |
| **FibonacciFinalizer** | One multiply | Sequential / strided integer keys (timestamps, counters, indices). |
| **Lowbias32Finalizer** | Two multiplies + shifts | You want stronger mixing without going full crypto — Wellons' lowest-avalanche-bias integer hash. |
| **XmxFinalizer** | Xorshift–multiply–xorshift | Adversarial or pathological input; strongest mixing. |

## Per-thread cache vs. ObjectPool

Both `PooledHashMap<K,V>.Create()` (core) and `PooledHashMapPool<K,V>.Create()` (`Prest.ObjectPool`) return a ready-to-use map; both auto-return on `Dispose`. The difference is the storage backing the pool:

- **Per-thread cache** (core) — a `[ThreadStatic]` single-slot cache per thread **and per type**: `PooledHashMap<K,V>`, `SwissHashMap<K,V>`, the generic `PooledHashMap<K,V,TAlgo>` and every other alias each have their own slot, and `Dispose` returns an instance to the slot of its own type. Zero synchronisation. Ideal for synchronous request handlers and short-lived scopes. Do **not** keep a reference to the map after `Dispose` — another caller on this thread will rent it.
- **`Prest.ObjectPool`** — `Microsoft.Extensions.ObjectPool.DefaultObjectPool` (max 64 retained). Concurrent-safe; instances survive `await` boundaries. Use when the map must outlive a single synchronous scope.

You should never mix the two on the same instance — a map rented from `PooledHashMapPool` returns itself to that pool on `Dispose`, not to the per-thread cache.

## Examples and benchmarks

`examples/Prest.Examples/` — runnable mini-programs for each feature:

- `BasicMap.cs`, `BasicSet.cs` — minimal CRUD.
- `Algorithms.cs` — Swiss / RobinHood / Linear / Chained side-by-side.
- `CustomComparer.cs` — wiring an `IEqualityComparer<T>`.
- `CustomHasher.cs` — a stateless struct `IHasher<T>` through the hasher alias.
- `TypedFields.cs` — a class holding maps and sets in fields declared with the short alias types.
- `Finalizers.cs` — Fibonacci + XMX finalizers on clustered keys.
- `Enumeration.cs` — `foreach`, `.Keys`, `.Values`.

Run them:

```bash
dotnet run --project examples/Prest.Examples -c Release
```

`benchmarks/Prest.Benchmarks/` — [BenchmarkDotNet](https://benchmarkdotnet.org/) suites:

- `PooledHashMapBenchmarks.cs` — `PooledHashMap<int,int>` vs `Dictionary<int,int>` vs `Faster.Map.DenseMap<int,int>` across lookup (hit + miss), `ContainsKey`, insert-from-empty, and steady-state churn at N ∈ {16, 256, 4096, 65536}.
- `PooledHashMapStringBenchmarks.cs` — same comparisons on `string`-keyed maps.
- `AlgorithmComparisonBenchmarks.cs` — the four algorithms head-to-head.
- `AlternateLookupBenchmarks.cs` — `ReadOnlySpan<char>` and UTF-8 lookups on `string`-keyed maps vs `Dictionary.GetAlternateLookup`, through the comparer and through a struct hasher.
- `FinalizerComparisonBenchmarks.cs` — `NoOp` / `Fibonacci` / `Lowbias32` / `XMX` on Swiss and RobinHood with sequential-int keys.
- `PooledStackBenchmarks.cs` — `PooledStack<int>` and `StackOnlyPooledStack<int>` vs `Stack<int>`: push N then pop N, on a fresh stack per call and on a reused one, at N ∈ {16, 256, 4096}.
- `StackOnlyPooledHashMapBenchmarks.cs` — `StackOnlyPooledHashMap<int,int>` and `PooledHashMap<int,int>.Create` vs `Dictionary<int,int>` as a method-local dedup index, with one map alive and with two alive at once, at N ∈ {16, 256, 4096}.

Latest results are committed under [`benchmarks/artifacts/results/`](./benchmarks/artifacts/results) (`-report-github.md` files render directly on GitHub).

```bash
dotnet run --project benchmarks/Prest.Benchmarks -c Release -- --filter '*'
```

## Native AOT

All shipping packages set `<IsAotCompatible>true</IsAotCompatible>` on their .NET 8+ TFMs, so trim / AOT / single-file analyzers run in-build and fail on any public API that isn't AOT-safe. The core types, algorithms, object pools, `PooledBufferWriter`, and every individual converter/formatter are all analyzer-clean.

Two convenience APIs are flagged with `[RequiresDynamicCode]` + `[RequiresUnreferencedCode]` because they reach the closed converter/formatter type via `Type.MakeGenericType`:

- `Prest.Serializers.SystemTextJson.PooledTypesJsonConverterFactory`
- `Prest.Serializers.VYaml.PooledTypeFormatterResolver`

If your project uses Native AOT or trimming, skip these two and register the concrete converters directly — each is AOT-safe when closed over concrete generic arguments:

```csharp
options.Converters.Add(new PooledArrayConverter<int>());
options.Converters.Add(new PooledListConverter<int>());
options.Converters.Add(new PooledHashSetConverter<int,
    SwissTableAlgorithm<int, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>());
options.Converters.Add(new PooledHashMapConverter<int, string,
    SwissTableAlgorithm<KeyValueSlot<int, string>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>());

// Alias types (PooledHashMap<K,V>, SwissHashSet<T>, …) take the alias's own Create.
options.Converters.Add(new PooledHashSetAliasConverter<SwissHashSet<string>, string,
    SwissTableAlgorithm<string, string, EqualityDefaultHasher<string>, NoOpHashFinalizer>>(
    SwissHashSet<string>.Create));
```

The same applies to VYaml: instead of adding `PooledTypeFormatterResolver.Instance` to your resolver chain, construct the concrete `PooledArrayFormatter<T>` / `PooledListFormatter<T>` / `PooledHashMapFormatter<K,V,TAlgo>` / `PooledHashSetFormatter<T,TAlgo>` (or `PooledHashMapAliasFormatter<TMap,K,V,TAlgo>` / `PooledHashSetAliasFormatter<TSet,T,TAlgo>` for alias types) you need at compile time and compose them into your own `IYamlFormatterResolver`.

## Target frameworks

| Package | net11.0 | net10.0 | net8.0 | netstandard2.1 | netstandard2.0 |
|---|:-:|:-:|:-:|:-:|:-:|
| `Prest` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `Prest.ObjectPool` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `Prest.SystemTextJson` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `Prest.SystemTextJson.ObjectPool` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `Prest.Serializers.SystemTextJson` | ✓ | ✓ | ✓ | — | — |
| `Prest.Serializers.VYaml` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `Prest.Immutable` | ✓ | ✓ | — | — | — |

`Prest.Serializers.SystemTextJson` requires `net8.0+` for `IUtf8SpanFormattable`. `Prest.Immutable` requires `net10.0+` (uses C# extension members).

## License

MIT © kerem-acer. See [`LICENSE`](./LICENSE).
