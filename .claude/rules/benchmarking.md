---
description: Rules for writing and modifying benchmarks
globs: ["**/benchmarks/**", "**/*Benchmarks*.cs"]
---

# Benchmark Authoring Rules

## Framework

- Use **BenchmarkDotNet** — `[Benchmark]`, `[GlobalSetup]`, `[Params]`.
- Always apply `[MemoryDiagnoser]` at class level.
- BenchmarkDotNet and U8String come from central package management — don't pin versions in the benchmark csproj.

## Architecture

- **No base classes.** Every benchmark class is self-contained.
- Shared data generation lives in `TestData` (static methods).
- Shared encoding conversion lives in `EncodedSet` (record struct with factory).
- Shared parameter values live in custom `ParamsAttribute` subclasses (`BenchmarkParams.cs`).
- One class per file. One file per method per encoding.

## Directory Structure

Benchmarks are organized **by method, then by encoding** — not by encoding first:

```
benchmarks/
  Search/
    Contains/
      ContainsUtf8Benchmarks.cs
      ContainsUtf16Benchmarks.cs
      ContainsUtf32Benchmarks.cs
    ByteIndexOf/
      ...
  Mutation/
    Replace/
      ReplaceUtf8Benchmarks.cs
      ...
    ToUpper/
      ...
  Concat/
    TextConcatUtf8Benchmarks.cs
    TextConcatUtf16Benchmarks.cs
    TextConcatUtf32Benchmarks.cs
  Builder/
    TextBuilderUtf8Benchmarks.cs
    ...
  Creation/
    ByteArray/
      TextCreationByteArrayUtf8Benchmarks.cs
      TextCreationByteArrayUtf16Benchmarks.cs
      TextCreationByteArrayUtf32Benchmarks.cs
    Span/
      ...
    ImmutableArray/
      TextCreationImmutableArrayUtf8Benchmarks.cs
    String/
      ...
    CharArray/
      ...
    CharSpan/
      ...
    IntArray/
      ...
    IntSpan/
      ...
  Interpolation/
    TextInterpolationUtf8Benchmarks.cs
    ...
  Split/
    TextSplitUtf8Benchmarks.cs
    ...
  Pipeline/
    HttpPipelineBenchmarks.cs
    JsonSerializationBenchmarks.cs
  Shared/
    EncodedSet.cs
    TestData.cs
    BenchmarkParams.cs
    Script.cs
```

## Naming

- **Class:** `{Method}{Utf8|Utf16|Utf32}Benchmarks` for search/equality, `{Feature}{Utf8|Utf16|Utf32}Benchmarks` for others.
- **File:** matches class name.
- **Method:** `{Library}{Operation}` for hit, `{Library}{Operation}_Miss` for miss, `{Library}{Operation}_Diff` for different-value.
- **Description attribute:** `"{Library}.{Method}"` or `"{Library}.{Method} {encoding}"` or `"{Library}.{Method} {encoding} miss"`.

## Code Style

Respect `.editorconfig` and `.globalconfig`. Key rules for benchmarks:

- **Attributes on their own line** — never place `[Params]` or custom param attributes on the same line as the field declaration.
- **Omit default accessibility** — omit `private` on fields (implicit per `resharper_csharp_default_private_modifier = implicit`).
- **LF line endings**, UTF-8 charset, final newline required.
- **File-scoped namespaces** — `namespace Glot.Benchmarks;` (not block-scoped).

## Structure

Class layout order:

1. Custom param attributes on fields (`[SearchSizeParams]`, `[ScriptParams]`) — attribute on its own line
2. Private fields (`EncodedSet` preferred — see EncodedSet section below)
3. `[GlobalSetup]` method named `Setup`
4. Benchmark methods — baseline first, then competitors, then Text variants, then miss/diff variants

## Parameters

Use custom attributes from `BenchmarkParams.cs` instead of inline `[Params(...)]`:

| Attribute | Values | Use for |
|---|---|---|
| `[SearchSizeParams]` | 64, 256, 4096, 65536 | Search/contains/index benchmarks |
| `[EqualitySizeParams]` | 8, 64, 256, 4096, 65536 | Equals, CompareTo, GetHashCode |
| `[ScriptParams]` | Ascii, Latin, Cjk, Emoji, Mixed | All locale-parameterized benchmarks |
| `[PartSizeParams]` | 1, 8, 64, 1024 | Builder, concat, interpolation benchmarks |

- Add new custom attributes to `BenchmarkParams.cs` when a parameter set is reused across multiple classes.
- Use inline `[Params(...)]` only for one-off parameters unique to a single class (e.g. `Parts`).
- **Never modify `[Params]` values to reduce runtime.** Use `--filter` or `--param:` CLI args instead.

## Baselines

Each encoding group has a native-encoding baseline:

| Group | Baseline | Rationale |
|---|---|---|
| `Utf8` | `Span<byte>` operations or `U8String` | Both operate on UTF-8 bytes natively |
| `Utf16` | `string` | Native UTF-16 representation in .NET |
| `Utf32` | none | No established UTF-32 competitor exists |

- **`string` is UTF-16** — never use `string` as baseline in a UTF-8 benchmark class.
- For UTF-8 benchmarks, use raw `Span<byte>` operations (e.g. `Span.IndexOf`, `Span.LastIndexOf`, `HashCode.AddBytes`) or `U8String` as baseline.
- Mark exactly one method `Baseline = true` per class (or per category if using `[BenchmarkCategory]`).
- If the class has no competitor, omit `Baseline = true` entirely.

