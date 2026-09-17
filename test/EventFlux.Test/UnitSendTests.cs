using EventFlux.Abstractions;
using EventFlux.Test.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    public class UnitSendTests
    {
        private sealed class LegacyEventBus : IEventBus
        {
            public List<object> SentRequests { get; } = new();

            public void AddStackRequestEvent<TEvent>(TEvent eventRequest) where TEvent : IEventRequest
            {
            }

            public Task PublishAsync(IEventRequest request, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task<TResponse?> SendAsync<TResponse>(IEventRequest<TResponse> request, CancellationToken cancellationToken = default)
                where TResponse : IEventResponse
            {
                SentRequests.Add(request);
                return Task.FromResult<TResponse?>(default);
            }

            public Task StackEventDispatcherAsync(CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        private static ServiceProvider BuildProvider(Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(UnitSendTests).Assembly);
            services.AddEventDispatcher();
            configure?.Invoke(services);
            return services.BuildServiceProvider();
        }

        [Fact]
        public void Unit_AllValuesAreEqual()
        {
            // Arrange
            var left = EventFlux.Abstractions.Unit.Value;
            var right = default(EventFlux.Abstractions.Unit);

            // Assert
            Assert.Equal(left, right);
            Assert.True(left == right);
            Assert.False(left != right);
            Assert.Equal(0, left.CompareTo(right));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
            Assert.True(left.Equals((object)right));
            Assert.IsAssignableFrom<IEventResponse>(left);
        }

        [Fact]
        public async Task Unit_Task_IsCompletedWithValue()
        {
            // Act
            var result = await EventFlux.Abstractions.Unit.Task;

            // Assert
            Assert.True(EventFlux.Abstractions.Unit.Task.IsCompletedSuccessfully);
            Assert.Equal(EventFlux.Abstractions.Unit.Value, result);
        }

        [Theory]
        [InlineData(typeof(IEventBus))]
        [InlineData(typeof(IEventDispatcher))]
        public void DispatchContracts_ExposeNonGenericSendForUnitRequests(Type contract)
        {
            // Act
            var method = contract.GetMethod(
                nameof(IEventBus.SendAsync),
                new[] { typeof(IEventRequest<EventFlux.Abstractions.Unit>), typeof(CancellationToken) });

            // Assert
            Assert.NotNull(method);
            Assert.False(method!.IsGenericMethod);
            Assert.False(method.IsAbstract);
            Assert.Equal(typeof(Task), method.ReturnType);
        }

        [Fact]
        public async Task EventBus_SendAsync_UnitRequest_InvokesHandler()
        {
            // Arrange
            using var provider = BuildProvider();
            using var scope = provider.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            var command = new RecordUnitCommand();

            // Act
            await bus.SendAsync(command);

            // Assert
            Assert.Equal(new[] { "handler" }, command.Trace);
        }

        [Fact]
        public async Task EventBus_SendAsyncGeneric_UnitRequest_ReturnsUnitValue()
        {
            // Arrange
            using var provider = BuildProvider();
            using var scope = provider.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            var command = new RecordUnitCommand();

            // Act
            var result = await bus.SendAsync<EventFlux.Abstractions.Unit>(command);

            // Assert
            Assert.Equal(EventFlux.Abstractions.Unit.Value, result);
            Assert.Single(command.Trace);
        }

        [Fact]
        public async Task EventBus_SendAsync_UnitRequestWithoutHandler_ThrowsInvalidOperationException()
        {
            // Arrange
            using var provider = BuildProvider();
            using var scope = provider.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => bus.SendAsync(new UnhandledUnitCommand()));
        }

        [Fact]
        public async Task EventBus_SendAsync_NullUnitRequest_ThrowsArgumentNullException()
        {
            // Arrange
            using var provider = BuildProvider();
            using var scope = provider.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => bus.SendAsync((IEventRequest<EventFlux.Abstractions.Unit>)null!));
        }

        [Fact]
        public async Task EventBus_SendAsync_UnitRequestDeclinedByCanHandle_CompletesWithoutInvokingHandler()
        {
            // Arrange
            using var provider = BuildProvider();
            using var scope = provider.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            var command = new DeclinedUnitCommand();

            // Act
            await bus.SendAsync(command);

            // Assert
            Assert.Empty(command.Trace);
        }

        [Fact]
        public async Task EventBus_SendAsync_UnitRequestWithCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            using var provider = BuildProvider();
            using var scope = provider.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bus.SendAsync(new RecordUnitCommand(), cts.Token));
        }

        [Fact]
        public async Task EventDispatcher_SendAsync_UnitRequest_RunsPipelineBehaviorsAroundHandler()
        {
            // Arrange
            using var provider = BuildProvider(services => services.AddEventBehavior<RecordUnitCommandBehavior>());
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();
            var command = new RecordUnitCommand();

            // Act
            await dispatcher.SendAsync(command);

            // Assert
            Assert.Equal(new[] { "closed-behavior", "handler" }, command.Trace);
        }

        [Fact]
        public async Task EventDispatcher_SendAsync_UnitRequestWithoutHandler_ThrowsInvalidOperationException()
        {
            // Arrange
            using var provider = BuildProvider();
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.SendAsync(new UnhandledUnitCommand()));
        }

        [Fact]
        public async Task ExistingIEventBusImplementation_WithoutUnitOverload_ForwardsToGenericSend()
        {
            // Arrange
            IEventBus bus = new LegacyEventBus();
            var command = new RecordUnitCommand();

            // Act
            await bus.SendAsync(command);

            // Assert
            Assert.Same(command, Assert.Single(((LegacyEventBus)bus).SentRequests));
        }
    }
}
