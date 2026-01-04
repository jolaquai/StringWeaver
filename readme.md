The `StringWeaver` package exposes a custom high-performance builder for `string`s with a mutable, directly accessible buffer and a versatile API for manipulating the contents.

# Consumption

The assembly multi-targets `netstandard2.0` and several newer .NETs to support performance-focused APIs introduced in later versions:

* Core functionality is exposed in the `netstandard2.0` compilation, meaning any conforming project platform can use it.
* A dependency on `PCRE.NET` is introduced to facilitate all regex operations on `StringWeaver` to meet performance goals/allocation minimums for `< net7.0`.
* Several quality-of-life APIs are also introduced in their respective compilations, such as support for `ISpanFormattable` on `>= net6.0`.
* For `>= net7.0`, for the `Replace*` methods that take a `PcreRegex`, analogous methods that take `Regex` instance and utilize the Span-based APIs introduced in `System.Text.RegularExpressions` are also exposed.
* `net8.0` introduced many `Span`-based APIs throughout the entire .NET ecosystem, allowing streamlined code paths on `>= net8.0`.

Unfortunately, neither `PCRE.NET` nor `System.Text.RegularExpressions` expose allocating APIs that would easily allow producing match details object instances using a `Span` input. This also means that typical (allocating) replacement operations that allow dynamically producing replacement content using a `MatchEvaluator` cannot be implemented.

# Variants

For some usage examples, refer to `examples.md`.

## General purpose

Namespace: `global::StringWeaver`

* `StringWeaver`: Default and most versatile implementation. Offers the full API surface and sources its backing buffers by `new`'ing and resizing `char[]`s on demand.
  * The default `StringWeaver` should be the first choice for the ordinary consumer. Where profiling shows that a different implementation would be more beneficial, consider the alternatives described below.
* `UnsafeStringWeaver`: Implements a `StringWeaver` that sources its backing buffers from unmanaged instead of managed memory. This has the benefit of not causing any GC pressure while exposing exactly the same APIs as the default `StringWeaver`.
  * Consider using this if you frequently create `StringWeaver`s with capacities near `int.MaxValue` or instances which are very long-lived.
  * &#x26A0; `UnsafeStringWeaver` implements `IDisposable` (`StringWeaver` does not). Not disposing of instances of `UnsafeStringWeaver` is a memory leak.

## Specialized

Namespace: `global::StringWeaver.Specialized`

* `PooledStringWeaver`: Implements a `StringWeaver` that sources its backing buffers from `ArrayPool<char>.Shared` (or your own passed implementation of `ArrayPool<char>`). This allows keeping GC pressure low while still using managed memory.
  * Consider using this if you frequently create and dispose of `StringWeaver`s with non-trivial capacities (e.g. a few dozen kB or more).
  * &#x26A0; `PooledStringWeaver` implements `IDisposable` (`StringWeaver` does not). Not disposing of instances of `PooledStringWeaver` causes the buffer backing it to be lost to the pool, which is a memory leak and degrades performance of every other user of that `ArrayPool<char>` in your application.
* `WrappingStringWeaver`: A truly zero-allocation wrapper for existing buffers that otherwise offer the full capabilities of the base `StringWeaver` implementation.
  * Use this any time you already have some memory you want to treat as a `char` buffer and want to avoid copying at all costs.
  * &#x26A0; `WrappingStringWeaver` only pins memory for you when specifically asked to do so (and only when given a `char[]` or `Memory<char>`). Given a `Span<char>` or a pointer (whether managed or unmanaged), it is the caller's responsibility to ensure the memory remains valid and accessible for the lifetime of the `WrappingStringWeaver` instance. Changing the contents of the memory outside of the `WrappingStringWeaver` instance should be done with caution but is generally safe (under the same constraints as the default `StringWeaver` implementation, e.g. `EnumerateIndicesOf` `ref struct`s ensuring the buffer wasn't modified during enumeration). See this variant's wiki page for more details and usage examples.
  * While all constructors take buffers or pointers typed as `char` in some form, you can reinterpret any memory as `char` and use it with `WrappingStringWeaver`. Pinned memory reinterpreted using `Unsafe.As<TFrom, TTo>(ref TFrom)` is a good example of this.
  * &#x26A0; `WrappingStringWeaver` implements `IDisposable` (`StringWeaver` does not). If you used one of the overloads that specify that the memory should be pinned, not disposing of the instance is a memory leak.

