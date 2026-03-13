#if NET6_0_OR_GREATER
using System.Numerics;
#endif

namespace StringWeaver.Helpers;

internal static class Pow2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextPowerOf2(int n)
    {
        // Next would be 2^31 = negative
        if (n > 0x40000000)
        {
            return 0;
        }

#if NET6_0_OR_GREATER
        return (int)BitOperations.RoundUpToPowerOf2((uint)n);
#else
        n--;
        n |= n >> 1;
        n |= n >> 2;
        n |= n >> 4;
        n |= n >> 8;
        n |= n >> 16;
        return n + 1;
#endif
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NextPowerOf2(long n)
    {
        // Next would be 2^63 = negative
        if (n > 0x4000_0000_0000_0000)
        {
            return 0;
        }

#if NET6_0_OR_GREATER
        return (long)BitOperations.RoundUpToPowerOf2((ulong)n);
#else
        n--;
        n |= n >> 1;
        n |= n >> 2;
        n |= n >> 4;
        n |= n >> 8;
        n |= n >> 16;
        n |= n >> 32;
        return n + 1;
#endif
    }
}
