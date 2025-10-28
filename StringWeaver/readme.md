The `StringWeaver` package exposes a custom high-performance builder for `string`s with a mutable, directly accessible buffer and a versatile API for manipulating the contents.

# Consumption

The assembly multi-targets `netstandard2.0` and several newer .NETs to support performance-focused APIs introduced in later versions.

* Core functionality is exposed in the `netstandard2.0` compilation, meaning any conforming project platform can use it.
* A dependency on `PCRE.NET` is introduced to facilitate all regex operations on `StringWeaver` to meet performance goals/allocation minimums for `< net7.0`.
* Several quality-of-life APIs are also introduced in their respective compilations, such as support for `ISpanFormattable` on `>= net6.0`.
* For `>= net7.0`, for the `Replace*` methods that take a `PcreRegex`, analogous methods that take `Regex` instance and utilize the Span-based APIs introduced in `System.Text.RegularExpressions` are also exposed.
* `net8.0` introduced many `Span`-based APIs throughout the entire .NET ecosystem, allowing streamlined code paths on `>= net8.0`.

# Variants

* `StringWeaver`: Default and most versatile implementation. Offers the full API surface and sources its backing buffers by `new`'ing and resizing `char[]`s on demand.
  * The default `StringWeaver` should be the first choice for the ordinary consumer. Where profiling shows that a different implementation would be more beneficial, consider the alternatives described below.
* `PooledStringWeaver`: Implements essentially exactly the same API as `StringWeaver`, except sourcing its backing buffers from `System.Buffers.ArrayPool<char>.Shared` to minimize allocations.
  * Unique to this implementation is the fact that the backing storage may end up fragmented/non-contiguous. While this is mostly an implementation detail, it has functional and performance implications when access to the backing storage is required. Since the type cannot guarantee direct access to be valid, writers must use the `IBufferWriter<char>` API. Readers may use the `GetReadOnlySequence` method to obtain a `ReadOnlySequence<char>`, giving a chunked, immutable view over the buffer contents.
  * Consider using this if you frequently create short-lived `StringWeaver`s with medium capacities.
* `UnsafeStringWeaver`: Implements a `StringWeaver` that sources its backing buffers from unmanaged instead of managed memory. This has the benefit of not causing any GC pressure, but significantly limits managed representation capabilities. Use this type only when expecting very large buffers, long lifetimes and minimal conversion requirements to managed representations of the data (which includes conversion to `string`).
* `ValueStringWeaver`: Implements a `ref struct` version of `StringWeaver` which sources its backing buffers from the stack.
  * This variant exposes the most limited subset of the full base `StringWeaver` API surface but simultaneously has the least overhead and is highly performant, especially when creating many small, short-lived instances.
  * Use this type only in very hot paths where you can be sure about stack space availability and carefully profile whether use of this variant is actually beneficial.
  * There is a global limit for the maximum size a `ValueStackWeaver` may grow to. See `# Configuration` for details.

# Configuration

The `static class StringWeaverConfiguration` exposes global configuration options for all `StringWeaver` variants. The options are usually strongly-typed and implemented in a thread-safe manner, however, altering options while the affected variants are in use will very likely lead to undefined behavior. I recommend very carefully thinking about what you're changing, why you're doing it and if it's really a good idea, then setting them on application startup and never touching the entire class again.

# Contribution

Opening issues and submitting PRs are welcome. All changes must be appropriately covered by tests.
Support for `netstandard2.0` must always be maintained. If possible, new functionality should be added to all target frameworks. New dependencies may be introduced after I vet the decision to do so.

Or get in touch on Discord `@eyeoftheenemy`