---
description: Rules for writing and modifying unit tests
globs: ["**/*Tests*/**", "**/*Test*.cs", "**/tests/**"]
---

# Test Authoring Rules

## Framework

- Use **TUnit** — `[Test]`, `[Arguments]`, `await Assert.That()`.
- Do NOT use xUnit (`[Fact]`, `[Theory]`) or NUnit (`[TestCase]`) attributes.
- **Setup:** use constructors with `readonly` fields for sync setup. Use `[Before(Test)]` only when async or special lifecycle behavior is needed. Do NOT use `null!` field initializers.
- TUnit comes from `Directory.Build.props` — don't add it to individual test csproj files.

## Naming

- Test method: `{MethodName}_{Scenario}_{ExpectedResult}`
- Test class: `{ClassName}Tests` matching the class under test
- Partial class files: `{ClassName}Tests.{MethodGroup}.cs`

## Structure

- Always use AAA with comments: `// Arrange`, `// Act`, `// Assert`
- Never use `#region` — split into partial class files instead.
- Asserted values must be explicit in the test body, not hidden in helper defaults.
- **No duplicated literals between Arrange and Assert:** extract shared values as local `const` variables and use them in both places.
- Tests at top of file, utility/helper methods at bottom.

## Ref Struct Considerations

- Prest ref-struct types: `StackOnlyPooledList<T>`, `StackOnlyPooledMap<K,V>`, `StackOnlyPooledHashSet<T>`. A `ref struct` cannot live across `await` boundaries.
- **Extract all values before the first `await`:** call methods on the ref struct and store results in regular variables (int, bool, etc.) before asserting.
- Do NOT pass a ref struct to async lambdas or capture it in closures.
- Dispose inline within the sync portion of the test (`using var map = ...;`) — the `using` scope must not cross an `await`.

## Assertions

TUnit built-in assertions:

```csharp
// Value equality
await Assert.That(x).IsEqualTo(y);

// Boolean
await Assert.That(x).IsTrue();
await Assert.That(x).IsFalse();

// Comparison
await Assert.That(x).IsGreaterThan(y);
await Assert.That(x).IsLessThan(y);

// Type checking
await Assert.That(result).IsTypeOf<ExpectedType>();

// Exceptions (async)
await Assert.That(async () => await method()).ThrowsExactly<ArgumentException>();

// Exceptions (sync, value-returning)
await Assert.That(() => _ = method()).Throws<ArgumentException>();
```

Prest does **not** use Verify or snapshot testing — all assertions go through `Assert.That(...)`. For multi-property objects, write one assertion per property; there is no `Verify()` shortcut.

## Generic Algorithm Tests

Prest's hashtable algorithms (`SwissTableAlgorithm`, `LinearProbingAlgorithm`, `RobinHoodAlgorithm`, `ChainedAlgorithm`) share a contract. Prefer a single test body parameterized by `[Arguments(typeof(...))]` that closes the generic over each algorithm, so every algorithm is covered by the same assertions. Add algorithm-specific tests only for behavior that is genuinely unique to one algorithm (e.g. Robin Hood's backward-shift, SwissTable's tombstone-to-empty transition, Chained's swap-with-last).

## Coverage

- 95%+ recommended, 100% aspired
- Generated code excluded via `coverage.settings.xml` (`.g.cs` files + `[ExcludeFromCodeCoverage]`)
- **Dead code rule:** if code is not truly reachable by any user, remove it from production instead of writing a test for it. Only test code that users (or the library itself) can actually reach.

### Commands

```bash
dotnet build Prest.slnx -c Release

dotnet test --solution Prest.slnx -c Release -- \
  --coverage --coverage-settings coverage.settings.xml \
  --coverage-output-format cobertura \
  --coverage-output coverage.cobertura.xml

reportgenerator \
  -reports:"tests/*/bin/Release/net10.0/TestResults/coverage.cobertura.xml" \
  -targetdir:TestResults/CoverageReport \
  -reporttypes:"TextSummary"
```

## TDD Workflow (recommended)

1. Write failing test (red)
2. Implement minimum to pass (green)
3. Refactor, keep green
4. Check coverage

## Don'ts

- **Don't** use `--collect:"XPlat Code Coverage"` — TUnit uses `--coverage` flag
- **Don't** use `dotnet test <solution>` — use `dotnet test --solution <solution>`
- **Don't** test record-generated equality/GetHashCode — records handle this
- **Don't** duplicate transitive package references in test csproj — if the production project references a package, it's already available transitively
- **Don't** use Verify / snapshot testing — Prest relies solely on `Assert.That(...)`
