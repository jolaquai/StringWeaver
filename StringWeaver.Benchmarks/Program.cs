using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace StringWeaver.Benchmarks;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            args = ["--filter", "*"];
        }

        var switcher = BenchmarkSwitcher.FromTypes([
            // typeof(ReplaceChainBenchmark),
            typeof(ManyOccurrencesBenchmarks),
        ]);
        // switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);

        switcher.Run(args,
#if DEBUG
            new DebugInProcessConfig()
#else
            DefaultConfig.Instance
#endif
                .HideColumns([Column.Error, Column.StdDev, Column.RatioSD])
        );
    }
}