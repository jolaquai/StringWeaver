# AGENTS.md

## Project Overview

StringWeaver is a high-performance C# string builder library with a mutable, directly accessible buffer.
It multi-targets `netstandard2.0`, `net6.0`, `net7.0`, and `net8.0`. Tests run exclusively on `net10.0`.

## Build / Lint / Test Commands

```bash
# Restore dependencies
dotnet restore

# Build entire solution
dotnet build

# Build in Release mode (also generates NuGet package)
dotnet build -c Release

# Run all tests
dotnet test

# Run a single test by fully qualified name
dotnet test --filter "FullyQualifiedName~StringWeaver.Tests.StringWeaverTests.Append_MultipleChars_BuildsString"

# Run tests matching a pattern
dotnet test --filter "FullyQualifiedName~Constructor"

# Run tests in a specific test class
dotnet test --filter "FullyQualifiedName~StringWeaver.Tests.Specialized.PooledStringWeaverTests"
```

The test runner is Microsoft.Testing.Platform (configured in `global.json`). The test framework is xUnit v3.

There is no separate lint command. Static analysis is configured via `<AnalysisMode>recommended</AnalysisMode>`
and `<AnalysisModePerformance>all</AnalysisModePerformance>` in `StringWeaver.csproj`. Warnings surface during build.

Suppressed warnings (project-level): `CA1510`, `CA1513`, `CA1720` (library), `SYSLIB1045` (tests).

## Project Structure

```
StringWeaver/                    # Library project (multi-target)
  StringWeaver.cs                # Main partial class - core API
  UnsafeStringWeaver.cs          # Variant using unmanaged memory
  StringWeaverConfiguration.cs   # Global config options
  StringWeaverExtensions.cs      # Extension methods (static partial)
  Helpers/                       # Internal utilities (Pow2, NativeBuffer<T>, MemoryViewProvider<T>, etc.)
  IO/                            # I/O adapters (WeaverTextWriter, WeaverStream)
  Specialized/                   # WrappingStringWeaver, PooledStringWeaver
StringWeaver.Tests/              # Test project (net10.0 only, xUnit v3)
  StringWeaver.Tests.cs          # Tests for the main class
  UnsafeStringWeaver.Tests.cs    # Tests for UnsafeStringWeaver
  Helpers/                       # Tests for helper classes
  IO/                            # Tests for I/O adapters
  Specialized/                   # Tests for specialized variants
```

Namespaces mirror the directory structure: `StringWeaver`, `StringWeaver.Helpers`, `StringWeaver.IO`,
`StringWeaver.Specialized`. Test namespaces: `StringWeaver.Tests`, `StringWeaver.Tests.Helpers`, etc.

`InternalsVisibleTo` is set for `StringWeaver.Tests`, so tests can access `internal` members directly.

## Code Style

### Formatting
- 4-space indentation, no tabs.
- Allman-style braces (opening brace on its own line).
- File-scoped namespaces: `namespace StringWeaver;`
- No enforced line length limit.

### Naming
- **Classes/structs/enums**: PascalCase (`StringWeaver`, `NativeBuffer<T>`, `MemorySource`).
- **Methods/properties**: PascalCase (`Append`, `GrowCore`, `FreeCapacity`).
- **Private fields**: `_camelCase` with underscore prefix (`_weaver`, `_pool`, `_memorySource`).
  Exception: core backing fields like `buffer`, `pointer`, `disposed` may omit the underscore,
  especially when `internal` and accessed by tests.
- **Parameters/locals**: camelCase (`capacity`, `requiredCapacity`, `span`).
- **Constants**: PascalCase (`DefaultCapacity`, `MaxCapacity`, `SafeCharStackalloc`).
- **Throw helpers**: `Throw` prefix (`ThrowResizeNotSupported`, `ThrowModifiedException`).

### Types and Nullability
- Nullable reference types are **disabled** project-wide (`<Nullable>disable</Nullable>`).
- Optional parameters use `= null` defaults instead of nullable annotations.
- Use `var` pervasively when the type is apparent from the right-hand side.
- Explicit types only when `var` cannot be used (e.g., `Span<char>`, uninitialized declarations).

