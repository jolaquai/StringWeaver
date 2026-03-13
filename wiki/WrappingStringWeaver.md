`WrappingStringWeaver` is effectively a convenience wrapper that turns any arbitrary memory block into the backing memory for a `StringWeaver` instance. It allows you to treat any memory you are able to get a mutable `char`-typed reference to (that is, there are constructors accepting `Span<char>`, `Memory<char>`, `char[]`, `char*` and `ref char`).

The resulting instance does not support resizing and will always throw a `NotSupportedException` when an operation would require expanding the backing memory because the instance doesn't know *how* to do the resize (where to source the memory, where to update references etc.). If that's something you want to implement, derive from `StringWeaver` directly.

`WrappingStringWeaver` implements `IDisposable`. If you used one of the overloads that pin memory (`pin: true`, available for `Memory<char>` and `char[]` constructors), not disposing is a memory leak. For `Span<char>` and pointer constructors, it is the caller's responsibility to ensure the memory remains valid for the lifetime of the instance.

# Examples

Here are some examples of how to create `WrappingStringWeaver`s. There won't be any usage examples since the API surface is 100% inherited from `StringWeaver`.

```csharp
using StringWeaver.Specialized;

// From a char array (usedLength = how many chars are already "written")
var array = "Hello World!!!".ToCharArray();
using var w1 = new WrappingStringWeaver(array, usedLength: 11);
// w1.ToString() == "Hello World"
// w1.Capacity == 14 (full array length)

// From a char array with pinning (prevents GC from moving the array)
using var w2 = new WrappingStringWeaver(array, usedLength: 11, pin: true);

// From a char array with index and length (wrap a sub-region)
using var w3 = new WrappingStringWeaver(array, index: 6, length: 5, usedLength: 5);
// w3.ToString() == "World"

// From a Span<char> (caller must keep memory alive)
Span<char> span = stackalloc char[64];
"Hello".AsSpan().CopyTo(span);
var w4 = new WrappingStringWeaver(span, usedLength: 5);
// w4.ToString() == "Hello"
// w4 can Append up to 64 chars total, but cannot grow beyond that

// From Memory<char>
var memory = new char[128].AsMemory();
using var w5 = new WrappingStringWeaver(memory, usedLength: 0);
w5.Append("Built in-place");

// From a pointer (unsafe)
unsafe
{
    fixed (char* ptr = array)
    {
        var w6 = new WrappingStringWeaver(ptr, length: array.Length, usedLength: 11);
        // w6.ToString() == "Hello World"
    }
}

// From a ref char (unsafe, reinterpret any memory as char buffer)
unsafe
{
    // array.Length must be >= 11, otherwise you're dereferencing memory you don't own!
    ref char r = ref array[0];
    var w7 = new WrappingStringWeaver(ref r, length: array.Length, usedLength: 11);
}
```

Operations that would exceed the buffer capacity throw `NotSupportedException`:

```csharp
var buf = new char[8];
using var w = new WrappingStringWeaver(buf, usedLength: 0);
w.Append("Hello");    // OK (5 <= 8)
w.Append(" World!");  // NotSupportedException — would need 12 chars, buffer is 8
```
