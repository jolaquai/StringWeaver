namespace StringWeaver.IO;

#if !NETSTANDARD2_0

#pragma warning disable CA1305 // Specify IFormatProvider

internal sealed class WeaverTextWriter : TextWriter
{
    private static readonly char[] _platformNewLine = Environment.NewLine.ToCharArray();

    private readonly StringWeaver _weaver;

    public WeaverTextWriter(StringWeaver weaver)
    {
        _weaver = weaver;

        CoreNewLine = _platformNewLine;
    }

    /// <summary>
    /// Returns <see cref="Encoding.Unicode"/> since <see langword="char"/>s are written to the underlying <see cref="StringWeaver"/> directly.
    /// </summary>
    public override Encoding Encoding { get; } = Encoding.Unicode;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Flush() { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Task FlushAsync() => Task.CompletedTask;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(bool value) => _weaver.Append(value ? "true" : "false");
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(char value) => _weaver.Append(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(char[] buffer) => _weaver.Append(buffer);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(char[] buffer, int index, int count) => _weaver.Append(buffer, index, count);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(decimal value) => _weaver.Append(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(double value) => _weaver.Append(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(int value) => _weaver.Append(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(long value) => _weaver.Append(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(object value) => _weaver.Append(value?.ToString());
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(ReadOnlySpan<char> buffer) => _weaver.Append(buffer);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(float value) => _weaver.Append(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(string value) => _weaver.Append(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(StringBuilder value)
    {
        foreach (var chunk in value.GetChunks())
        {
            _weaver.Append(chunk.Span);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(uint value) => _weaver.Append(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(ulong value) => _weaver.Append(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine() => _weaver.Append(CoreNewLine);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(bool value)
    {
        Write(value);
        WriteLine();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(char value)
    {
        Write(value);
        WriteLine();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(char[] buffer)
    {
        Write(buffer);
        WriteLine();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(char[] buffer, int index, int count)
    {
        Write(buffer, index, count);
        WriteLine();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(decimal value)
    {
        Write(value);
        WriteLine();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(double value)
    {
        Write(value);
        WriteLine();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(int value)
    {
        Write(value);
        WriteLine();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(long value)
    {
        Write(value);
        WriteLine();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(object value)
    {
        Write(value);
        WriteLine();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(ReadOnlySpan<char> buffer)
    {
        Write(buffer);
        WriteLine();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(float value)
    {
        Write(value);
        WriteLine();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(string value)
    {
        Write(value);
        WriteLine();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(StringBuilder value)
    {
        Write(value);
        WriteLine();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(uint value)
    {
        Write(value);
        WriteLine();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteLine(ulong value)
    {
        Write(value);
        WriteLine();
    }
}
#endif