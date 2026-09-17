using BenchmarkDotNet.Running;
using EventBus.Benchmarks;

public class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            var summary = BenchmarkRunner.Run<MediatRComparisonBenchmarks>();
            return summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success) ? 1 : 0;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }
}
