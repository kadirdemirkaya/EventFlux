using EventFlux.Abstractions;
using EventFlux.Extensions;
using EventFlux.Options;

namespace EventFlux.Test
{
    public class LegacyExtensionsNamespaceOnlyTests
    {
        [Fact]
        public void OnlyLegacyNamespaceImported_ExtensionSyntax_RegistersEventFluxServices()
        {
            // Arrange
            Microsoft.Extensions.DependencyInjection.IServiceCollection services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

            // Act
            services
                .AddEventBus(typeof(LegacyExtensionsNamespaceOnlyTests).Assembly)
                .AddEventBus(options => options.PublishStrategy = PublishStrategy.Sequential, typeof(LegacyExtensionsNamespaceOnlyTests).Assembly)
                .AddEventDispatcher()
                .AddEventDispatcher(options => options.PublishStrategy = PublishStrategy.Sequential)
                .AddEventLogging()
                .AddEventTimeout();

            // Assert
            Assert.Single(services, d => d.ServiceType == typeof(IEventBus));
            Assert.Single(services, d => d.ServiceType == typeof(IEventDispatcher));
            Assert.Equal(4, services.Count(d => d.ServiceType == typeof(IEventCustomPipeline<>) || d.ServiceType == typeof(IEventCustomPipeline<,>)));
        }

        [Fact]
        public void OnlyLegacyNamespaceImported_ConcreteCollection_RegistersEventFluxServices()
        {
            // Arrange
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

            // Act
            services.AddEventBus(typeof(LegacyExtensionsNamespaceOnlyTests).Assembly);
            services.AddEventDispatcher();

            // Assert
            Assert.Single(services, d => d.ServiceType == typeof(IEventBus));
            Assert.Single(services, d => d.ServiceType == typeof(IEventDispatcher));
        }
    }
}
