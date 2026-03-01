`StringWeaver` is designed to be as close to a drop-in replacement as possible for any situation where your first instinct would be to use a `StringBuilder`.

Some feature highlights include:
* Highly efficient memory usage through the use of slicing instead of memory copies where possible.
* A significantly expanded set of methods for in-place replacement, often with zero additional allocations.
* Extensive API support through custom-built `TextWriter`, `Stream` and `IBufferWriter<char>` implementations.

Consider the `Replace` set of methods in `StringBuilder`:
```csharp
class StringBuilder
{
    public StringBuilder Replace(string oldValue, string? newValue);
    public StringBuilder Replace(string oldValue, string? newValue, int startIndex, int count);
    public StringBuilder Replace(char oldChar, char newChar);
    public StringBuilder Replace(char oldChar, char newChar, int startIndex, int count);
}
```

Compare this to `StringWeaver`'s replacement capabilities, where:

* each method shown also has two additional overloads to allow specifying the `int index` at which to begin searching and an `int length` to define a range within which to search,
* I've marked the APIs (plus additional overloads as mentioned) that are effectively enough to replace all of `StringBuilder`'s replacement methods with a caret (`^`)

```csharp
class StringWeaver
{
    public void Replace(char from, char to)                                                                                   // 
    public void ReplaceAll(char from, char to)                                                                                // ^
    public void Replace(ReadOnlySpan<char> from, ReadOnlySpan<char> to)                                                       // 
    public void ReplaceAll(ReadOnlySpan<char> from, ReadOnlySpan<char> to)                                                    // ^
    public void Replace(System.Range range, ReadOnlySpan<char> to)                                                            // 
    public void Replace(int index, int length, ReadOnlySpan<char> to)                                                         // 
    public void Replace(PCRE.PcreRegex regex, ReadOnlySpan<char> to)                                                          // 
    public void ReplaceAll(PCRE.PcreRegex regex, ReadOnlySpan<char> to)                                                       // 
    public void Replace(PCRE.PcreRegex regex, int bufferSize, StringWeaver.StringWeaverWriter writeReplacementAction)         // 
    public void ReplaceAll(PCRE.PcreRegex regex, int bufferSize, StringWeaver.StringWeaverWriter writeReplacementAction)      // 
    public void ReplaceExact(PCRE.PcreRegex regex, int bufferSize, StringWeaver.StringWeaverWriter writeReplacementAction)    // 
    public void ReplaceAllExact(PCRE.PcreRegex regex, int bufferSize, StringWeaver.StringWeaverWriter writeReplacementAction) // 
    public void Replace(Regex regex, ReadOnlySpan<char> to)                                                                   // 
    public void ReplaceAll(Regex regex, ReadOnlySpan<char> to)                                                                // 
    public void Replace(Regex regex, int bufferSize, StringWeaver.StringWeaverWriter writeReplacementAction)                  // 
    public void ReplaceAll(Regex regex, int bufferSize, StringWeaver.StringWeaverWriter writeReplacementAction)               // 
    public void ReplaceExact(Regex regex, int bufferSize, StringWeaver.StringWeaverWriter writeReplacementAction)             // 
    public void ReplaceAllExact(Regex regex, int bufferSize, StringWeaver.StringWeaverWriter writeReplacementAction)          // 
}
```

Impressive, right? And still, that is far from all; several quality-of-life shorthand methods such as for trimming characters or sequences off either end of an instance's buffer are also provided, not to mention the borderline insane number of `IndexOf` (and similar) overloads for efficient searching.

# Examples

Here are some common usage examples demonstrating how you'd use `StringWeaver` instead of `StringBuilder` or `StringWriter` for various tasks.

The following alias definitions can be used to make referring to `StringWeaver.StringWeaver` easier:
* Put this at the top of a file: `using SW = StringWeaver.StringWeaver;`
* Or put this into any file: `global using SW = StringWeaver.StringWeaver;`
* Or put this into your `csproj`:
```xml
<ItemGroup>
  <Using Include="StringWeaver.StringWeaver" Alias="SW" />
</ItemGroup>
```

## When to use StringWeaver

`StringWeaver` shines wherever you'd normally reach for `StringBuilder`, `StringWriter`, or repeated `string.Concat`/interpolation in a loop. Some concrete scenarios:

* **Building large text output** — log formatters, code generators, template engines, report builders.
* **In-place text manipulation** — trimming, replacing, removing substrings without allocating intermediate strings.
* **Interop with `TextWriter`/`Stream`/`IBufferWriter<char>` APIs** — pass a single `StringWeaver` to APIs that expect any of these abstractions.
* **High-throughput hot paths** — `StringWeaver` avoids the copy-on-grow penalty that `StringBuilder` pays and supports direct `Span<char>` access for zero-copy reads.

