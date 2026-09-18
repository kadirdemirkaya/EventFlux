using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using EventBus.Benchmarks;

public class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
            return 0;
        }

        var summary = BenchmarkRunner.Run<DispatchOverheadBenchmarks>();
        return IsSuccessful(summary) ? 0 : 1;
    }

    private static bool IsSuccessful(Summary summary) =>
        !summary.HasCriticalValidationErrors
        && summary.Reports.Length > 0
        && summary.Reports.All(report => report.Success && report.ResultStatistics is not null);
}
