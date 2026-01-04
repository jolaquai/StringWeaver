using System.Diagnostics;

using Microsoft.Diagnostics.NETCore.Client;

namespace StringWeaver.Benchmarks;

[MemoryDiagnoser]
[HideColumns(Column.Error, Column.StdDev, Column.RatioSD)]
public class CompareBaseVariants
{
    /// <summary>
    /// For short sample text like this, repeated replacements with string.Replace will very likely end up being cheaper than using StringWeaver, but they *will* still pollute your memory with string allocations.
    /// </summary>
    public string SampleTextShort;
    public string SampleTextLong;
    public string SampleTextVeryLong;
    public string[] Args;

    [GlobalSetup]
    public void Setup()
    {
        SampleTextShort = "The quick brown fox jumps over the lazy dog.";
        SampleTextLong = string.Concat(Enumerable.Repeat(SampleTextShort, 1000));
        SampleTextVeryLong = string.Concat(Enumerable.Repeat(SampleTextShort, 10000));
        Args = [SampleTextShort, SampleTextLong, SampleTextVeryLong];
    }

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

    [Benchmark]
    public void UnsafeStringWeaverReplaceAllCalls()
    {
        using (var sw = new UnsafeStringWeaver(Args[I]))
        {
            sw.ReplaceAll("quick", "swift");
            sw.ReplaceAll("brown", "dark");
            sw.ReplaceAll("fox", "wolf");
            sw.ReplaceAll("jumps", "leaps");
            sw.ReplaceAll("lazy", "sleepy");

            var result = sw.ToString();
        }
    }
}