## Replacing typical bad code patterns

### String concatenation in a loop

```csharp
// BAD — O(n²) allocations
string result = "";
foreach (var item in items)
    result += item.ToString() + ", ";

// GOOD — single buffer, no intermediate strings
var sw = new SW();
foreach (var item in items)
{
    sw.Append(item);
    sw.Append(", ");
}
sw.Trim(2); // remove trailing ", "
var result = sw.ToString();
```

### Repeated `string.Replace` chains

```csharp
// BAD — each Replace allocates a new string
var sanitized = input
    .Replace("&", "&amp;")
    .Replace("<", "&lt;")
    .Replace(">", "&gt;");

// GOOD — all mutations happen in-place on one buffer
var sw = new SW(input);
sw.ReplaceAll("&", "&amp;");
sw.ReplaceAll("<", "&lt;");
sw.ReplaceAll(">", "&gt;");
var sanitized = sw.ToString();
```

### Building CSV/TSV rows

```csharp
var sw = new SW();
foreach (var row in dataRows)
{
    sw.Append(row.Name);
    sw.Append('\t');
    sw.Append(row.Value);
    sw.AppendLine();
}
File.WriteAllText("output.tsv", sw.ToString());
```

## Basic Appends

```csharp
var sw = new SW();

// Strings, chars, repeated chars
sw.Append("Hello");
sw.Append(' ');
sw.Append('!', 3);            // "Hello !!!"

// Spans (zero-copy)
ReadOnlySpan<char> span = " World".AsSpan();
sw.Append(span);               // "Hello !!! World"

// ISpanFormattable (NET6+) — formats directly into the buffer
sw.Append(42);                 // appends "42"
sw.Append(3.14);               // appends "3.14"
sw.Append(DateTime.Now);       // appends current date/time

// Interpolated string handler — no boxing, no intermediate strings
var name = "StringWeaver";
sw.Append($"Welcome to {name}!");

// AppendLine variants mirror every Append overload
sw.AppendLine();               // newline only
sw.AppendLine("Done.");        // "Done.\r\n" (or "\n" on Unix)
```

## IndexOf/IndicesOf

```csharp
var sw = new SW("Hello World, Hello Earth");

// First occurrence
int idx = sw.IndexOf('W');          // 6
int idx2 = sw.IndexOf("Hello");    // 0

// Scoped search: start at index 7, search 10 chars
int idx3 = sw.IndexOf("Hello", 7, 17); // 13

// Enumerate ALL indices of a char (zero-alloc with ref struct enumerator)
foreach (var i in sw.EnumerateIndicesOf('l'))
{
    // yields: 2, 3, 16, 17
}

// Enumerate indices of a substring
foreach (var i in sw.EnumerateIndicesOf("Hello".AsSpan()))
{
    // yields: 0, 13
}

// IndexOfAny / IndexOfAnyExcept (NET8+ supports SearchValues<char>)
int first = sw.IndexOfAny("aeiou".AsSpan()); // first vowel
```

## Replace(All)

```csharp
var sw = new SW("aabbaabb");

// Replace first occurrence only
sw.Replace("aa", "X");         // "Xbbaabb"

// Replace ALL occurrences
sw = new SW("aabbaabb");
sw.ReplaceAll("aa", "X");     // "XbbXbb"

// Char-to-char replacement (extremely fast, no reallocation)
sw = new SW("Hello World");
sw.ReplaceAll('o', '0');      // "Hell0 W0rld"

// Replace by range
sw = new SW("Hello World");
sw.Replace(..5, "Hi".AsSpan());    // "Hi World"

// Replace by index + length
sw = new SW("Hello World");
sw.Replace(6, 5, "Earth".AsSpan()); // "Hello Earth"

// Scoped replacement: only within a substring
sw = new SW("aaXXaa");
sw.ReplaceAll("aa", "BB", 2, 4);   // first "aa" untouched: "aaBBaa" — wait, only the region [2..6) = "XXaa" is searched
```

## PcreRegex

```csharp
// PCRE2-powered regex (via PCRE.NET) — available on all targets
var sw = new SW("Order #123, Order #456");

// Replace first match
sw.Replace(new PcreRegex(@"#\d+"), "#000");
// "Order #000, Order #456"

// Replace all matches
sw = new SW("Order #123, Order #456");
sw.ReplaceAll(new PcreRegex(@"#\d+"), "#000");
// "Order #000, Order #000"

// Writer-based replacement for dynamic output
sw = new SW("hello world");
sw.ReplaceAll(new PcreRegex(@"\b\w"), bufferSize: 1, static (Span<char> buf, ReadOnlySpan<char> match) =>
{
    buf[0] = char.ToUpper(match[0]);
});
// "Hello World"
```

## Regex

