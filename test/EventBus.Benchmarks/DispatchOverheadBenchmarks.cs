using BenchmarkDotNet.Attributes;
using EventBus.Benchmarks.Events;
using EventFlux.Abstractions;
using EventFlux.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EventBus.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class DispatchOverheadBenchmarks
    {
        private ServiceProvider _provider = null!;
        private IEventBus _eventBus = null!;
        private IEventDispatcher _eventDispatcher = null!;

        [GlobalSetup]
        public void Setup()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventDispatcher();
            services.AddEventBus(typeof(DispatchOverheadBenchmarks).Assembly);

            _provider = services.BuildServiceProvider();
            _eventBus = _provider.GetRequiredService<IEventBus>();
            _eventDispatcher = _provider.GetRequiredService<IEventDispatcher>();
        }

        [GlobalCleanup]
        public void Cleanup() => _provider.Dispose();

        [Benchmark(Baseline = true)]
        public Task<OverheadResponse?> Bus_Send() =>
            _eventBus.SendAsync<OverheadResponse>(new OverheadSendRequest { Num = 1 });

        [Benchmark]
        public Task Bus_Publish_OneHandler() =>
            _eventBus.PublishAsync(new OverheadPublishRequest());

        [Benchmark]
        public Task Bus_Publish_ThreeHandlers() =>
            _eventBus.PublishAsync(new OverheadFanOutRequest());

        [Benchmark]
        public Task<OverheadResponse> Dispatcher_Send() =>
            _eventDispatcher.SendAsync<OverheadResponse>(new OverheadSendRequest { Num = 1 });

        [Benchmark]
        public Task Dispatcher_Publish_OneHandler() =>
            _eventDispatcher.PublishAsync(new OverheadPublishRequest());

        [Benchmark]
        public Task Dispatcher_Publish_ThreeHandlers() =>
            _eventDispatcher.PublishAsync(new OverheadFanOutRequest());
    }
}