## Required Competitors

Every benchmark class should include these competitors where applicable:

| Competitor | When to include | Example |
|---|---|---|
| `string` | UTF-16 classes, or as cross-reference | `_source.Str.Contains(...)` |
| `Span<byte>` / raw bytes | UTF-8 classes | `_haystack.RawBytes.AsSpan().IndexOf(_needle.RawBytes)` |
| `U8String` | UTF-8 classes (search/equality) | `_haystack.U8.Contains(_needle.U8)` |
| `Text` | All classes | The type under test |

## EncodedSet

`EncodedSet` is a `record struct` that holds a string in all six representations: `Str`, `RawBytes`, `Utf8`, `Utf16`, `Utf32`, `U8`.

- **Always use `EncodedSet`** for benchmark data — never manually call `Encoding.UTF8.GetBytes()` or create separate `string[]`, `byte[][]`, `Text[]` fields when `EncodedSet` can hold them.
- Use `EncodedSet.From(string)` whenever a benchmark needs the same value in multiple encodings.
- Access fields directly: `_haystack.Utf8`, `_needle.RawBytes`, `_a.Str`, `_source.U8`.
- When APIs need typed arrays (e.g. `string[]` for `string.Concat`), derive them from `EncodedSet[]` in Setup — not as independently constructed fields.

```csharp
// Search: haystack + needle + missing needle
EncodedSet _haystack, _needle, _missingNeedle;

// Equality: two equal values + one different
EncodedSet _a, _b, _diff;

// GetHashCode: single value
EncodedSet _a;

// Concat: array of parts, with derived typed arrays for APIs that need them
EncodedSet[] _parts = null!;
string[] _strings = null!;     // derived from _parts in Setup
Text[] _textsUtf8 = null!;     // derived from _parts in Setup
```

## Encoding Coverage

Each benchmark class tests **one encoding group** (Utf8, Utf16, or Utf32). Cross-encoding operations (e.g. UTF-16 needle in UTF-8 haystack) belong in the haystack's encoding class.

For search benchmarks, also test **miss scenarios** (needle not found) for every encoding variant.

For equality benchmarks, test **different-value scenarios**.

## Categories

Use `[BenchmarkCategory]` + `[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]` + `[CategoriesColumn]` when a single class benchmarks **multiple distinct operations** (e.g. Split + EnumerateRunes). Each category gets its own baseline.

Do **not** use categories when a class benchmarks one operation — just use a flat class.

## Pooled / Disposable Results

Benchmarks returning pooled or disposable results use `void` return and `using var`:

```csharp
[Benchmark(Description = "Text.ReplacePooled")]
public void TextReplacePooled()
{
    using var result = _source.Utf8.ReplacePooled(_marker.Utf8, _replacement.Utf8);
}
```

## TestData Helpers

| Method | Returns | Purpose |
|---|---|---|
| `Generate(int, Script)` | `string` | Repeating pattern of target char length |
| `GenerateCsv(int, Script)` | `string` | Comma-separated segments for split benchmarks |
| `Needle(Script)` | `string` | Short substring guaranteed to exist in `Generate` output |
| `MissingNeedle(Script)` | `string` | Short substring guaranteed NOT to exist in `Generate` output |
| `Mutate(string)` | `string` | Copy with last character changed (safe for surrogate pairs) |
| `MarkerPair(Script)` | `(string, string)` | Marker + replacement pair for replace benchmarks |

## Commands

```bash
# Build
dotnet build benchmarks/Glot.Benchmarks.csproj -c Release

# Short run (reduced warmup/iteration counts, for validation)
dotnet run --project benchmarks/Glot.Benchmarks.csproj -c Release -- \
  --filter '*ContainsUtf8*' --job short

# Dry run (1 iteration, fastest — just verifies benchmarks run)
dotnet run --project benchmarks/Glot.Benchmarks.csproj -c Release -- \
  --filter '*ContainsUtf8*' --job dry

# In-process run (skip per-benchmark child process; weaker isolation, much faster startup)
dotnet run --project benchmarks/Glot.Benchmarks.csproj -c Release -- \
  --filter '*ContainsUtf8*' --in-process

# Filter by parameter (use actual param names from the class)
dotnet run --project benchmarks/Glot.Benchmarks.csproj -c Release -- \
  --filter '*ContainsUtf8*' --param:N=256 --param:Locale=Ascii

# List all benchmarks
dotnet run --project benchmarks/Glot.Benchmarks.csproj -c Release -- --list flat

# Full report
./benchmarks/run-report.sh
```

## Don'ts

- **Don't** use abstract base classes for benchmarks — each class is self-contained.
- **Don't** manually encode bytes — use `EncodedSet.From()` and access `.RawBytes`, `.Utf8`, `.U8`, etc.
- **Don't** create separate `string[]`, `byte[][]`, `Text[]` fields independently — derive them from `EncodedSet[]` in Setup.
- **Don't** use `string` as baseline in UTF-8 benchmark classes — `string` is UTF-16.
- **Don't** modify `[Params]` attribute values to speed up runs — use `--filter` and `--param:` CLI args.
- **Don't** add BenchmarkDotNet or U8String versions to the benchmark csproj — central package management handles this.
- **Don't** create a benchmark without a competitor baseline (except Utf32 group where none exists).
- **Don't** test only the hit path — always include miss/different scenarios for search and equality benchmarks.
- **Don't** organize directories by encoding first — organize by method/feature, then encoding files within.