```csharp
// System.Text.RegularExpressions.Regex — available on NET7+
var sw = new SW("2024-01-15 and 2024-12-25");

sw.ReplaceAll(new Regex(@"\d{4}-\d{2}-\d{2}"), "DATE");
// "DATE and DATE"
```

## Remove

```csharp
var sw = new SW("Hello World");

// Remove by range
sw.Remove(5..);               // "Hello"

// Remove by index + length
sw = new SW("Hello World");
sw.Remove(5, 6);              // "Hello"

// Remove all occurrences of a char within a range
sw = new SW("Hello World");
sw.Remove('l', ..);           // "Heo Word"
```

## Trim

```csharp
var sw = new SW("   Hello World   ");

// Trim single char from both ends
sw.Trim(' ');                  // "Hello World"

// TrimStart / TrimEnd
sw = new SW("---Hello---");
sw.TrimStart('-');             // "Hello---"
sw.TrimEnd('-');               // "Hello"

// Trim any of several chars
sw = new SW(".-Hello-.");
sw.Trim(".-".AsSpan());       // "Hello"

// Trim an exact repeating sequence from both ends
sw = new SW("ababHelloabab");
sw.TrimSequence("ab");        // "Hello"

// Trim N chars from the end
sw = new SW("Hello World");
sw.Trim(6);                   // "Hello"
```

## Length mods

```csharp
var sw = new SW("Hello World");

// Truncate to exact length
sw.Truncate(5);                // "Hello"

// Expand after writing directly into the buffer
sw = new SW("Hello");
var writable = sw.GetWritableSpan(6);
" World".AsSpan().CopyTo(writable);
sw.Expand(6);                 // "Hello World"
```

## Clear

```csharp
var sw = new SW("Hello World");
sw.Clear();                    // Length == 0, buffer retained

// Clear + zero-fill the buffer (security-sensitive scenarios)
sw = new SW("SensitiveData");
sw.Clear(wipe: true);
```

## Copy

```csharp
var sw = new SW("Hello World");

// Copy to Span<char>
Span<char> dest = stackalloc char[sw.Length];
sw.CopyTo(dest);

// Copy to char[]
var arr = new char[sw.Length];
sw.CopyTo(arr, 0);

// Copy a block (substring) to a TextWriter
using var writer = new StreamWriter("out.txt");
sw.CopyBlock(0, 5, writer);   // writes "Hello"

// Copy to an IBufferWriter<char>
var bufWriter = new ArrayBufferWriter<char>();
sw.CopyTo(bufWriter);
```

## Grow

```csharp
var sw = new SW(capacity: 16);

// Pre-grow to avoid repeated reallocations
sw.EnsureCapacity(1024);

// GetWritableSpan/GetWritableMemory grow automatically
var span = sw.GetWritableSpan(minimumSize: 100);
// span.Length >= 100 guaranteed
```

## ToString

```csharp
var sw = new SW("Hello World");

// Standard ToString — allocates a new string
string s = sw.ToString();

// Drain — ToString + Clear in one call (reuse the weaver)
string drained = sw.Drain();
// sw.Length == 0 now, ready for reuse

// Direct Span access — zero-allocation reads
ReadOnlySpan<char> span = sw.Span;
```

## `TextWriter` wrapper

Pass a `StringWeaver` to any API that expects a `TextWriter` (NET6+):

```csharp
var sw = new SW();
var writer = sw.GetTextWriter();

// Any TextWriter consumer works transparently
writer.Write("Hello ");
writer.WriteLine("World");
writer.Write(42);

// Example: JsonSerializer writes to a TextWriter
// System.Text.Json.JsonSerializer.Serialize(writer, myObject);

string result = sw.ToString(); // "Hello World\r\n42"
```

## `Stream` wrapper

Use a `StringWeaver` as a byte sink — bytes are decoded into the buffer:

```csharp
var sw = new SW();
using var stream = sw.GetStream(Encoding.UTF8);

// Any API that writes to a Stream can now target StringWeaver
byte[] utf8 = Encoding.UTF8.GetBytes("Hello Stream");
stream.Write(utf8, 0, utf8.Length);

string result = sw.ToString(); // "Hello Stream"
```

## `IBufferWriter<char>` implementation

`StringWeaver` implements `IBufferWriter<char>` directly — no wrapper needed:

```csharp
var sw = new SW();
IBufferWriter<char> bufferWriter = sw;

// Get a writable region, write into it, then advance
var span = bufferWriter.GetSpan(5);
"Hello".AsSpan().CopyTo(span);
bufferWriter.Advance(5);

// Pass to any API that accepts IBufferWriter<char>
// e.g., Utf8JsonWriter with a transcoding layer, or custom serializers

string result = sw.ToString(); // "Hello"
```

