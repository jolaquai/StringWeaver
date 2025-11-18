namespace StringWeaver.Helpers;

#if !NETSTANDARD2_0

#pragma warning disable CA1815 // Override equals and operator equals on value types
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS9113 // Parameter is unread.
#pragma warning disable IDE0251 // Make member 'readonly'

/// <summary>
/// Supports <see cref="StringWeaver"/>, allowing interpolated <see langword="string"/> literals to be efficiently formatted into a <see cref="StringWeaver"/> instance.
/// </summary>
/// <remarks>
/// This type is not intended to be used directly from client code.
/// </remarks>
[InterpolatedStringHandler]
public struct StringWeaverFormatHandler(int literalLength, int formattedCount, StringWeaver weaver, IFormatProvider formatProvider = null)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLiteral(string str) => weaver.Append(str);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(scoped ReadOnlySpan<char> value) => weaver.Append(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted<T>(T value) => AppendFormatted(value, null);
    public void AppendFormatted<T>(T value, string format)
    {
        switch (value)
        {
            case ISpanFormattable spanFormattable:
                weaver.Append(spanFormattable, format, formatProvider);
                break;
            case IFormattable formattable:
                weaver.Append(formattable.ToString(format, formatProvider));
                break;
            default:
                weaver.Append(value?.ToString());
                break;
        }
    }
}
#endif