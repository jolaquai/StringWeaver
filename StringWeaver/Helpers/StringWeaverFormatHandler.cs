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
public readonly struct StringWeaverFormatHandler
{
    private readonly StringWeaver _weaver;
    private readonly IFormatProvider _formatProvider;

    /// <summary>
    /// Unconditionally throws an <see cref="InvalidOperationException"/>. This constructor is not intended to be used directly.
    /// </summary>
    public StringWeaverFormatHandler() => throw new InvalidOperationException("This type should not be used without proper initialization. For parameters of this type, simply pass an interpolated string literal.");
    internal StringWeaverFormatHandler(int literalLength, int formattedCount, StringWeaver weaver, IFormatProvider formatProvider = null)
    {
        _weaver = weaver;
        _formatProvider = formatProvider;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void AppendLiteral(string str) => _weaver.Append(str);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void AppendFormatted(scoped ReadOnlySpan<char> value) => _weaver.Append(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void AppendFormatted<T>(T value) => AppendFormatted(value, null);
    public readonly void AppendFormatted<T>(T value, string format)
    {
        switch (value)
        {
            case ISpanFormattable spanFormattable:
                _weaver.Append(spanFormattable, format, _formatProvider);
                break;
            case IFormattable formattable:
                _weaver.Append(formattable.ToString(format, _formatProvider));
                break;
            default:
                _weaver.Append(value?.ToString());
                break;
        }
    }
}
#endif