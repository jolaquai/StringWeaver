namespace StringWeaver.Helpers;

internal static class Pow2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextPowerOf2(int n)
    {
        n--;

        // Next would be 2^31 = negative
        if (n >= 0x40000000)
        {
            return 0;
        }

        n |= n >> 1;
        n |= n >> 2;
        n |= n >> 4;
        n |= n >> 8;
        n |= n >> 16;
        return n + 1;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NextPowerOf2(long n)
    {
        n--;

        // Next would be 2^63 = negative
        if (n >= 0x4000_0000_0000_0000)
        {
            return 0;
        }

        n |= n >> 1;
        n |= n >> 2;
        n |= n >> 4;
        n |= n >> 8;
        n |= n >> 16;
        n |= n >> 32;
        return n + 1;
    }
}