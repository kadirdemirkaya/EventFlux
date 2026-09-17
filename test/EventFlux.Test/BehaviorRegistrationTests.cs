using EventFlux.Abstractions;
using EventFlux.Behaviors;
using EventFlux.Test.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    public class BehaviorRegistrationTests
    {
        [Fact]
        public void AddEventBehavior_ClosedBehavior_RegistersImplementedPipelineInterface()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventBehavior<RecordUnitCommandBehavior>();

            // Assert
            var descriptor = Assert.Single(services);
            Assert.Equal(typeof(IEventCustomPipeline<RecordUnitCommand, EventFlux.Abstractions.Unit>), descriptor.ServiceType);
            Assert.Equal(typeof(RecordUnitCommandBehavior), descriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        }

        [Fact]
        public void AddEventBehavior_BehaviorImplementingBothShapes_RegistersEveryPipelineInterface()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventBehavior<DualShapeBehavior>();

            // Assert
            Assert.Equal(2, services.Count);
            Assert.Contains(services, d => d.ServiceType == typeof(IEventCustomPipeline<RecordUnitCommand, EventFlux.Abstractions.Unit>));
            Assert.Contains(services, d => d.ServiceType == typeof(IEventCustomPipeline<TracedNotification>));
        }

        [Fact]
        public void AddEventBehavior_CalledTwice_RegistersOnce()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventBehavior<RecordUnitCommandBehavior>();
            services.AddEventBehavior<RecordUnitCommandBehavior>();

            // Assert
            Assert.Single(services);
        }

        [Fact]
        public void AddEventBehavior_WithLifetime_UsesRequestedLifetime()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventBehavior<RecordUnitCommandBehavior>(ServiceLifetime.Scoped);

            // Assert
            Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services).Lifetime);
        }

        [Fact]
        public void AddEventBehavior_TypeWithoutPipelineInterface_ThrowsArgumentException()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => services.AddEventBehavior<NotAClosedBehavior>());
            Assert.Contains(nameof(NotAClosedBehavior), ex.Message);
            Assert.Empty(services);
        }

        [Fact]
        public void AddEventBehavior_AbstractBehavior_ThrowsArgumentException()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => services.AddEventBehavior<AbstractClosedBehavior>());
            Assert.Empty(services);
        }

        [Fact]
        public void AddEventBehavior_NullServices_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceCollection services = null!;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => services.AddEventBehavior<RecordUnitCommandBehavior>());
        }

        [Fact]
        public async Task AddEventBehavior_DualShapeBehavior_RunsForRequestAndNotification()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus<BehaviorRegistrationTests>();
            services.AddEventDispatcher();
            services.AddEventBehavior<DualShapeBehavior>();
            using var provider = services.BuildServiceProvider();
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();
            var command = new RecordUnitCommand();
            var notification = new TracedNotification();

            // Act
            await dispatcher.SendAsync(command);
            await dispatcher.PublishAsync(notification);

            // Assert
            Assert.Equal(new[] { "dual-behavior", "handler" }, command.Trace);
            Assert.Equal(new[] { "dual-behavior", "handler" }, notification.Trace);
        }

        [Fact]
        public async Task ManuallyRegisteredBehaviorWithSeveralHandleMethods_RunsWithoutAmbiguity()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus<BehaviorRegistrationTests>();
            services.AddEventDispatcher();
            services.AddTransient<IEventCustomPipeline<RecordUnitCommand, EventFlux.Abstractions.Unit>, DualShapeBehavior>();
            services.AddTransient<IEventCustomPipeline<TracedNotification>, DualShapeBehavior>();
            using var provider = services.BuildServiceProvider();
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();
            var command = new RecordUnitCommand();
            var notification = new TracedNotification();

            // Act
            await dispatcher.SendAsync(command);
            await dispatcher.PublishAsync(notification);

            // Assert
            Assert.Equal(new[] { "dual-behavior", "handler" }, command.Trace);
            Assert.Equal(new[] { "dual-behavior", "handler" }, notification.Trace);
        }

        [Fact]
        public async Task ExplicitlyImplementedBehavior_RunsForRequestAndNotification()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus<BehaviorRegistrationTests>();
            services.AddEventDispatcher();
            services.AddEventBehavior<ExplicitlyImplementedBehavior>();
            using var provider = services.BuildServiceProvider();
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();
            var command = new RecordUnitCommand();
            var notification = new TracedNotification();

            // Act
            await dispatcher.SendAsync(command);
            await dispatcher.PublishAsync(notification);

            // Assert
            Assert.Equal(new[] { "explicit-behavior", "handler" }, command.Trace);
            Assert.Equal(new[] { "explicit-behavior", "handler" }, notification.Trace);
        }

        [Theory]
        [InlineData(typeof(TracingRequestBehavior<,>), typeof(IEventCustomPipeline<,>))]
        [InlineData(typeof(TracingNotificationBehavior<>), typeof(IEventCustomPipeline<>))]
        public void AddEventOpenBehavior_MatchingOpenBehavior_RegistersOpenPipeline(Type openBehaviorType, Type expectedServiceType)
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventOpenBehavior(openBehaviorType);

            // Assert
            var descriptor = Assert.Single(services);
            Assert.Equal(expectedServiceType, descriptor.ServiceType);
            Assert.Equal(openBehaviorType, descriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        }

        [Fact]
        public void AddEventOpenBehavior_BuiltInBehavior_MatchesAddEventTimeoutRegistration()
        {
            // Arrange
            var expected = new ServiceCollection();
            var actual = new ServiceCollection();

            // Act
            expected.AddEventTimeout();
            actual.AddEventOpenBehavior(typeof(TimeoutBehavior<>));
            actual.AddEventOpenBehavior(typeof(TimeoutBehavior<,>));

            // Assert
            Assert.Equal(
                expected.Select(d => (d.ServiceType, d.ImplementationType, d.Lifetime)),
                actual.Select(d => (d.ServiceType, d.ImplementationType, d.Lifetime)));
        }

        [Fact]
        public void AddEventOpenBehavior_CalledTwice_RegistersOnce()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventOpenBehavior(typeof(TracingRequestBehavior<,>));
            services.AddEventOpenBehavior(typeof(TracingRequestBehavior<,>));

            // Assert
            Assert.Single(services);
        }

        [Fact]
        public void AddEventOpenBehavior_WithLifetime_UsesRequestedLifetime()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddEventOpenBehavior(typeof(TracingRequestBehavior<,>), ServiceLifetime.Singleton);

            // Assert
            Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services).Lifetime);
        }

        [Fact]
        public async Task AddEventOpenBehavior_RegisteredBehaviors_RunAroundHandlers()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus<BehaviorRegistrationTests>();
            services.AddEventDispatcher();
            services.AddEventOpenBehavior(typeof(TracingRequestBehavior<,>));
            services.AddEventOpenBehavior(typeof(TracingNotificationBehavior<>));
            using var provider = services.BuildServiceProvider();
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();
            var command = new RecordUnitCommand();
            var notification = new TracedNotification();

            // Act
            await dispatcher.SendAsync(command);
            await dispatcher.PublishAsync(notification);

            // Assert
            Assert.Equal(new[] { "open-behavior", "handler" }, command.Trace);
            Assert.Equal(new[] { "open-behavior", "handler" }, notification.Trace);
        }

        [Theory]
        [InlineData(typeof(PartiallyClosedBehavior<>))]
        [InlineData(typeof(SwappedParametersBehavior<,>))]
        public void AddEventOpenBehavior_TypeParametersNotMappingOntoPipeline_ThrowsArgumentException(Type openBehaviorType)
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => services.AddEventOpenBehavior(openBehaviorType));
            Assert.Equal("openBehaviorType", ex.ParamName);
            Assert.Contains("do not map", ex.Message);
            Assert.Empty(services);
        }

        [Theory]
        [InlineData(typeof(RecordUnitCommandBehavior))]
        [InlineData(typeof(TracingRequestBehavior<RecordUnitCommand, EventFlux.Abstractions.Unit>))]
        public void AddEventOpenBehavior_ClosedType_ThrowsArgumentException(Type closedBehaviorType)
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => services.AddEventOpenBehavior(closedBehaviorType));
            Assert.Contains("AddEventBehavior", ex.Message);
            Assert.Empty(services);
        }

        [Fact]
        public void AddEventOpenBehavior_TypeWithoutPipelineInterface_ThrowsArgumentException()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => services.AddEventOpenBehavior(typeof(NotABehavior<>)));
            Assert.Empty(services);
        }

        [Fact]
        public void AddEventOpenBehavior_AbstractBehavior_ThrowsArgumentException()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => services.AddEventOpenBehavior(typeof(AbstractRequestBehavior<,>)));
            Assert.Empty(services);
        }

        [Fact]
        public void AddEventOpenBehavior_NullType_ThrowsArgumentNullException()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => services.AddEventOpenBehavior(null!));
        }

        [Fact]
        public void AddEventOpenBehavior_NullServices_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceCollection services = null!;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => services.AddEventOpenBehavior(typeof(TracingRequestBehavior<,>)));
        }
    }
}
