`StringWeaver` is designed to be as close to a drop-in replacement as possible for any situation where your first instict would be to use a `StringBuilder`.

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

[WIP]

## Basic Appends

```csharp
var sw = new SW();

```

## IndexOf/IndicesOf


## Replace(All)


## PcreRegex


## Regex


## Remove


## Trim


## Length mods


## Clear


## Copy


## Grow


## ToString


## `TextWriter` wrapper


## `Stream` wrapper


## `IBufferWriter<char>` implementation

