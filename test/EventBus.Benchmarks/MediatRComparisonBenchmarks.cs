using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using EventBus.Benchmarks.Events;
using EventFlux.Abstractions;
using EventFlux.Extensions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace EventBus.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class MediatRComparisonBenchmarks
    {
        private ServiceProvider _provider = null!;
        private IEventBus _eventBus = null!;
        private IEventDispatcher _eventDispatcher = null!;
        private IMediator _mediator = null!;

        [GlobalSetup]
        public void Setup()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventDispatcher();
            services.AddEventBus(typeof(MediatRComparisonBenchmarks).Assembly);
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRComparisonBenchmarks>());

            _provider = services.BuildServiceProvider();
            _eventBus = _provider.GetRequiredService<IEventBus>();
            _eventDispatcher = _provider.GetRequiredService<IEventDispatcher>();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        [GlobalCleanup]
        public void Cleanup() => _provider.Dispose();

        [BenchmarkCategory("Send"), Benchmark(Baseline = true)]
        public Task<MediatRSendResponse> MediatR_Send() =>
            _mediator.Send(new MediatRSendRequest { Num = 1 });

        [BenchmarkCategory("Send"), Benchmark]
        public Task<OverheadResponse?> EventFlux_Bus_Send() =>
            _eventBus.SendAsync<OverheadResponse>(new OverheadSendRequest { Num = 1 });

        [BenchmarkCategory("Send"), Benchmark]
        public Task<OverheadResponse> EventFlux_Dispatcher_Send() =>
            _eventDispatcher.SendAsync<OverheadResponse>(new OverheadSendRequest { Num = 1 });

        [BenchmarkCategory("Publish1"), Benchmark(Baseline = true)]
        public Task MediatR_Publish_OneHandler() =>
            _mediator.Publish(new MediatRPublishNotification());

        [BenchmarkCategory("Publish1"), Benchmark]
        public Task EventFlux_Bus_Publish_OneHandler() =>
            _eventBus.PublishAsync(new OverheadPublishRequest());

        [BenchmarkCategory("Publish1"), Benchmark]
        public Task EventFlux_Dispatcher_Publish_OneHandler() =>
            _eventDispatcher.PublishAsync(new OverheadPublishRequest());

        [BenchmarkCategory("Publish3"), Benchmark(Baseline = true)]
        public Task MediatR_Publish_ThreeHandlers() =>
            _mediator.Publish(new MediatRFanOutNotification());

        [BenchmarkCategory("Publish3"), Benchmark]
        public Task EventFlux_Bus_Publish_ThreeHandlers() =>
            _eventBus.PublishAsync(new OverheadFanOutRequest());

        [BenchmarkCategory("Publish3"), Benchmark]
        public Task EventFlux_Dispatcher_Publish_ThreeHandlers() =>
            _eventDispatcher.PublishAsync(new OverheadFanOutRequest());
    }
}
