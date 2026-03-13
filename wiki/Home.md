Welcome to the StringWeaver wiki!

`StringWeaver` is a high-performance mutable string builder with a contiguous buffer and a vastly expanded manipulation API compared to `StringBuilder`.

> **`StringWeaver` is _not_ a drop-in replacement for `StringBuilder`.**
> `StringBuilder` uses a linked list of `char[]` chunks — appending never copies existing data. `StringWeaver` stores content in a single contiguous `char[]` (or unmanaged buffer), which enables direct `Span<char>` access and in-place regex matching but means **growing copies the entire buffer**. Choose `StringWeaver` when you need to _manipulate_ buffer contents (trim, replace, regex-match, slice) without allocating intermediate strings. For purely append-heavy workloads with unpredictable size, `StringBuilder` may be more memory-efficient.

See the [Examples page](Examples.md) for usage guidance and code samples.