# Inheritance

`StringWeaver` itself is not `sealed` because `UnsafeStringWeaver` inherits from it. As such, inheritance from `StringWeaver` is allowed for external types as well. There are only a select few members you must override to make your implementation function correctly, everything else (functionality-wise) is handled for you (and not virtual, for that matter):

* `Memory<char> FullMemory`: Gets a `Memory<char>` instance over the entire backing storage (NOT just the used portion).
* `void GrowCore(int requiredCapacity)`: WITHOUT checking whether it is required, expands the backing storage to at least the specified capacity (those checks are done for you before this `virtual` method is ever called).
* &#x26A0; `StringWeaver.ToString` (the base version) is `sealed override`.
* `IBufferWriter<char>` implementations as well as `Stream` and `TextWriter` support through wrappers are both handled internally as well. Do not re-implement any of those.

Adding functionality on top of anything already handled for you is straightforward. All the base functionality can be used to compose your own methods or just use `(Full)Memory`/`(Full)Span` to access the backing storage directly. The `Length` property has an accessible setter that controls which portion of the buffer is considered "used".

&#x26A0; The above statement is true for disposal. As mentioned, `StringWeaver` does _not_ implement `IDisposable`, so derived types must do so themselves if they require it. Ensure cleanup for your own types is sound.

## &#x26A0; v2.0.0+ Breaking Changes

v2.0.0 saw the introduction of breaking changes to further reduce allocations for specific scenarios. If you aren't deriving from `StringWeaver`, you will very likely not be affected by most of these changes.

* `protected int Start { get; set; }` and `protected int End { get; set; }`: These properties delimit the used portion of the buffer. This allows derived types to implement functionality that would otherwise require shifting data around in the backing storage. It is discouraged for custom derived types to modify this directly. The methods `StringWeaver` exposes are well-behaved with respect to this property, if its value is sane at call time.
* `public int Length { get; }`: This property no longer has a setter. It is now a computed property using the new property `StringWeaver.End`.
* `protected internal virtual Memory<char> FullMemory { get; }`: There has been no API change for this property, but the details of exactly what it is expected to return very much has. It must encompass the entirety of the backing memory of the current instance, regardless of the values of `StringWeaver.Start` and `StringWeaver.End`. Before this, this expecation wasn't very explicit; trimming or similar operations kept the used portion of the backing memory aligned to index `0` in the `Memory<char>` returned by this property. This is no longer the case to reduce allocations where possible; however, the above change was made to facilitate this.
* `protected void EnsureZeroAligned()`: Aligns the used portion to index `0` in the backing storage for you using `StringWeaver.Start` and `StringWeaver.End`.
* `protected void ValidateRange(int index, int length)`: Validates a range against the used portion of the buffer, throwing an `ArgumentOutOfRangeException` for you if it is not entirely within bounds.
* `protected virtual void GrowCore(int requiredCapacity)`: Instead of `protected virtual void Grow(int requiredCapacity)`, you now override `GrowCore`. Because increasing `StringWeaver.Start` values effectively reduce the available capacity, a new branch was added that attempts to satisfy the capacity requirement without actually needing to allocate using `EnsureZeroAligned`.
* `protected virtual void ClearCore()`: Introduced to support derived `StringWeaver` types that need to do additional cleanup when `Clear` is called or where clearing is not just setting `StringWeaver.Start` and `StringWeaver.End` to `0`.

On a less... annoying note, `void ReplaceAll(ReadOnlySpan<char>, ReadOnlySpan<char>)` (and the overloads taking an `int index` and `int length`, as well as several methods that delegate to this one) had a path optimized that allocated temporary storage for the replacement process. This memory is now sourced either from a `stackalloc` or from native memory.

# Global configuration

The `static class StringWeaverConfiguration` exposes global configuration options for all `StringWeaver` variants where applicable. The options are usually strongly-typed and implemented in a thread-safe manner, however, altering options while the affected variants are in use will very likely lead to undefined behavior. I recommend very carefully thinking about what you're changing, why you're doing it and if it's really a good idea, then setting them on application startup and never touching the entire class again.

# Contribution

Opening issues and submitting PRs are welcome. All changes must be appropriately covered by tests. Tests run exclusively under `net10.0`.

Support for `netstandard2.0` must always be maintained. If possible, new functionality should be added to all target frameworks. New dependencies may be introduced after I vet the decision to do so.

Or get in touch on Discord `@eyeoftheenemy`