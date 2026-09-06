using EventFlux.Abstractions;
using EventFlux.Attributes;
using EventFlux.Delegates;
using EventFlux.Extensions;
using EventFlux.Options;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventFlux.Test
{
    [Collection("SharedHandlerCounters")]
    public class PublishStrategyTests
    {
        public record OrderedStrategyEvent(string Id) : IEventRequest;

        public class OrderExecutionTracker
        {
            public List<string> Events { get; } = new();
            private readonly object _lock = new();

            public void Record(string step)
            {
                lock (_lock)
                {
                    Events.Add(step);
                }
            }
        }

        [HandlerOrder(1)]
        public class SlowOrderedHandler : IEventHandler<OrderedStrategyEvent>
        {
            private readonly OrderExecutionTracker _tracker;

            public SlowOrderedHandler(OrderExecutionTracker tracker)
            {
                _tracker = tracker;
            }

            public async Task Handle(OrderedStrategyEvent @event)
            {
                _tracker.Record("SlowStart");
                await Task.Delay(60).ConfigureAwait(false);
                _tracker.Record("SlowEnd");
            }
        }

        [HandlerOrder(2)]
        public class FastOrderedHandler : IEventHandler<OrderedStrategyEvent>
        {
            private readonly OrderExecutionTracker _tracker;

            public FastOrderedHandler(OrderExecutionTracker tracker)
            {
                _tracker = tracker;
            }

            public async Task Handle(OrderedStrategyEvent @event)
            {
                _tracker.Record("FastStart");
                await Task.Delay(10).ConfigureAwait(false);
                _tracker.Record("FastEnd");
            }
        }

        [HandlerOrder(3)]
        public class ConditionalOrderedHandler : IEventHandler<OrderedStrategyEvent>
        {
            private readonly OrderExecutionTracker _tracker;

            public ConditionalOrderedHandler(OrderExecutionTracker tracker)
            {
                _tracker = tracker;
            }

            public bool CanHandle(OrderedStrategyEvent @event) => @event.Id != "skip-conditional";

            public Task Handle(OrderedStrategyEvent @event)
            {
                _tracker.Record("Conditional");
                return Task.CompletedTask;
            }
        }

        public class StrategyLoggingPipeline<TRequest> : IEventCustomPipeline<TRequest>
            where TRequest : IEventRequest
        {
            private readonly OrderExecutionTracker _tracker;

            public StrategyLoggingPipeline(OrderExecutionTracker tracker)
            {
                _tracker = tracker;
            }

            public async Task Handle(TRequest request, EventHandlerDelegate next, CancellationToken cancellationToken)
            {
                _tracker.Record("PipelineStart");
                await next(cancellationToken).ConfigureAwait(false);
                _tracker.Record("PipelineEnd");
            }
        }

        private static ServiceProvider CreateProvider(
            OrderExecutionTracker tracker,
            Action<EventFluxOptions>? configure = null,
            bool registerDispatcher = true,
            bool registerPipeline = false)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(tracker);

            if (configure != null)
            {
                services.AddEventBus(configure, typeof(PublishStrategyTests).Assembly);
                if (registerDispatcher)
                {
                    services.AddEventDispatcher(configure);
                }
            }
            else
            {
                services.AddEventBus(typeof(PublishStrategyTests).Assembly);
                if (registerDispatcher)
                {
                    services.AddEventDispatcher();
                }
            }

            if (registerPipeline)
            {
                services.AddTransient<IEventCustomPipeline<OrderedStrategyEvent>, StrategyLoggingPipeline<OrderedStrategyEvent>>();
            }

            return services.BuildServiceProvider();
        }

        [Fact]
        public void DefaultOptions_HasPublishStrategyParallel()
        {
            // Arrange
            var options = new EventFluxOptions();

            // Assert
            Assert.Equal(PublishStrategy.Parallel, options.PublishStrategy);
        }

        [Fact]
        public void AddEventBus_WithCustomPublishStrategy_RegistersConfiguredOptions()
        {
            // Arrange
            var tracker = new OrderExecutionTracker();
            using var sp = CreateProvider(tracker, opt => opt.PublishStrategy = PublishStrategy.Sequential);

            // Act
            var options = sp.GetRequiredService<EventFluxOptions>();

            // Assert
            Assert.Equal(PublishStrategy.Sequential, options.PublishStrategy);
        }

        [Fact]
        public async Task EventBus_PublishAsync_WithDefaultParallelStrategy_RunsConcurrently()
        {
            // Arrange
            var tracker = new OrderExecutionTracker();
            using var sp = CreateProvider(tracker);
            var eventBus = sp.GetRequiredService<IEventBus>();

            // Act
            await eventBus.PublishAsync(new OrderedStrategyEvent("test-parallel"));

            // Assert
            Assert.Contains("Conditional", tracker.Events);
            Assert.True(tracker.Events.IndexOf("SlowStart") < tracker.Events.IndexOf("FastStart"));
            Assert.True(tracker.Events.IndexOf("FastEnd") < tracker.Events.IndexOf("SlowEnd"));
        }

        [Fact]
        public async Task EventBus_PublishAsync_WithSequentialStrategy_WaitsEachHandlerToComplete()
        {
            // Arrange
            var tracker = new OrderExecutionTracker();
            using var sp = CreateProvider(tracker, opt => opt.PublishStrategy = PublishStrategy.Sequential);
            var eventBus = sp.GetRequiredService<IEventBus>();

            // Act
            await eventBus.PublishAsync(new OrderedStrategyEvent("test-sequential"));

            // Assert
            Assert.Equal(5, tracker.Events.Count);
            Assert.Equal("SlowStart", tracker.Events[0]);
            Assert.Equal("SlowEnd", tracker.Events[1]);
            Assert.Equal("FastStart", tracker.Events[2]);
            Assert.Equal("FastEnd", tracker.Events[3]);
            Assert.Equal("Conditional", tracker.Events[4]);
        }

        [Fact]
        public async Task EventBus_PublishAsync_WithSequentialStrategy_SkipsWhenCanHandleIsFalse()
        {
            // Arrange
            var tracker = new OrderExecutionTracker();
            using var sp = CreateProvider(tracker, opt => opt.PublishStrategy = PublishStrategy.Sequential);
            var eventBus = sp.GetRequiredService<IEventBus>();

            // Act
            await eventBus.PublishAsync(new OrderedStrategyEvent("skip-conditional"));

            // Assert
            Assert.Equal(4, tracker.Events.Count);
            Assert.DoesNotContain("Conditional", tracker.Events);
            Assert.Equal("SlowStart", tracker.Events[0]);
            Assert.Equal("SlowEnd", tracker.Events[1]);
            Assert.Equal("FastStart", tracker.Events[2]);
            Assert.Equal("FastEnd", tracker.Events[3]);
        }

        [Fact]
        public async Task EventDispatcher_PublishAsync_WithSequentialStrategy_SkipsWhenCanHandleIsFalse()
        {
            // Arrange
            var tracker = new OrderExecutionTracker();
            using var sp = CreateProvider(tracker, opt => opt.PublishStrategy = PublishStrategy.Sequential);
            var dispatcher = sp.GetRequiredService<IEventDispatcher>();

            // Act
            await dispatcher.PublishAsync(new OrderedStrategyEvent("skip-conditional"));

            // Assert
            Assert.Equal(4, tracker.Events.Count);
            Assert.DoesNotContain("Conditional", tracker.Events);
        }

        [Fact]
        public async Task EventDispatcher_PublishAsync_WithDefaultParallelStrategy_RunsConcurrently()
        {
            // Arrange
            var tracker = new OrderExecutionTracker();
            using var sp = CreateProvider(tracker);
            var dispatcher = sp.GetRequiredService<IEventDispatcher>();

            // Act
            await dispatcher.PublishAsync(new OrderedStrategyEvent("test-dispatcher-parallel"));

            // Assert
            Assert.Contains("Conditional", tracker.Events);
            Assert.True(tracker.Events.IndexOf("SlowStart") < tracker.Events.IndexOf("FastStart"));
            Assert.True(tracker.Events.IndexOf("FastEnd") < tracker.Events.IndexOf("SlowEnd"));
        }

        [Fact]
        public async Task EventDispatcher_PublishAsync_WithSequentialStrategy_WaitsEachHandlerToComplete()
        {
            // Arrange
            var tracker = new OrderExecutionTracker();
            using var sp = CreateProvider(tracker, opt => opt.PublishStrategy = PublishStrategy.Sequential, registerPipeline: true);
            var dispatcher = sp.GetRequiredService<IEventDispatcher>();

            // Act
            await dispatcher.PublishAsync(new OrderedStrategyEvent("test-dispatcher-sequential"));

            // Assert
            Assert.Equal(7, tracker.Events.Count);
            Assert.Equal("PipelineStart", tracker.Events[0]);
            Assert.Equal("SlowStart", tracker.Events[1]);
            Assert.Equal("SlowEnd", tracker.Events[2]);
            Assert.Equal("FastStart", tracker.Events[3]);
            Assert.Equal("FastEnd", tracker.Events[4]);
            Assert.Equal("Conditional", tracker.Events[5]);
            Assert.Equal("PipelineEnd", tracker.Events[6]);
        }

        [Fact]
        public async Task EventBus_PublishAsync_WithSequentialStrategy_AbortsWhenCancelled()
        {
            // Arrange
            var tracker = new OrderExecutionTracker();
            using var cts = new CancellationTokenSource();
            using var sp = CreateProvider(tracker, opt => opt.PublishStrategy = PublishStrategy.Sequential);
            var eventBus = sp.GetRequiredService<IEventBus>();

            cts.CancelAfter(20);

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await eventBus.PublishAsync(new OrderedStrategyEvent("test-cancel"), cts.Token);
            });

            Assert.DoesNotContain("FastStart", tracker.Events);
        }
    }
}
