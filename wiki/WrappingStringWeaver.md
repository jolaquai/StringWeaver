`WrappingStringWeaver` is effectively a convenience wrapper that turns any arbitrary memory block into the backing memory for a `StringWeaver` instance. It allows you to treat any memory you are able to get a mutable `char`-typed reference to (that is, there are constructors accepting `Span<char>`, `Memory<char>`, `char*` and `ref char`).

The resulting instance obviously does not support resizing and will always throw a `NotSupportedException` when an operation would result in (or rather, require) expanding the backing memory because the instance doesn't know *how* to do the resize (where to source the memory, where to update references etc.). If that's something you want to implement, derive from `StringWeaver` directly.

# Examples

Here's some examples of how to create `WrappingStringWeaver`s. There won't be any usage examples since the API surface is 100% inherited from `StringWeaver`.

```csharp
// WIP
```
