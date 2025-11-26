using SW = StringWeaver.StringWeaver;

namespace StringWeaver.Tests;

public class StringWeaverTests
{
    [Fact]
    public void Test()
    {
        var sw = new SW("bbaaaa");
        sw.ReplaceAll(new Regex("a"), "cc", 0, 4);
        var result = sw.ToString();
    }
}