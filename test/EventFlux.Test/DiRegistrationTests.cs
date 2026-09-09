using EventFlux.Abstractions;
using EventFlux.Behaviors;
using EventFlux.Extensions;
using EventFlux.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace EventFlux.Test
{
    public class DiRegistrationTests
    {
        [Fact]
        public void AddEventBus_WithNoAssemblies_ThrowsArgumentException()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => services.AddEventBus());
        }

        [Fact]
        public void AddEventBus_WithEmptyAssemblyArray_ThrowsArgumentException()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => services.AddEventBus(Array.Empty<Assembly>()));
        }

        [Fact]
        public void AddEventBus_WithNullAssemblies_ThrowsArgumentException()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => services.AddEventBus((Assembly[])null!));
        }

        [Fact]
        public void AddEventBus_WithNullServices_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceCollection services = null!;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => services.AddEventBus(typeof(DiRegistrationTests).Assembly));
        }

        [Fact]
        public void AddEventLogging_CalledMultipleTimes_RegistersBehaviorsOnce()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventLogging();
            services.AddEventLogging();

            // Assert
            var singleCount = services.Count(d => d.ServiceType == typeof(IEventCustomPipeline<>) && d.ImplementationType == typeof(LoggingBehavior<>));
            var doubleCount = services.Count(d => d.ServiceType == typeof(IEventCustomPipeline<,>) && d.ImplementationType == typeof(LoggingBehavior<,>));

            Assert.Equal(1, singleCount);
            Assert.Equal(1, doubleCount);
        }

        [Fact]
        public void AddEventTimeout_CalledMultipleTimes_RegistersBehaviorsOnce()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventTimeout();
            services.AddEventTimeout();

            // Assert
            var singleCount = services.Count(d => d.ServiceType == typeof(IEventCustomPipeline<>) && d.ImplementationType == typeof(TimeoutBehavior<>));
            var doubleCount = services.Count(d => d.ServiceType == typeof(IEventCustomPipeline<,>) && d.ImplementationType == typeof(TimeoutBehavior<,>));

            Assert.Equal(1, singleCount);
            Assert.Equal(1, doubleCount);
        }

        [Fact]
        public void AddEventDispatcher_CalledMultipleTimes_RegistersOnce()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventDispatcher();
            services.AddEventDispatcher();

            // Assert
            var count = services.Count(d => d.ServiceType == typeof(IEventDispatcher));
            Assert.Equal(1, count);
        }

        [Fact]
        public void AddEventBus_RegistersEventStackServiceAsSingleton()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventBus(typeof(DiRegistrationTests).Assembly);

            // Assert
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(EventStackService));
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }
    }
}
