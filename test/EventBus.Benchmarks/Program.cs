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
        BenchmarkRunner.Run<EventBusBenchmarks>();
    }
}

[MemoryDiagnoser]
[ThreadingDiagnoser] // multi-thread için
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

    // 🔹 Single request latency
    [Benchmark]
    public async Task SendAsync_Single()
    {
        var request = new ExampleEventRequest { Num = 123 };
        await _eventBus.SendAsync<ExampleEventResponse>(request);
    }

    // 🔹 Publish latency
    [Benchmark]
    public async Task PublishAsync_Single()
    {
        var request = new PublishEventRequest { Data = "benchmark" };
        await _eventBus.PublishAsync(request);
    }

    // 🔹 High load / parallel usage
    [Benchmark]
    public async Task SendAsync_Parallel_100()
    {
        var tasks = Enumerable.Range(0, 100)
            .Select(_ =>
                _eventBus.SendAsync<ExampleEventResponse>(
                    new ExampleEventRequest { Num = 123 }
                ));

        await Task.WhenAll(tasks);
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
    public async Task SendAsync_ColdStart()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddEventDispatcher()
                .AddEventLogging()
                .AddEventTimeout();
        services.AddEventBus(typeof(EventBusBenchmarks).Assembly);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.SendAsync<ExampleEventResponse>(
            new ExampleEventRequest { Num = 123 });
    }

    [Benchmark]
    public async Task PublishAsync_MultiHandler()
    {
        var request = new MultiHandlerEventRequest { Message = "load-test" };
        await _eventBus.PublishAsync(request);
    }
}
