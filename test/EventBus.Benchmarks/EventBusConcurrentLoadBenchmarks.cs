using BenchmarkDotNet.Attributes;
using EventBus.Benchmarks.Events;
using EventFlux.Abstractions;
using EventFlux.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EventBus.Benchmarks
{
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    public class EventBusConcurrentLoadBenchmarks
    {
        private ServiceProvider _provider = null!;
        private IEventBus _eventBus = null!;

        [Params(1, 10, 100, 500)]
        public int ParallelCount;

        [GlobalSetup]
        public void Setup()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(EventBusConcurrentLoadBenchmarks).Assembly);

            _provider = services.BuildServiceProvider();
            _eventBus = _provider.GetRequiredService<IEventBus>();
        }

        [GlobalCleanup]
        public void Cleanup() => _provider.Dispose();

        [Benchmark]
        public Task SendAsync_ConcurrentCallers()
        {
            var tasks = new Task[ParallelCount];

            for (var i = 0; i < ParallelCount; i++)
            {
                tasks[i] = _eventBus.SendAsync<ExampleEventResponse>(new ExampleEventRequest { Num = 123 });
            }

            return Task.WhenAll(tasks);
        }

        [Benchmark]
        public Task PublishAsync_ConcurrentCallers()
        {
            var tasks = new Task[ParallelCount];

            for (var i = 0; i < ParallelCount; i++)
            {
                tasks[i] = _eventBus.PublishAsync(new PublishEventRequest { Data = "parallel-benchmark" });
            }

            return Task.WhenAll(tasks);
        }

        [Benchmark]
        public Task PublishAsync_MultiHandler_SingleCaller() =>
            _eventBus.PublishAsync(new MultiHandlerEventRequest { Message = "load-test" });
    }
}
