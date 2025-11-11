using StringWeaver.Helpers;

namespace StringWeaver.Tests.Helpers;

public class Pow2Tests
{
    private const int Int32Bits = sizeof(int) * 8;
    private const int Int64Bits = sizeof(long) * 8;

    public static TheoryData<int, int> Int32TheoryDataFactory()
    {
        var td = new TheoryData<int, int>();
        for (var i = 0; i < Int32Bits - 1; i++)
        {
            var val = (1 << i) + 1;
            var exp = 1 << (i + 1);
            if (exp < 0)
            {
                exp = 0;
            }
            td.Add(val, exp);
        }
        return td;
    }
    public static TheoryData<long, long> Int64TheoryDataFactory()
    {
        var td = new TheoryData<long, long>();
        for (var i = 0; i < Int64Bits - 1; i++)
        {
            var val = (1L << i) + 1;
            var exp = 1L << (i + 1);
            if (exp < 0)
            {
                exp = 0;
            }
            td.Add(val, exp);
        }
        return td;
    }

    [Theory]
    [MemberData(nameof(Int32TheoryDataFactory))]
    public void Number_Yields_NextHigherPow2(int n, int nextPow2)
    {
        var result = Pow2.NextPowerOf2(n);
        Assert.Equal(nextPow2, result);
    }

    [Theory]
    [MemberData(nameof(Int64TheoryDataFactory))]
    public void Number_Yields_NextHigherPow2Int64(long n, long nextPow2)
    {
        var result = Pow2.NextPowerOf2(n);
        Assert.Equal(nextPow2, result);
    }
}
