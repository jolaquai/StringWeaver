`StringWeaver` is designed to be as close to a drop-in replacement for any place where your first instict would be use a `StringBuilder` _and_ expand primarily on in-place replacement capabilities:
```csharp
class StringBuilder
{
    public StringBuilder Replace(string oldValue, string? newValue);
    public StringBuilder Replace(string oldValue, string? newValue, int startIndex, int count);
    public StringBuilder Replace(char oldChar, char newChar);
    public StringBuilder Replace(char oldChar, char newChar, int startIndex, int count);
}
```

Compare this to `StringWeaver`'s replacement capabilities:
```csharp
class StringWeaver
{
    public void Replace(char from, char to);                                                          // ^
    public void Replace(int index, int length, ReadOnlySpan<char> to);                                // 
    public void Replace(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction);  // 
    public void Replace(PcreRegex regex, ReadOnlySpan<char> to);                                      // 
    public void Replace(Range range, ReadOnlySpan<char> to);                                          // 
    public void Replace(ReadOnlySpan<char> from, ReadOnlySpan<char> to);                              // 
    public void Replace(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction);      // 
    public void Replace(Regex regex, ReadOnlySpan<char> to);                                          // 
    public void ReplaceAll(char from, char to);                                                       // 
    public void ReplaceAll(PcreRegex regex, int bufferSize, StringWeaverWriter ;                      // 
    public void ReplaceAll(PcreRegex regex, ReadOnlySpan<char> to);                                   // 
    public void ReplaceAll(ReadOnlySpan<char> from, ReadOnlySpan<char> to);                           // 
    public void ReplaceAll(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction);   // 
    public void ReplaceAll(Regex regex, ReadOnlySpan<char> to);                                       // 
    public void ReplaceAllExact(PcreRegex regex, int length, StringWeaverWriter ;                     // 
    public void ReplaceAllExact(Regex regex, int length, StringWeaverWriter writeReplacementAction);  // 
    public void ReplaceExact(PcreRegex regex, int length, StringWeaverWriter writeReplacementAction); // 
    public void ReplaceExact(Regex regex, int length, StringWeaverWriter writeReplacementAction);     // 
}
```
Impressive, right? And still, that is not all. Several quality-of-life shorthand methods such as for trimming characters or sequences off either end of an instance's buffer 