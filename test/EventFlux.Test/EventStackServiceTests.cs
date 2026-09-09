using EventFlux.Abstractions;
using EventFlux.Extensions;
using EventFlux.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    public class StackTestEvent : IEventRequest
    {
        public string Payload { get; set; } = string.Empty;
    }

    public class StackTestEventHandler : IEventHandler<StackTestEvent>
    {
        public static readonly List<string> HandledPayloads = new();
        public static readonly object SyncLock = new();

        public Task Handle(StackTestEvent @event)
        {
            lock (SyncLock)
            {
                HandledPayloads.Add(@event.Payload);
            }

            return Task.CompletedTask;
        }
    }

    public class EventStackServiceTests
    {
        [Fact]
        public void EventStackService_AddAndDrain_OperatesCorrectly()
        {
            // Arrange
            var stackService = new EventStackService();
            var event1 = new StackTestEvent { Payload = "1" };
            var event2 = new StackTestEvent { Payload = "2" };

            // Act
            stackService.AddEventRequest(event1);
            stackService.AddEventRequest(event2);

            // Assert
            Assert.False(stackService.IsEmpty);
            Assert.Equal(2, stackService.Count);

            var drained = stackService.Drain();
            Assert.Equal(2, drained.Count);
            Assert.Same(event1, drained[0]);
            Assert.Same(event2, drained[1]);

            Assert.True(stackService.IsEmpty);
            Assert.Equal(0, stackService.Count);
        }

        [Fact]
        public void EventStackService_Clear_EmptiesTheQueue()
        {
            // Arrange
            var stackService = new EventStackService();
            stackService.AddEventRequest(new StackTestEvent { Payload = "1" });
            stackService.AddEventRequest(new StackTestEvent { Payload = "2" });

            // Act
            stackService.Clear();

            // Assert
            Assert.True(stackService.IsEmpty);
            Assert.Equal(0, stackService.Count);

            var drained = stackService.Drain();
            Assert.Empty(drained);
        }

        [Fact]
        public void AddEventBus_RegistersEventStackServiceAsSingleton_SharedAcrossScopes()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(EventStackServiceTests).Assembly);

            using var rootProvider = services.BuildServiceProvider();

            // Act
            var rootStack = rootProvider.GetRequiredService<EventStackService>();

            using var scope1 = rootProvider.CreateScope();
            var scope1Stack = scope1.ServiceProvider.GetRequiredService<EventStackService>();

            using var scope2 = rootProvider.CreateScope();
            var scope2Stack = scope2.ServiceProvider.GetRequiredService<EventStackService>();

            // Assert
            Assert.NotNull(rootStack);
            Assert.Same(rootStack, scope1Stack);
            Assert.Same(scope1Stack, scope2Stack);
        }

        [Fact]
        public async Task EventBus_AddStackRequestEvent_InOneScope_CanBeDispatchedInAnotherScope()
        {
            // Arrange
            lock (StackTestEventHandler.SyncLock)
            {
                StackTestEventHandler.HandledPayloads.Clear();
            }

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(EventStackServiceTests).Assembly);

            using var rootProvider = services.BuildServiceProvider();

            // Act
            using (var scope1 = rootProvider.CreateScope())
            {
                var bus1 = scope1.ServiceProvider.GetRequiredService<IEventBus>();
                bus1.AddStackRequestEvent(new StackTestEvent { Payload = "from-scope-1" });
            }

            using (var scope2 = rootProvider.CreateScope())
            {
                var bus2 = scope2.ServiceProvider.GetRequiredService<IEventBus>();
                bus2.AddStackRequestEvent(new StackTestEvent { Payload = "from-scope-2" });
            }

            using (var scope3 = rootProvider.CreateScope())
            {
                var bus3 = scope3.ServiceProvider.GetRequiredService<IEventBus>();
                await bus3.StackEventDispatcherAsync();
            }

            // Assert
            lock (StackTestEventHandler.SyncLock)
            {
                Assert.Equal(2, StackTestEventHandler.HandledPayloads.Count);
                Assert.Contains("from-scope-1", StackTestEventHandler.HandledPayloads);
                Assert.Contains("from-scope-2", StackTestEventHandler.HandledPayloads);
            }
        }

        [Fact]
        public async Task EventStackService_CanBeInjectedDirectly_AndDispatchedByEventBus()
        {
            // Arrange
            lock (StackTestEventHandler.SyncLock)
            {
                StackTestEventHandler.HandledPayloads.Clear();
            }

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(EventStackServiceTests).Assembly);

            using var rootProvider = services.BuildServiceProvider();

            // Act
            var stackService = rootProvider.GetRequiredService<EventStackService>();
            stackService.AddEventRequest(new StackTestEvent { Payload = "direct-enqueue" });

            using (var scope = rootProvider.CreateScope())
            {
                var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                await bus.StackEventDispatcherAsync();
            }

            // Assert
            lock (StackTestEventHandler.SyncLock)
            {
                Assert.Single(StackTestEventHandler.HandledPayloads);
                Assert.Equal("direct-enqueue", StackTestEventHandler.HandledPayloads[0]);
            }
        }

        [Fact]
        public void EventBus_WithCustomInjectedEventStackService_UsesProvidedInstance()
        {
            // Arrange
            var customStackService = new EventStackService();
            var eventBus = new EventBus(null!, null!, null, customStackService);
            var testEvent = new StackTestEvent { Payload = "custom-instance" };

            // Act
            eventBus.AddStackRequestEvent(testEvent);

            // Assert
            Assert.Equal(1, customStackService.Count);
            var drained = customStackService.Drain();
            Assert.Single(drained);
            Assert.Same(testEvent, drained[0]);
        }

        [Fact]
        public void EventBus_ConstructedWithoutServiceProvider_MaintainsLocalEventStackService()
        {
            // Arrange
            var eventBus = new EventBus();
            var testEvent = new StackTestEvent { Payload = "standalone" };

            // Act
            eventBus.AddStackRequestEvent(testEvent);

            // Assert
            Assert.NotNull(eventBus);
        }
    }
}
