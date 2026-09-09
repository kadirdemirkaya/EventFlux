using EventFlux.Abstractions;
using EventFlux.Extensions;
using EventFlux.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    public class LifetimeRequest : IEventRequest<LifetimeResponse>
    {
    }

    public class LifetimeResponse : IEventResponse
    {
        public Guid InstanceId { get; set; }
    }

    public class LifetimeRequestHandler : IEventHandler<LifetimeRequest, LifetimeResponse>
    {
        public Guid InstanceId { get; } = Guid.NewGuid();

        public Task<LifetimeResponse> Handle(LifetimeRequest request)
        {
            return Task.FromResult(new LifetimeResponse { InstanceId = InstanceId });
        }
    }

    public class LifetimeNotification : IEventRequest
    {
    }

    public class LifetimeNotificationHandler : IEventHandler<LifetimeNotification>
    {
        public Guid InstanceId { get; } = Guid.NewGuid();

        public Task Handle(LifetimeNotification @event)
        {
            return Task.CompletedTask;
        }
    }

    public class HandlerLifetimeTests
    {
        [Fact]
        public void DefaultOptions_HasHandlerLifetimeTransient()
        {
            // Arrange
            var options = new EventFluxOptions();

            // Act
            var lifetime = options.HandlerLifetime;

            // Assert
            Assert.Equal(ServiceLifetime.Transient, lifetime);
        }

        [Fact]
        public void AddEventBus_WithDefaultLifetime_RegistersHandlersAsTransient()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventBus(typeof(HandlerLifetimeTests).Assembly);

            // Assert
            var requestHandlerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventHandler<LifetimeRequest, LifetimeResponse>));
            Assert.NotNull(requestHandlerDescriptor);
            Assert.Equal(ServiceLifetime.Transient, requestHandlerDescriptor.Lifetime);

            var notificationHandlerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventHandler<LifetimeNotification>));
            Assert.NotNull(notificationHandlerDescriptor);
            Assert.Equal(ServiceLifetime.Transient, notificationHandlerDescriptor.Lifetime);
        }

        [Fact]
        public void AddEventBus_DoesNotRegisterConcreteHandlerTypes()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventBus(typeof(HandlerLifetimeTests).Assembly);

            // Assert
            var requestHandlerConcrete = services.FirstOrDefault(d => d.ServiceType == typeof(LifetimeRequestHandler));
            Assert.Null(requestHandlerConcrete);

            var notificationHandlerConcrete = services.FirstOrDefault(d => d.ServiceType == typeof(LifetimeNotificationHandler));
            Assert.Null(notificationHandlerConcrete);
        }

        [Fact]
        public async Task AddEventBus_WithScopedLifetime_RegistersHandlersAsScoped()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.CreateScopePerEvent = false;
            }, typeof(HandlerLifetimeTests).Assembly);

            using var sp = services.BuildServiceProvider();

            // Act
            Guid scope1Instance1Id;
            Guid scope1Instance2Id;
            Guid scope2InstanceId;

            using (var scope1 = sp.CreateScope())
            {
                var bus1 = scope1.ServiceProvider.GetRequiredService<IEventBus>();
                var resp1 = await bus1.SendAsync(new LifetimeRequest());
                var resp2 = await bus1.SendAsync(new LifetimeRequest());

                scope1Instance1Id = resp1!.InstanceId;
                scope1Instance2Id = resp2!.InstanceId;
            }

            using (var scope2 = sp.CreateScope())
            {
                var bus2 = scope2.ServiceProvider.GetRequiredService<IEventBus>();
                var resp3 = await bus2.SendAsync(new LifetimeRequest());

                scope2InstanceId = resp3!.InstanceId;
            }

            // Assert
            Assert.Equal(scope1Instance1Id, scope1Instance2Id);
            Assert.NotEqual(scope1Instance1Id, scope2InstanceId);
        }

        [Fact]
        public async Task AddEventBus_WithSingletonLifetime_RegistersHandlersAsSingleton()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Singleton;
                options.CreateScopePerEvent = false;
            }, typeof(HandlerLifetimeTests).Assembly);

            using var sp = services.BuildServiceProvider();

            // Act
            Guid scope1InstanceId;
            Guid scope2InstanceId;

            using (var scope1 = sp.CreateScope())
            {
                var bus1 = scope1.ServiceProvider.GetRequiredService<IEventBus>();
                var resp1 = await bus1.SendAsync(new LifetimeRequest());
                scope1InstanceId = resp1!.InstanceId;
            }

            using (var scope2 = sp.CreateScope())
            {
                var bus2 = scope2.ServiceProvider.GetRequiredService<IEventBus>();
                var resp2 = await bus2.SendAsync(new LifetimeRequest());
                scope2InstanceId = resp2!.InstanceId;
            }

            // Assert
            Assert.Equal(scope1InstanceId, scope2InstanceId);
        }
    }
}