### Access Modifiers
- Always explicit (never rely on implicit `private` or `internal`).
- Modifier order: `access [static] [unsafe] [sealed|abstract|virtual|override|new] [readonly] [partial]`.
- Helper/infrastructure classes default to `internal`.
- Derived types that should not be subclassed are `sealed`.
- `protected internal` for base class members needed by derived types and tests.

### Expression Bodies vs Block Bodies
- Expression-bodied (`=>`) for single-expression methods and properties.
- Block bodies for anything with control flow or multiple statements.
- Properties with attributes on accessors use block syntax with expression-bodied getter:
  ```csharp
  public int Length
  {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => End - Start;
  }
  ```

### Pattern Matching
- Prefer `is null` / `is not null` over `== null` / `!= null`.
- Use `switch` expressions for enum dispatch.

### Imports / Usings
- Global usings are defined in `.csproj` files via `<Using Include="..." />`, not in `GlobalUsings.cs`.
- Both projects enable `<ImplicitUsings>enable</ImplicitUsings>`.
- Per-file usings are minimal. When present: System usings first, blank line, third-party, blank line, project-internal.
- Type aliases when needed to avoid ambiguity: `using SW = StringWeaver.StringWeaver;`.

### Regions
- Extensive `#region` / `#endregion` usage to organize large files.
- Common regions: `#region .ctors`, `#region Props/Indexers`, `#region Grow`, `#region Cleanup`.

### Conditional Compilation
- Heavy use of `#if` directives for multi-targeting:
  - `#if NET6_0_OR_GREATER`, `#if NET7_0_OR_GREATER`, `#if !NETSTANDARD2_0`, etc.
- API members are conditionally compiled out for older targets (not runtime-checked).

### Performance Annotations
- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on hot-path methods.
- `[MethodImpl(MethodImplOptions.NoInlining)]` on cold-path / throw-helper methods.
- `unsafe` keyword on classes with pointer fields; `AllowUnsafeBlocks` enabled project-wide.

### Error Handling
- Guard clauses at method entry throwing standard exceptions with `nameof()`.
- `ArgumentOutOfRangeException`, `ArgumentNullException`, `ArgumentException` for invalid inputs.
- `ObjectDisposedException` via `EnsureUsable()` guard methods.
- `NotSupportedException` for structurally impossible operations.
- `InvalidOperationException` for state violations.
- `[DoesNotReturn]` attribute on throw helpers (conditional on `!NETSTANDARD2_0`).
- Thread-safe dispose using `Interlocked.Exchange` / `Interlocked.CompareExchange`.

### Documentation
- XML doc comments (`<summary>`, `<param>`, `<returns>`, `<exception>`, `<remarks>`) on all public/protected members.
- `<inheritdoc/>` on overrides where base docs suffice.
- `<see cref="..."/>` and `<see langword="..."/>` for cross-references.
- Internal classes may use plain `//` comments instead of XML docs.
- Pragma warnings include the warning code and description: `#pragma warning disable CA1815 // Override equals...`

## Test Conventions

- Test framework: **xUnit v3** (3.2.0) with `[Fact]`, `[Theory]`, `[InlineData]`, `[MemberData]`.
- Test class naming: mirrors source with `.Tests` suffix (e.g., `StringWeaverTests`, `PooledStringWeaverTests`).
- Test file naming: `SourceFile.Tests.cs` (e.g., `StringWeaver.Tests.cs`, `WeaverStream.Tests.cs`).
- Test method naming: `MethodOrScenario_Condition_ExpectedResult` with underscore separators.
  Examples: `Constructor_DefaultCapacity_InitializesEmpty`, `Append_NullArray_Throws`.
- Arrange-Act-Assert pattern (implicit, without section comments).
- Short throw-tests use expression-bodied form: `public void Foo_Throws() => Assert.Throws<T>(() => ...);`
- Tests are grouped with `#region`: `#region Constructor Tests`, `#region Append Tests`, etc.
- Common local variable names: `sw` for StringWeaver, `weaver` for variants, `w` for pooled.
- All changes must be covered by tests. Tests run exclusively under `net10.0`.

## Contribution Notes (from readme.md)

- All changes must be appropriately covered by tests.
- `netstandard2.0` support must always be maintained.
- New functionality should target all frameworks when possible.
- New dependencies require maintainer approval.
