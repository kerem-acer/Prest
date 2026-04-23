# Prest

Zero-allocation pooled collections for .NET with pluggable, zero-cost hash-table algorithms.

## Highlights

- **Pooled backing store.** Arrays come from `ArrayPool<T>.Shared`; `Dispose` returns them. Single-consumer ownership — no locking, no concurrent mutation.
- **Pluggable hash algorithm.** `PooledHashMap<K,V>` and `PooledHashSet<T>` pick a probe strategy at the type-parameter level: **Swiss** (SIMD group scan; default, matches .NET 9's `Dictionary`), **RobinHood** (displacement-tracking linear, backward-shift on erase — no tombstones), **Linear** (simplest), **Chained** (dense entry storage, iteration-friendly). The algorithm is a `struct` generic — the JIT monomorphizes every call end-to-end. No virtual dispatch, no boxing.
- **Auto-return-on-`Dispose`.** `PooledHashMap<K,V>.Create()` rents from a per-thread cache; `using` disposes back into that cache so the next rental on the same thread skips allocation entirely.
- **Concurrent pool for `await` code.** `Prest.ObjectPool` ships `PooledHashMapPool<K,V>` / `PooledHashSetPool<T>` / `PooledBufferWriterPool<T>` backed by `Microsoft.Extensions.ObjectPool.DefaultObjectPool` — safe across threads, survives `await`, `Dispose` auto-returns to the pool.
- **Zero-allocation enumeration.** `foreach`, `.Keys`, `.Values` all return value-type enumerators — no `IEnumerator<T>` boxing.
- **Serializer integrations.** `System.Text.Json` converters and `VYaml` formatters for `PooledArray<T>` and `PooledHashMap<K,V>`.
- **Native AOT-compatible.** All shipping packages build with `IsAotCompatible=true` on .NET 8+. The reflection-based factory / resolver are flagged with `RequiresDynamicCode` so AOT analyzers point you at the concrete per-type converter/formatter as the AOT-safe alternative.

## Packages

| Package | Purpose |
|---|---|
| [`Prest`](./src/Prest) | Core — `PooledHashMap<K,V>`, `PooledHashSet<T>`, algorithm/finalizer/hasher structs, `PooledArray<T>`, `PooledList<T>`, `PooledBufferWriter<T>` |
| [`Prest.ObjectPool`](./src/Prest.ObjectPool) | `DefaultObjectPool`-backed `PooledHashMapPool<K,V>`, `PooledHashSetPool<T>`, `PooledBufferWriterPool<T>` for `await`-safe rent/return |
| [`Prest.SystemTextJson`](./src/Prest.SystemTextJson) | `PooledJsonBufferWriter` (pairs `Utf8JsonWriter` with `PooledBufferWriter<byte>`) + threadstatic cache |
| [`Prest.SystemTextJson.ObjectPool`](./src/Prest.SystemTextJson.ObjectPool) | `DefaultObjectPool`-backed accessor for `PooledJsonBufferWriter` |
| [`Prest.Serializers.SystemTextJson`](./src/Prest.Serializers.SystemTextJson) | `JsonConverter` implementations + `PooledTypesJsonConverterFactory` (one registration covers `PooledArray<T>`, `PooledList<T>`, `PooledHashMap<K,V>`, `PooledHashSet<T>` for any algorithm) |
| [`Prest.Serializers.VYaml`](./src/Prest.Serializers.VYaml) | `IYamlFormatter` implementations for the same four types + `PooledTypeFormatterResolver` |
| [`Prest.Immutable`](./src/Prest.Immutable) | `PooledArray<T>` ↔ `ImmutableArray<T>` conversion extensions |

## Install

```bash
dotnet add package Prest
```

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

The default `PooledHashMap<K,V>` / `PooledHashSet<T>` close over SwissTable — the same algorithm .NET 9's `Dictionary` moved to, chosen as a reasonable starting point for mixed workloads. `Create(capacity)` rents from a per-thread cache, `Dispose` returns to it. See [**Choosing an algorithm**](#choosing-an-algorithm) for when to pick a different one.

```csharp
using var map = PooledHashMap<int, string>.Create(capacity: 16);
map.Add(1, "one");

using var set = PooledHashSet<string>.Create(capacity: 8);
set.Add("apple");
var wasNew = set.Add("apple");  // false — already present
```

### Pick an algorithm

Same API surface on every alias — swap the type name, nothing else:

```csharp
using var swiss  = SwissHashMap<int, int>.Create(32);
using var robin  = RobinHoodHashMap<int, int>.Create(32);
using var linear = LinearHashMap<int, int>.Create(32);
using var chain  = ChainedHashMap<int, int>.Create(32);
```

`PooledHashMap<K,V>` is an alias for `SwissHashMap<K,V>`. See [**Choosing an algorithm**](#choosing-an-algorithm) below.

### Custom equality comparer

Each algorithm ships a `Comparer{Algo}HashMap<K,V>` / `Comparer{Algo}HashSet<T>` variant that takes a runtime `IEqualityComparer<T>`:

```csharp
using var map = ComparerSwissHashMap<string, int>.Create(
    StringComparer.OrdinalIgnoreCase, capacity: 8);

map.Add("Hello", 1);
Console.WriteLine(map.ContainsKey("HELLO"));  // True
```

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

### Enumeration (zero allocation)

`foreach`, `.Keys`, and `.Values` return value-type enumerators — no boxing, no `IEnumerator<T>` allocation.

```csharp
foreach (var kvp in map)             { /* kvp.Key, kvp.Value */ }
foreach (var key   in map.Keys)      { /* ... */ }
foreach (var value in map.Values)    { /* ... */ }
```

### Pooled arrays and lists

```csharp
// Collection-expression literal over ArrayPool.Shared.
using PooledArray<int> numbers = [1, 2, 3];

// Growable, heap-allocated wrapper.
using var list = new PooledList<int>(initialCapacity: 16);
list.Add(42);

// Struct variant — zero wrapper allocation. Copies share rented buffers; Dispose one.
using var value = new ValuePooledList<int>(16);
value.Add(7);

// Ref-struct variant — compiler-enforced stack-only; an inline Span<T>
// can hold items before the pool rental happens.
Span<int> inline = stackalloc int[8];
using var stack = new StackOnlyPooledList<int>(inline);
stack.Add(99);
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

Map keys must be `IUtf8SpanFormattable` (enforced by the converter's generic constraints).
If you prefer to register converters individually — e.g. to restrict which closed types
are supported — the factory-less form is still there:

```csharp
options.Converters.Add(new PooledArrayConverter<int>());
options.Converters.Add(new PooledListConverter<int>());
options.Converters.Add(new PooledHashSetConverter<int,
    SwissTableAlgorithm<int, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>());
options.Converters.Add(new PooledHashMapConverter<int, string,
    SwissTableAlgorithm<KeyValueSlot<int, string>, int, EqualityDefaultHasher<int>, NoOpHashFinalizer>>());
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

The resolver covers `PooledArray<T>`, `PooledList<T>`, `PooledHashMap<K,V>`, and `PooledHashSet<T>` in one registration — no need to add each formatter individually.

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

- Start with the default Swiss. It is within a few percent of `Dictionary` on lookup workloads and roughly 10–20% faster than `Dictionary` on `Add` (thanks to pooled arrays — no allocation).
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

- **Per-thread cache** (core) — a `[ThreadStatic]` single-slot cache. Zero synchronisation. Ideal for synchronous request handlers and short-lived scopes. Do **not** keep a reference to the map after `Dispose` — another caller on this thread will rent it.
- **`Prest.ObjectPool`** — `Microsoft.Extensions.ObjectPool.DefaultObjectPool` (max 64 retained). Concurrent-safe; instances survive `await` boundaries. Use when the map must outlive a single synchronous scope.

You should never mix the two on the same instance — a map rented from `PooledHashMapPool` returns itself to that pool on `Dispose`, not to the per-thread cache.

## Examples and benchmarks

`examples/Prest.Examples/` — runnable mini-programs for each feature:

- `BasicMap.cs`, `BasicSet.cs` — minimal CRUD.
- `Algorithms.cs` — Swiss / RobinHood / Linear / Chained side-by-side.
- `CustomComparer.cs` — wiring an `IEqualityComparer<T>`.
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
- `FinalizerComparisonBenchmarks.cs` — `NoOp` / `Fibonacci` / `Lowbias32` / `XMX` on Swiss and RobinHood with sequential-int keys.

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
```

The same applies to VYaml: instead of adding `PooledTypeFormatterResolver.Instance` to your resolver chain, construct the concrete `PooledArrayFormatter<T>` / `PooledListFormatter<T>` / `PooledHashMapFormatter<K,V,TAlgo>` / `PooledHashSetFormatter<T,TAlgo>` you need at compile time and compose them into your own `IYamlFormatterResolver`.

## Target frameworks

| Package | net10.0 | net8.0 | netstandard2.1 | netstandard2.0 |
|---|:-:|:-:|:-:|:-:|
| `Prest` | ✓ | ✓ | ✓ | ✓ |
| `Prest.ObjectPool` | ✓ | ✓ | ✓ | ✓ |
| `Prest.SystemTextJson` | ✓ | ✓ | ✓ | ✓ |
| `Prest.SystemTextJson.ObjectPool` | ✓ | ✓ | ✓ | ✓ |
| `Prest.Serializers.SystemTextJson` | ✓ | ✓ | — | — |
| `Prest.Serializers.VYaml` | ✓ | ✓ | ✓ | ✓ |
| `Prest.Immutable` | ✓ | — | — | — |

`Prest.Serializers.SystemTextJson` requires `net8.0+` for `IUtf8SpanFormattable`. `Prest.Immutable` is `net10.0`-only (uses C# extension members).

## License

MIT © kerem-acer. See [`LICENSE`](./LICENSE).
