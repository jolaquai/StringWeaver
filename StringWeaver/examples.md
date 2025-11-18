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

}
```
Impressive, right? And still, that is not all. Several quality-of-life shorthand methods such as for trimming characters or sequences off either end of an instance's buffer 