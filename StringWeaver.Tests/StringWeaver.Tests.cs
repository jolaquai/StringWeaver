using SW = StringWeaver.StringWeaver;

namespace StringWeaver.Tests;

public class StringWeaverTests
{
    [Theory]
    [InlineData("B")]
    [InlineData("CC")]
    [InlineData("DDD")]
    public void ReplaceAll_LiteralOrRegex_ProduceSameResult(string replacement)
    {
        const string InitialContent = "bbAAAA";

        var swLiteral = new SW(InitialContent);
        var swRegex = new SW(InitialContent);
        var swPcre = new SW(InitialContent);

        swLiteral.ReplaceAll("AA", replacement, 0, 5);
        swRegex.ReplaceAll(new Regex("AA"), replacement, 0, 5);
        swPcre.ReplaceAll(new PcreRegex("AA"), replacement, 0, 5);

        var expected = $"bb{replacement}AA";
        Assert.Equal(expected, swLiteral.Span);
        Assert.Equal(expected, swRegex.Span);
        Assert.Equal(expected, swPcre.Span);
    }
}