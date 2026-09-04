using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using EventBus.Benchmarks.Events;
using EventFlux.Abstractions;
using EventFlux.Extensions;
using Microsoft.Extensions.DependencyInjection;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class EventBusBenchmarks
{
    private IEventBus _eventBus;

    [Params(1, 10, 100, 500)]
    public int ParallelCount;

    [GlobalSetup]
    public void Setup()
    {

        var services = new ServiceCollection();

        services.AddLogging();

        services.AddEventDispatcher()
                .AddEventLogging()
                .AddEventTimeout();

        services.AddEventBus(typeof(Program).Assembly);

        var provider = services.BuildServiceProvider();
        _eventBus = provider.GetRequiredService<IEventBus>();
    }

    [Benchmark]
    public async Task SendAsync_Parallel()
    {
        var tasks = Enumerable.Range(0, ParallelCount)
            .Select(_ =>
                _eventBus.SendAsync<ExampleEventResponse>(
                    new ExampleEventRequest { Num = 123 }
                ));

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task PublishAsync_MultiHandler()
    {
        var request = new MultiHandlerEventRequest { Message = "load-test" };
        await _eventBus.PublishAsync(request);
    }

    [Benchmark]
    public async Task PublishAsync_Parallel()
    {
        var tasks = Enumerable.Range(0, ParallelCount)
            .Select(_ =>
                _eventBus.PublishAsync(
                    new PublishEventRequest
                    {
                        Data = "parallel-benchmark"
                    }
                ));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
