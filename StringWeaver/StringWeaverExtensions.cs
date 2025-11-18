namespace StringWeaver;

#if !NETSTANDARD2_0
/// <summary>
/// Contains <see langword="extension"/> declarations for <see cref="StringWeaver"/>.
/// </summary>
public static partial class StringWeaverExtensions
{
    extension(StringWeaver stringWeaver)
    {
#pragma warning disable IDE0060 // Remove unused parameter
        /// <summary>
        /// Appends a formatted <see langword="string"/> to the end of the buffer.
        /// </summary>
        /// <param name="stringWeaverFormatHandler">The handler used to format the <see langword="string"/>. You should not be specifying this manually; pass an interpolated <see langword="string"/> literal for this parameter instead.</param>
        public void Append([InterpolatedStringHandlerArgument(nameof(stringWeaver))] StringWeaverFormatHandler stringWeaverFormatHandler)
        {
        }
        /// <summary>
        /// Appends a formatted <see langword="string"/> to the end of the buffer where the arguments are formatted using the specified <see cref="IFormatProvider"/>.
        /// </summary>
        /// <param name="formatProvider">The <see cref="IFormatProvider"/> used to format the arguments.</param>
        /// <param name="stringWeaverFormatHandler">The handler used to format the <see langword="string"/>. You should not be specifying this manually; pass an interpolated <see langword="string"/> literal for this parameter instead.</param>
        public void Append(IFormatProvider formatProvider, [InterpolatedStringHandlerArgument(nameof(stringWeaver), nameof(formatProvider))] StringWeaverFormatHandler stringWeaverFormatHandler)
        {
        }
#pragma warning restore IDE0060 // Remove unused parameter
    }
}
#endif