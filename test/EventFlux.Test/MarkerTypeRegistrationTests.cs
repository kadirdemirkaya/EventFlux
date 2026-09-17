using System.Diagnostics.CodeAnalysis;
using EventFlux.Abstractions;
using EventFlux.Options;
using EventFlux.Services;
using EventFlux.Test.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    public class MarkerTypeRegistrationTests
    {
        [Fact]
        public void AddEventBusMarker_RegistersSameServicesAsAssemblyOverload()
        {
            // Arrange
            var byAssembly = new ServiceCollection();
            var byMarker = new ServiceCollection();

            // Act
            byAssembly.AddEventBus(typeof(MarkerTypeRegistrationTests).Assembly);
            byMarker.AddEventBus<MarkerTypeRegistrationTests>();

            // Assert
            Assert.Equal(
                byAssembly.Select(d => (d.ServiceType, d.ImplementationType, d.Lifetime)),
                byMarker.Select(d => (d.ServiceType, d.ImplementationType, d.Lifetime)));
        }

        [Fact]
        public async Task AddEventBusMarker_ScansAssemblyDeclaringMarker()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus<SendEventHandler>();
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

            // Act
            var response = await bus.SendAsync(new SendEventRequest { Data = "marker" });

            // Assert
            Assert.Equal("marker", response!.Data);
            Assert.Contains(typeof(SendEventHandler), provider.GetRequiredService<EventService>().GetHandlersForEvent<SendEventRequest>());
        }

        [Fact]
        public void AddEventBusMarker_WithOptions_AppliesOptions()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventBus<MarkerTypeRegistrationTests>(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.PublishStrategy = PublishStrategy.Sequential;
            });

            // Assert
            var options = Assert.IsType<EventFluxOptions>(services.Single(d => d.ServiceType == typeof(EventFluxOptions)).ImplementationInstance);
            Assert.Equal(PublishStrategy.Sequential, options.PublishStrategy);
            Assert.Equal(ServiceLifetime.Scoped, services.Single(d => d.ServiceType == typeof(IEventHandler<SendEventRequest, SendEventResponse>)).Lifetime);
        }

        [Fact]
        public void AddEventBusMarker_WithNullOptions_UsesDefaults()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventBus<MarkerTypeRegistrationTests>(null);

            // Assert
            Assert.Equal(ServiceLifetime.Transient, services.Single(d => d.ServiceType == typeof(IEventHandler<SendEventRequest, SendEventResponse>)).Lifetime);
        }

        [Fact]
        public void AddEventBusMarker_NullServices_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceCollection services = null!;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => services.AddEventBus<MarkerTypeRegistrationTests>());
        }

        [Theory]
        [InlineData(nameof(EventFluxServiceCollectionExtensions.AddEventBus))]
        [InlineData(nameof(EventFluxServiceCollectionExtensions.AddEventDispatcher))]
        public void RegistrationEntryPoints_InDependencyInjectionNamespace_WarnAotAndTrimmingConsumers(string methodName)
        {
            // Arrange
            var overloads = typeof(EventFluxServiceCollectionExtensions).GetMethods()
                .Where(m => m.Name == methodName)
                .ToList();

            // Assert
            Assert.NotEmpty(overloads);
            Assert.All(overloads, m =>
            {
                Assert.True(m.IsDefined(typeof(RequiresDynamicCodeAttribute), false), $"{m} lacks [RequiresDynamicCode]");
                Assert.True(m.IsDefined(typeof(RequiresUnreferencedCodeAttribute), false), $"{m} lacks [RequiresUnreferencedCode]");
            });
        }
    }
}
