The `StringWeaver` package exposes a custom high-performance builder for `string`s with a mutable, directly accessible buffer and a versatile API for manipulating the contents.

# Consumption

The assembly multi-targets `netstandard2.0` and several newer .NETs to support performance-focused APIs introduced in later versions.

* Core functionality is exposed in the `netstandard2.0` compilation, meaning any conforming project platform can use it.
* A dependency on `PCRE.NET` is introduced to facilitate all regex operations on `StringWeaver` to meet performance goals/allocation minimums for `< net7.0`.
* Several quality-of-life APIs are also introduced in their respective compilations, such as support for `ISpanFormattable` on `>= net6.0`.
* For `>= net7.0`, for the `Replace*` methods that take a `PcreRegex`, analogous methods that take `Regex` instance and utilize the Span-based APIs introduced in `System.Text.RegularExpressions` are also exposed.
* `net8.0` introduced many `Span`-based APIs throughout the entire .NET ecosystem, allowing streamlined code paths on `>= net8.0`.

Unfortunately, neither `PCRE.NET` nor `System.Text.RegularExpressions` expose allocating APIs that would easily allow producing match details object instances using a `Span` input. This also means that typical (allocating) replacement operations that allow dynamically producing replacement content using a `MatchEvaluator` cannot be implemented.

# Variants

## General purpose

Namespace: `global::StringWeaver`

* `StringWeaver`: Default and most versatile implementation. Offers the full API surface and sources its backing buffers by `new`'ing and resizing `char[]`s on demand.
  * The default `StringWeaver` should be the first choice for the ordinary consumer. Where profiling shows that a different implementation would be more beneficial, consider the alternatives described below.
* `UnsafeStringWeaver`: Implements a `StringWeaver` that sources its backing buffers from unmanaged instead of managed memory. This has the benefit of not causing any GC pressure while exposing exactly the same APIs as the default `StringWeaver`.
  * Consider using this if you frequently create `StringWeaver`s with capacities near `int.MaxValue` or instances which are very long-lived.
  * (!) Note that `UnsafeStringWeaver` implements `IDisposable` (`StringWeaver` does not). Not disposing of instances of `UnsafeStringWeaver` is a memory leak.

## Specialized

Namespace: `global::StringWeaver.Specialized`

* `PooledStringWeaver`: Implements a `StringWeaver` that sources its backing buffers from a `System.Buffers.ArrayPool<char>` to minimize allocations.
  * Managed entirely by `PooledStringWeaverManager`, which supports several configuration settings you may use to control the behavior of the pool.
  * Unique to this implementation is the fact that the backing storage may end up fragmented/non-contiguous. While this is mostly an implementation detail, it has functional and performance implications when access to the backing storage is required. Since the type cannot guarantee direct access to be valid, writers must use the `IBufferWriter<char>` API. Readers may use the `GetReadOnlySequence` method to obtain a `ReadOnlySequence<char>`, giving a chunked and immutable, but zero-copy view over the buffer contents.
  * Consider using this if you frequently create short-lived `StringWeaver`s with medium capacities.
  * There are several 

# Inheritance

`StringWeaver` itself is not `sealed` because `UnsafeStringWeaver` inherits from it. As such, inheritance from `StringWeaver` is allowed for external types as well. There are only a select few members you must override to make your implementation function correctly, everything else (functionality-wise) is handled for you (and not virtual, for that matter):

* `Memory<char> FullMemory`: Gets a `Memory<char>` instance over the entire backing storage (NOT just the used portion).
* `void Grow(int requiredCapacity)`: WITHOUT checking whether it is required, expands the backing storage to at least the specified capacity (those checks are done for you before this `virtual` method is ever called). It is recommended you decorate your `override` with `[MethodImpl(MethodImplOptions.NoInlining)]`.
* (!) Note that `ToString` is `sealed override` in the base `StringWeaver`.
* `IBufferWriter<char>` implementations and `Stream` support are both handled internally as well. Do not re-implement either of those.

Adding functionality on top of anything already handled for you is straightforward. All the base functionality can be used to compose your own methods or just use `(Full)Memory`/`(Full)Span` to access the backing storage directly. The `Length` property has an accessible setter that controls which portion of the buffer is considered "used".

Note that the above setup is the reason `PooledStringWeaver` does not inherit from and exposes an API surface vastly different than that of `StringWeaver`: multiple non-contiguous pooled buffers (or ones larger in size than `int.MaxValue`) cannot be exposed as a single `Memory<char>`.

# Global configuration

The `static class StringWeaverConfiguration` exposes global configuration options for all `StringWeaver` variants where applicable. The options are usually strongly-typed and implemented in a thread-safe manner, however, altering options while the affected variants are in use will very likely lead to undefined behavior. I recommend very carefully thinking about what you're changing, why you're doing it and if it's really a good idea, then setting them on application startup and never touching the entire class again.

# Contribution

Opening issues and submitting PRs are welcome. All changes must be appropriately covered by tests.
Support for `netstandard2.0` must always be maintained. If possible, new functionality should be added to all target frameworks. New dependencies may be introduced after I vet the decision to do so.

Or get in touch on Discord `@eyeoftheenemy`