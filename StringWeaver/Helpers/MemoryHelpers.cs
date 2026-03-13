namespace StringWeaver.Helpers;

internal static class MemoryHelpers
{
#if NETSTANDARD2_0
    public static void Sort<T>(this Span<T> span)
    {
        var buf = ArrayPool<T>.Shared.Rent(span.Length);
        try
        {
            span.CopyTo(buf);
            Array.Sort(buf, 0, span.Length);
            new Span<T>(buf, 0, span.Length).CopyTo(span);
        }
        finally
        {
            ArrayPool<T>.Shared.Return(buf);
        }
    }
#endif
}
