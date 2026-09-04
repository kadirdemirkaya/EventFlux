using EventFlux.Abstractions;
using EventFlux.Delegates;
using EventFlux.Extensions;
using EventFlux.Services;
using EventFlux.Test.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    public class OrderedPipelineTrace
    {
        public static readonly List<string> Steps = new();

        public static void Add(string step)
        {
            lock (Steps) Steps.Add(step);
        }

        public static void Reset()
        {
            lock (Steps) Steps.Clear();
        }
    }

    public class FirstPipeline<TRequest> : IEventCustomPipeline<TRequest>
        where TRequest : IEventRequest
    {
        public async Task Handle(TRequest request, EventHandlerDelegate next, CancellationToken cancellationToken)
        {
            OrderedPipelineTrace.Add("first:before");
            await next(cancellationToken);
            OrderedPipelineTrace.Add("first:after");
        }
    }

    public class SecondPipeline<TRequest> : IEventCustomPipeline<TRequest>
        where TRequest : IEventRequest
    {
        public async Task Handle(TRequest request, EventHandlerDelegate next, CancellationToken cancellationToken)
        {
            OrderedPipelineTrace.Add("second:before");
            await next(cancellationToken);
            OrderedPipelineTrace.Add("second:after");
        }
    }

    [CollectionDefinition("SharedHandlerCounters", DisableParallelization = true)]
    public class SharedHandlerCountersCollection
    {
    }

    [Collection("SharedHandlerCounters")]
    public class FeatureCoverageTests
    {
        private static ServiceProvider BuildProvider(Action<IServiceCollection>? extra = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventDispatcher();
            services.AddEventBus(typeof(FeatureCoverageTests).Assembly);
            extra?.Invoke(services);

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Feature_EventBus_SendAsync_ReturnsResponse()
        {
            using var provider = BuildProvider();

            var response = await provider.GetRequiredService<IEventBus>()
                .SendAsync<ExampleEventResponse>(new ExampleEventRequest { Num = 21 });

            Assert.NotNull(response);
            Assert.Equal(42, response.Result);
        }

        [Fact]
        public async Task Feature_EventDispatcher_SendAsync_ReturnsResponse()
        {
            using var provider = BuildProvider();

            var response = await provider.GetRequiredService<IEventDispatcher>()
                .SendAsync<ExampleEventResponse>(new ExampleEventRequest { Num = 21 });

            Assert.NotNull(response);
            Assert.Equal(42, response.Result);
        }

        [Fact]
        public async Task Feature_EventBus_PublishAsync_InvokesHandler()
        {
            using var provider = BuildProvider();
            ResolutionEventHandler.HandledCount = 0;

            await provider.GetRequiredService<IEventBus>()
                .PublishAsync(new ResolutionEventRequest());

            Assert.Equal(1, ResolutionEventHandler.HandledCount);
        }

        [Fact]
        public async Task Feature_EventDispatcher_PublishAsync_InvokesHandler()
        {
            using var provider = BuildProvider();
            ResolutionEventHandler.HandledCount = 0;

            await provider.GetRequiredService<IEventDispatcher>()
                .PublishAsync(new ResolutionEventRequest());

            Assert.Equal(1, ResolutionEventHandler.HandledCount);
        }

        [Fact]
        public async Task Feature_MultipleHandlersPerEvent_AllInvoked()
        {
            using var provider = BuildProvider();
            OrderedResolutionEventHandlerFirst.HandledCount = 0;
            OrderedResolutionEventHandlerSecond.HandledCount = 0;

            await provider.GetRequiredService<IEventBus>()
                .PublishAsync(new OrderedResolutionEventRequest());

            Assert.Equal(1, OrderedResolutionEventHandlerFirst.HandledCount);
            Assert.Equal(1, OrderedResolutionEventHandlerSecond.HandledCount);
        }

        [Fact]
        public void Feature_AutomaticHandlerDiscovery_RegistersHandlersFromAssembly()
        {
            using var provider = BuildProvider();

            var handlers = provider.GetServices<IEventHandler<OrderedResolutionEventRequest>>().ToList();

            Assert.Equal(2, handlers.Count);
        }

        [Fact]
        public async Task Feature_CanHandle_False_SkipsHandler()
        {
            using var provider = BuildProvider();
            GatedResolutionEventHandler.HandledCount = 0;

            await provider.GetRequiredService<IEventBus>()
                .PublishAsync(new GatedResolutionEventRequest { Allow = false });

            Assert.Equal(0, GatedResolutionEventHandler.HandledCount);
        }

        [Fact]
        public async Task Feature_HandlerOrderAttribute_OrdersDescriptors()
        {
            using var provider = BuildProvider();
            OrderedResolutionEventHandlerFirst.HandledCount = 0;
            OrderedResolutionEventHandlerSecond.HandledCount = 0;

            await provider.GetRequiredService<IEventBus>()
                .PublishAsync(new OrderedResolutionEventRequest());

            Assert.Equal(1, OrderedResolutionEventHandlerFirst.HandledCount);
            Assert.Equal(1, OrderedResolutionEventHandlerSecond.HandledCount);
        }

        [Fact]
        public async Task Feature_PipelineBehaviors_RunInRegistrationOrder()
        {
            using var provider = BuildProvider(s =>
            {
                s.AddTransient(typeof(IEventCustomPipeline<>), typeof(FirstPipeline<>));
                s.AddTransient(typeof(IEventCustomPipeline<>), typeof(SecondPipeline<>));
            });
            OrderedPipelineTrace.Reset();
            ResolutionEventHandler.HandledCount = 0;

            await provider.GetRequiredService<IEventDispatcher>()
                .PublishAsync(new ResolutionEventRequest());

            Assert.Equal(
                new[] { "first:before", "second:before", "second:after", "first:after" },
                OrderedPipelineTrace.Steps);
            Assert.Equal(1, ResolutionEventHandler.HandledCount);
        }

        [Fact]
        public async Task Feature_AddEventLogging_DoesNotBreakDispatch()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventDispatcher().AddEventLogging();
            services.AddEventBus(typeof(FeatureCoverageTests).Assembly);
            using var provider = services.BuildServiceProvider();
            ResolutionEventHandler.HandledCount = 0;

            await provider.GetRequiredService<IEventDispatcher>()
                .PublishAsync(new ResolutionEventRequest());

            Assert.Equal(1, ResolutionEventHandler.HandledCount);
        }

        [Fact]
        public async Task Feature_AddEventTimeout_DoesNotBreakDispatch()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventDispatcher().AddEventTimeout();
            services.AddEventBus(typeof(FeatureCoverageTests).Assembly);
            using var provider = services.BuildServiceProvider();
            ResolutionEventHandler.HandledCount = 0;

            await provider.GetRequiredService<IEventDispatcher>()
                .PublishAsync(new ResolutionEventRequest());

            Assert.Equal(1, ResolutionEventHandler.HandledCount);
        }

        [Fact]
        public async Task Feature_DeferredBatchDispatch_DrainsStackedEvents()
        {
            using var provider = BuildProvider();
            var eventBus = provider.GetRequiredService<IEventBus>();
            ResolutionEventHandler.HandledCount = 0;

            eventBus.AddStackRequestEvent(new ResolutionEventRequest { Data = "a" });
            eventBus.AddStackRequestEvent(new ResolutionEventRequest { Data = "b" });

            Assert.Equal(0, ResolutionEventHandler.HandledCount);

            await eventBus.StackEventDispatcherAsync();

            Assert.Equal(2, ResolutionEventHandler.HandledCount);
        }

        [Fact]
        public void Feature_EventService_And_EventMapService_ResolvableFromDI()
        {
            using var provider = BuildProvider();

            var eventService = provider.GetService<EventService>();
            var eventMapService = provider.GetService<EventMapService>();

            Assert.NotNull(eventService);
            Assert.NotNull(eventMapService);
            Assert.NotEmpty(eventService!.InternalEventHandlers);
        }

        [Fact]
        public void Feature_EventMapService_MapsRequestToResponse()
        {
            using var provider = BuildProvider();

            var found = provider.GetRequiredService<EventMapService>()
                .TryGetValue(typeof(ExampleEventRequest), out var responseType);

            Assert.True(found);
            Assert.Equal(typeof(ExampleEventResponse), responseType);
        }

        [Fact]
        public void Feature_EventService_ListsHandlersForEvent()
        {
            using var provider = BuildProvider();

            var handlers = provider.GetRequiredService<EventService>()
                .GetHandlersForEvent(typeof(MultiHandlerEventRequest));

            Assert.NotNull(handlers);
            Assert.Equal(2, handlers!.Count);
        }

        [Fact]
        public async Task Feature_Cancellation_EventBusSendAsync()
        {
            using var provider = BuildProvider();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                provider.GetRequiredService<IEventBus>()
                    .SendAsync<ExampleEventResponse>(new ExampleEventRequest { Num = 1 }, cts.Token));
        }

        [Fact]
        public async Task Feature_Cancellation_EventBusPublishAsync()
        {
            using var provider = BuildProvider();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                provider.GetRequiredService<IEventBus>()
                    .PublishAsync(new ResolutionEventRequest(), cts.Token));
        }

        [Fact]
        public async Task Feature_Cancellation_EventBusStackDispatcher()
        {
            using var provider = BuildProvider();
            var eventBus = provider.GetRequiredService<IEventBus>();
            eventBus.AddStackRequestEvent(new ResolutionEventRequest());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                eventBus.StackEventDispatcherAsync(cts.Token));
        }

        [Fact]
        public async Task Feature_Cancellation_EventDispatcherPublishAsync()
        {
            using var provider = BuildProvider();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                provider.GetRequiredService<IEventDispatcher>()
                    .PublishAsync(new ResolutionEventRequest(), cts.Token));
        }

        [Fact]
        public async Task Feature_NoHandler_PublishCompletesQuietly()
        {
            using var provider = BuildProvider();

            await provider.GetRequiredService<IEventBus>()
                .PublishAsync(new NoHandlerEventRequest());
        }

        [Fact]
        public async Task Feature_NoHandler_SendThrows()
        {
            using var provider = BuildProvider();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.GetRequiredService<IEventBus>()
                    .SendAsync<UnhandledEventResponse>(new UnhandledEventRequest()));
        }
    }
}
