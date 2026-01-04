using System.Text;

namespace StringWeaver.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class ManyOccurrencesBenchmarks
{
    [Params(10, 100, 1000, 10000)]
    public int N;
    private string _input;

    [GlobalSetup]
    public void Setup() => _input = string.Join('.', Enumerable.Repeat("abc", N));
    [Benchmark(Baseline = true)]
    public string String_Replace() => _input.Replace("abc", "XXXX");
    [Benchmark]
    public string SW_ReplaceAll()
    {
        var sw = new StringWeaver(_input);
        sw.ReplaceAll("abc", "XXXX");
        return sw.ToString();
    }
    [Benchmark]
    public string SB_Replace() => new StringBuilder(_input).Replace("abc", "XXXX").ToString();
}