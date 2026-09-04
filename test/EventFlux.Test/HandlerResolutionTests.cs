using EventFlux.Abstractions;
using EventFlux.Extensions;
using EventFlux.Test.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    [Collection("SharedHandlerCounters")]
    public class HandlerResolutionTests
    {
        [Fact]
        public async Task PublishAsync_WithDuplicateHandlerRegistration_InvokesHandlerWithoutThrowing()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(HandlerResolutionTests).Assembly);
            services.AddTransient<IEventHandler<ResolutionEventRequest>, ResolutionEventHandler>();

            using var provider = services.BuildServiceProvider();
            var eventBus = provider.GetRequiredService<IEventBus>();
            ResolutionEventHandler.HandledCount = 0;

            // Act
            await eventBus.PublishAsync(new ResolutionEventRequest { Data = "duplicate" });

            // Assert
            Assert.True(ResolutionEventHandler.HandledCount > 0);
        }

        [Fact]
        public async Task PublishAsync_WithHandlerRegisteredOutsideScannedAssemblies_InvokesHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(IEventBus).Assembly);
            services.AddTransient<IEventHandler<ResolutionEventRequest>, ResolutionEventHandler>();

            using var provider = services.BuildServiceProvider();
            var eventBus = provider.GetRequiredService<IEventBus>();
            ResolutionEventHandler.HandledCount = 0;

            // Act
            await eventBus.PublishAsync(new ResolutionEventRequest { Data = "outside-scan" });

            // Assert
            Assert.Equal(1, ResolutionEventHandler.HandledCount);
        }

        [Fact]
        public async Task PublishAsync_WithCanHandleReturningFalse_SkipsHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(HandlerResolutionTests).Assembly);

            using var provider = services.BuildServiceProvider();
            var eventBus = provider.GetRequiredService<IEventBus>();
            GatedResolutionEventHandler.HandledCount = 0;

            // Act
            await eventBus.PublishAsync(new GatedResolutionEventRequest { Allow = false });

            // Assert
            Assert.Equal(0, GatedResolutionEventHandler.HandledCount);
        }

        [Fact]
        public async Task PublishAsync_WithCanHandleReturningTrue_InvokesHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(HandlerResolutionTests).Assembly);

            using var provider = services.BuildServiceProvider();
            var eventBus = provider.GetRequiredService<IEventBus>();
            GatedResolutionEventHandler.HandledCount = 0;

            // Act
            await eventBus.PublishAsync(new GatedResolutionEventRequest { Allow = true });

            // Assert
            Assert.Equal(1, GatedResolutionEventHandler.HandledCount);
        }
    }
}
