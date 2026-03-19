# StringWeaver

A mutable string builder that lets you chain replacements, regex transforms, and trims in-place — without allocating a new string at every step.

> **Note:** `StringWeaver` is _not_ a drop-in replacement for `StringBuilder`. Its single contiguous buffer enables `Span<char>` access and in-place regex operations, but growing copies the entire buffer. See the [wiki Examples page](wiki/Examples.md) for guidance on when to choose one over the other.

## Why?

Every `string.Replace` or `Regex.Replace` allocates a brand-new string. Chain a few together in a cleanup pipeline and you're creating dozens of throwaway copies:

```csharp
using System.Text.RegularExpressions;

// Classic approach — each call allocates a new string
string result = input
    .Replace("\r\n", "\n")
    .Replace("\t", " ")
    .Trim();
result = Regex.Replace(result, @"\s{2,}", " ");
result = Regex.Replace(result, @"<[^>]+>", "");
```

StringWeaver performs all of these operations on **one buffer**. No intermediate string is created between steps — the only `string` allocation is the one `ToString()` returns:

```csharp
using PCRE;
using SW = StringWeaver.StringWeaver;

var sw = new SW(input);       // one buffer, seeded with input
sw.ReplaceAll("\r\n", "\n");  // in-place
sw.ReplaceAll("\t", " ");     // in-place
sw.Trim();                    // in-place
sw.ReplaceAll(new PcreRegex(@"\s{2,}"), " ");
sw.ReplaceAll(new PcreRegex(@"<[^>]+>"), "");
string result = sw.ToString(); // the only string allocation
```

This matters when you're cleaning user input, sanitizing HTML, normalizing log lines, or running any pipeline where text passes through multiple transformation steps.

## Quick start

```
dotnet add package StringWeaver
```

```csharp
using SW = StringWeaver.StringWeaver;

// Create from a string, span, byte[], or another StringWeaver
var sw = new SW("Hello, World!");

// Mutate in-place
sw.ReplaceAll("World", "NuGet");
sw.Append(" 🎉");
sw.Trim('!');

// Read the result
string final = sw.ToString();
// Or access the buffer directly
ReadOnlySpan<char> span = sw.Span;
```

The full API includes `Append`, `Replace`, `ReplaceAll`, `Remove`, `Trim`, `TrimStart`, `TrimEnd`, `TrimSequence`, `Clear`, `Drain`, indexer access via `Index`/`Range`, and `IndexOf`/`EnumerateIndicesOf` for searching — all operating in-place on the same buffer. Regex overloads accept either `PcreRegex` or `System.Text.RegularExpressions.Regex` (on `>= net7.0`).

For more examples, see the [wiki Examples page](wiki/Examples.md).

## Regex without allocating match objects

Neither `PCRE.NET` nor `System.Text.RegularExpressions` expose APIs that produce allocating match-detail objects from a `Span` input. The traditional `MatchEvaluator` delegate pattern is therefore not supported. Instead, StringWeaver exposes the `StringWeaverWriter` delegate (`void(Span<char> buffer, ReadOnlySpan<char> match)`) together with a `bufferSize` parameter, which allows dynamically generating replacement content with zero allocations.

## Variants

Start with the default `StringWeaver`. Switch to an alternative only when profiling shows it would help.

| Type | Namespace | Backing memory | `IDisposable` | When to use |
|---|---|---|---|---|
| `StringWeaver` | `StringWeaver` | `char[]` (managed) | No | Default choice. |
| `UnsafeStringWeaver` | `StringWeaver` | Unmanaged (`Marshal`) | **Yes** | Very large or very long-lived buffers; avoids GC pressure entirely. |
| `PooledStringWeaver` | `StringWeaver.Specialized` | `ArrayPool<char>.Shared` | **Yes** | Frequent create/dispose cycles with non-trivial capacities (dozens of kB+). |
| `WrappingStringWeaver` | `StringWeaver.Specialized` | Caller-provided buffer | **Yes** | You already own the memory and want zero-copy access to the full API. Disposing is required when constructed with pinned memory; always recommended otherwise. |

⚠️ Variants marked `IDisposable` **must** be disposed. Failing to do so leaks memory (or, for `PooledStringWeaver`, degrades pool performance app-wide). `WrappingStringWeaver` given a `Span<char>` or pointer requires the caller to keep the memory valid for the wrapper's lifetime.

## Framework support

The assembly multi-targets `netstandard2.0`, `net6.0`, `net7.0`, and `net8.0`:

* **`netstandard2.0`** — full core functionality; works on any conforming platform.
* **`>= net6.0`** — quality-of-life additions such as `ISpanFormattable` support.
* **`>= net7.0`** — `Replace*` and `EnumerateIndicesOf*` overloads accepting `System.Text.RegularExpressions.Regex` (span-based API).
* **`>= net8.0`** — streamlined code paths using the `Span`-based APIs introduced across the .NET ecosystem.

A dependency on [`PCRE.NET`](https://github.com/ltrzesniewski/pcre-net) is used across all targets as the primary regex engine.

## Inheritance

`StringWeaver` is not `sealed`; you can derive from it. Override these two members to provide your own backing storage:

* `Memory<char> FullMemory` — the **entire** backing memory, not just the used portion.
* `void GrowCore(int requiredCapacity)` — expand storage to at least the given capacity (pre-validated by the base class).

Everything else — `IBufferWriter<char>`, `Stream`/`TextWriter` wrappers, all public mutation methods — is handled for you. `Start`, `End`, and `Length` control which portion of the buffer is considered "used". `ToString()` is `sealed override`.

⚠️ The base `StringWeaver` does _not_ implement `IDisposable`. Derived types that manage their own resources must implement it themselves.

<details>
<summary>⚠️ v2.0.0+ breaking changes (click to expand)</summary>

These changes further reduce allocations. If you are **not** deriving from `StringWeaver`, you are very likely unaffected.

* `Start` / `End` are now `protected int` properties that delimit the used portion of the buffer. `Length` is computed from `End - Start` and no longer has a setter.
* `FullMemory` must return the **entirety** of the backing memory regardless of `Start`/`End`. Trimming no longer keeps data zero-aligned.
* `Grow(int)` was renamed to `GrowCore(int)`. A new branch attempts to satisfy capacity requirements via `EnsureZeroAligned()` before allocating.
* New helpers: `EnsureZeroAligned()`, `ValidateRange(int, int)`, `ClearCore()`.
* `ReplaceAll(ReadOnlySpan<char>, ReadOnlySpan<char>)` (and related overloads) no longer allocates temporary managed storage; it uses `stackalloc` or native memory instead.

</details>

## Global configuration

`StringWeaverConfiguration` exposes global, thread-safe options for all variants. Set them once on application startup — changing them while instances are in use leads to undefined behavior.

## Contribution

Issues and PRs are welcome. All changes must be covered by tests. Tests run exclusively under `net10.0`.

`netstandard2.0` support must always be maintained. New functionality should target all frameworks when possible. New dependencies require maintainer approval.

Discord: `@eyeoftheenemy`
