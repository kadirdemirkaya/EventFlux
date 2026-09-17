using System.Reflection;
using EventFlux.Abstractions;
using EventFlux.Extensions;
using EventFlux.Options;
using EventFlux.Test.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    public class DependencyInjectionNamespaceTests
    {
        [Fact]
        public void RegistrationExtensions_LiveInDependencyInjectionNamespace()
        {
            // Assert
            Assert.Equal("Microsoft.Extensions.DependencyInjection", typeof(EventFluxServiceCollectionExtensions).Namespace);
        }

        [Theory]
        [InlineData(nameof(EventBusServiceExtension.AddEventBus), new[] { typeof(IServiceCollection), typeof(Assembly[]) })]
        [InlineData(nameof(EventBusServiceExtension.AddEventBus), new[] { typeof(IServiceCollection), typeof(Action<EventFluxOptions>), typeof(Assembly[]) })]
        [InlineData(nameof(EventBusServiceExtension.AddEventDispatcher), new[] { typeof(IServiceCollection) })]
        [InlineData(nameof(EventBusServiceExtension.AddEventDispatcher), new[] { typeof(IServiceCollection), typeof(Action<EventFluxOptions>) })]
        [InlineData(nameof(EventBusServiceExtension.AddEventLogging), new[] { typeof(IServiceCollection) })]
        [InlineData(nameof(EventBusServiceExtension.AddEventTimeout), new[] { typeof(IServiceCollection) })]
        public void LegacyExtensionClass_KeepsOriginalSignatures(string methodName, Type[] parameterTypes)
        {
            // Act
            var method = typeof(EventBusServiceExtension).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, parameterTypes);

            // Assert
            Assert.Equal("EventFlux.Extensions", typeof(EventBusServiceExtension).Namespace);
            Assert.NotNull(method);
            Assert.False(method!.IsGenericMethod);
            Assert.Equal(typeof(IServiceCollection), method.ReturnType);
        }

        [Fact]
        public void LegacyStaticCalls_RegisterEventFluxServices()
        {
            // Arrange
            IServiceCollection services = new ServiceCollection();

            // Act
            EventBusServiceExtension.AddEventBus(services, typeof(DependencyInjectionNamespaceTests).Assembly);
            EventBusServiceExtension.AddEventBus(services, options => options.PublishStrategy = PublishStrategy.Sequential, typeof(DependencyInjectionNamespaceTests).Assembly);
            EventBusServiceExtension.AddEventDispatcher(services);
            EventBusServiceExtension.AddEventDispatcher(services, options => options.PublishStrategy = PublishStrategy.Sequential);
            EventBusServiceExtension.AddEventLogging(services);
            EventBusServiceExtension.AddEventTimeout(services);

            // Assert
            Assert.Single(services, d => d.ServiceType == typeof(IEventBus));
            Assert.Single(services, d => d.ServiceType == typeof(IEventDispatcher));
            Assert.Single(services, d => d.ServiceType == typeof(IEventHandler<SendEventRequest, SendEventResponse>));
            Assert.Equal(4, services.Count(d => d.ServiceType == typeof(IEventCustomPipeline<>) || d.ServiceType == typeof(IEventCustomPipeline<,>)));
        }

        [Fact]
        public void BothNamespacesImported_InterfaceTypedCollection_BindsWithoutAmbiguity()
        {
            // Arrange
            IServiceCollection services = new ServiceCollection();

            // Act
            services
                .AddEventBus(typeof(DependencyInjectionNamespaceTests).Assembly)
                .AddEventBus(options => options.PublishStrategy = PublishStrategy.Sequential, typeof(DependencyInjectionNamespaceTests).Assembly)
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
        public async Task BothNamespacesImported_ConcreteCollection_RegistersWorkingEventBus()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            // Act
            services
                .AddEventBus(typeof(DependencyInjectionNamespaceTests).Assembly)
                .AddEventDispatcher();
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var response = await scope.ServiceProvider.GetRequiredService<IEventBus>().SendAsync(new SendEventRequest { Data = "both" });

            // Assert
            Assert.Equal("both", response!.Data);
        }
    }
}
