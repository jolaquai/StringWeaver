namespace StringWeaver.Benchmarks;

[MemoryDiagnoser]
[HideColumns(Column.Error, Column.StdDev, Column.RatioSD)]
public class ReplaceBadCodePatterns
{
    /// <summary>
    /// For short sample text like this, repeated replacements with string.Replace will very likely end up being cheaper than using StringWeaver, but they *will* still pollute your memory with string allocations.
    /// </summary>
    public const string SampleTextShort = "The quick brown fox jumps over the lazy dog.";
    public static readonly string SampleTextLong = string.Concat(Enumerable.Repeat(SampleTextShort, 1000));
    public static readonly string SampleTextVeryLong = string.Concat(Enumerable.Repeat(SampleTextShort, 10000));

    public static readonly string[] Args = [SampleTextShort, SampleTextLong, SampleTextVeryLong];

    [Params(0, 1, 2)]
    public int I;

    [Benchmark(Baseline = true)]
    public void ChainedStringReplaceCalls()
    {
        var start = Args[I];

        start = start.Replace("quick", "swift");
        start = start.Replace("brown", "dark");
        start = start.Replace("fox", "wolf");
        start = start.Replace("jumps", "leaps");
        start = start.Replace("lazy", "sleepy");

        GC.KeepAlive(start);
    }

    [Benchmark]
    public void StringWeaverReplaceAllCalls()
    {
        var sw = new SW(Args[I]);

        sw.ReplaceAll("quick", "swift");
        sw.ReplaceAll("brown", "dark");
        sw.ReplaceAll("fox", "wolf");
        sw.ReplaceAll("jumps", "leaps");
        sw.ReplaceAll("lazy", "sleepy");

        var result = sw.ToString();
    }
}