using EventFlux.Abstractions;
using EventFlux.Extensions;
using EventFlux.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    public class ScopedProbeDependency
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public class ScopeTestCommand : IEventRequest<ScopeTestResponse>
    {
    }

    public class ScopeTestResponse : IEventResponse
    {
        public Guid ScopedId { get; set; }
    }

    public class ScopeTestCommandHandler : IEventHandler<ScopeTestCommand, ScopeTestResponse>
    {
        private readonly ScopedProbeDependency _dependency;

        public ScopeTestCommandHandler(ScopedProbeDependency dependency)
        {
            _dependency = dependency;
        }

        public Task<ScopeTestResponse> Handle(ScopeTestCommand request)
        {
            return Task.FromResult(new ScopeTestResponse
            {
                ScopedId = _dependency.Id
            });
        }
    }

    public class ScopeTestNotification : IEventRequest
    {
        public static readonly List<Guid> RecordedIds = new();

        public static void Reset()
        {
            lock (RecordedIds)
            {
                RecordedIds.Clear();
            }
        }
    }

    public class ScopeTestNotificationHandler : IEventHandler<ScopeTestNotification>
    {
        private readonly ScopedProbeDependency _dependency;

        public ScopeTestNotificationHandler(ScopedProbeDependency dependency)
        {
            _dependency = dependency;
        }

        public Task Handle(ScopeTestNotification @event)
        {
            lock (ScopeTestNotification.RecordedIds)
            {
                ScopeTestNotification.RecordedIds.Add(_dependency.Id);
            }

            return Task.CompletedTask;
        }
    }

    [Collection("SharedHandlerCounters")]
    public class OptionsAndScopeTests
    {
        private static ServiceProvider CreateProvider(Action<EventFluxOptions>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped<ScopedProbeDependency>();

            if (configure != null)
            {
                services.AddEventBus(configure, typeof(OptionsAndScopeTests).Assembly);
                services.AddEventDispatcher(configure);
            }
            else
            {
                services.AddEventBus(typeof(OptionsAndScopeTests).Assembly);
                services.AddEventDispatcher();
            }

            return services.BuildServiceProvider();
        }

        [Fact]
        public void DefaultOptions_HasCreateScopePerEventTrue()
        {
            // Arrange & Act
            var options = new EventFluxOptions();

            // Assert
            Assert.True(options.CreateScopePerEvent);
        }

        [Fact]
        public void AddEventBus_WithCustomOptions_RegistersConfiguredOptions()
        {
            // Arrange & Act
            using var provider = CreateProvider(opt => opt.CreateScopePerEvent = false);
            var options = provider.GetRequiredService<EventFluxOptions>();

            // Assert
            Assert.False(options.CreateScopePerEvent);
        }

        [Fact]
        public async Task EventBus_SendAsync_WithAmbientScope_SharesCallersScopedService()
        {
            // Arrange
            using var provider = CreateProvider(opt => opt.CreateScopePerEvent = false);
            using var callerScope = provider.CreateScope();

            var callerDependency = callerScope.ServiceProvider.GetRequiredService<ScopedProbeDependency>();
            var bus = callerScope.ServiceProvider.GetRequiredService<IEventBus>();

            // Act
            var response = await bus.SendAsync(new ScopeTestCommand());

            // Assert
            Assert.NotNull(response);
            Assert.Equal(callerDependency.Id, response.ScopedId);
        }

        [Fact]
        public async Task EventBus_SendAsync_WithDefaultChildScope_DoesNotShareCallersScopedService()
        {
            // Arrange
            using var provider = CreateProvider();
            using var callerScope = provider.CreateScope();

            var callerDependency = callerScope.ServiceProvider.GetRequiredService<ScopedProbeDependency>();
            var bus = callerScope.ServiceProvider.GetRequiredService<IEventBus>();

            // Act
            var response = await bus.SendAsync(new ScopeTestCommand());

            // Assert
            Assert.NotNull(response);
            Assert.NotEqual(callerDependency.Id, response.ScopedId);
        }

        [Fact]
        public async Task EventBus_PublishAsync_WithAmbientScope_SharesCallersScopedService()
        {
            // Arrange
            ScopeTestNotification.Reset();
            using var provider = CreateProvider(opt => opt.CreateScopePerEvent = false);
            using var callerScope = provider.CreateScope();

            var callerDependency = callerScope.ServiceProvider.GetRequiredService<ScopedProbeDependency>();
            var bus = callerScope.ServiceProvider.GetRequiredService<IEventBus>();

            // Act
            await bus.PublishAsync(new ScopeTestNotification());

            // Assert
            var recordedId = Assert.Single(ScopeTestNotification.RecordedIds);
            Assert.Equal(callerDependency.Id, recordedId);
        }

        [Fact]
        public async Task EventDispatcher_SendAsync_WithAmbientScope_SharesCallersScopedService()
        {
            // Arrange
            using var provider = CreateProvider(opt => opt.CreateScopePerEvent = false);
            using var callerScope = provider.CreateScope();

            var callerDependency = callerScope.ServiceProvider.GetRequiredService<ScopedProbeDependency>();
            var dispatcher = callerScope.ServiceProvider.GetRequiredService<IEventDispatcher>();

            // Act
            var response = await dispatcher.SendAsync(new ScopeTestCommand());

            // Assert
            Assert.NotNull(response);
            Assert.Equal(callerDependency.Id, response.ScopedId);
        }

        [Fact]
        public async Task EventDispatcher_PublishAsync_WithAmbientScope_SharesCallersScopedService()
        {
            // Arrange
            ScopeTestNotification.Reset();
            using var provider = CreateProvider(opt => opt.CreateScopePerEvent = false);
            using var callerScope = provider.CreateScope();

            var callerDependency = callerScope.ServiceProvider.GetRequiredService<ScopedProbeDependency>();
            var dispatcher = callerScope.ServiceProvider.GetRequiredService<IEventDispatcher>();

            // Act
            await dispatcher.PublishAsync(new ScopeTestNotification());

            // Assert
            var recordedId = Assert.Single(ScopeTestNotification.RecordedIds);
            Assert.Equal(callerDependency.Id, recordedId);
        }

        [Fact]
        public async Task EventDispatcher_SendAsync_WithDefaultChildScope_DoesNotShareCallersScopedService()
        {
            // Arrange
            using var provider = CreateProvider();
            using var callerScope = provider.CreateScope();

            var callerDependency = callerScope.ServiceProvider.GetRequiredService<ScopedProbeDependency>();
            var dispatcher = callerScope.ServiceProvider.GetRequiredService<IEventDispatcher>();

            // Act
            var response = await dispatcher.SendAsync(new ScopeTestCommand());

            // Assert
            Assert.NotNull(response);
            Assert.NotEqual(callerDependency.Id, response.ScopedId);
        }
    }
}
