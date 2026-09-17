using System.Diagnostics;
using EventFlux.Abstractions;
using EventFlux.Behaviors;
using EventFlux.Delegates;
using EventFlux.Options;
using EventFlux.Test.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventFlux.Test
{
    public class TimeoutOptionsTests
    {
        private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan GenerousUpperBound = TimeSpan.FromSeconds(4);

        private static ServiceProvider BuildProvider(Action<EventFluxOptions>? configureOptions)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus<TimeoutOptionsTests>();
            services.AddEventDispatcher(configureOptions);
            services.AddEventTimeout();
            return services.BuildServiceProvider();
        }

        [Fact]
        public void Timeout_DefaultsToNull()
        {
            // Assert
            Assert.Null(new EventFluxOptions().Timeout);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Timeout_NonPositiveValue_ThrowsArgumentOutOfRangeException(int milliseconds)
        {
            // Arrange
            var options = new EventFluxOptions();

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Timeout = TimeSpan.FromMilliseconds(milliseconds));
            Assert.Null(options.Timeout);
        }

        [Fact]
        public void Timeout_ValueAboveCancellationTokenSourceLimit_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var options = new EventFluxOptions();

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Timeout = TimeSpan.FromMilliseconds((double)int.MaxValue + 1));
        }

        [Fact]
        public void Timeout_AcceptsPositiveInfiniteAndNull()
        {
            // Arrange
            var options = new EventFluxOptions();

            // Act & Assert
            options.Timeout = TimeSpan.FromSeconds(5);
            Assert.Equal(TimeSpan.FromSeconds(5), options.Timeout);

            options.Timeout = Timeout.InfiniteTimeSpan;
            Assert.Equal(Timeout.InfiniteTimeSpan, options.Timeout);

            options.Timeout = null;
            Assert.Null(options.Timeout);
        }

        [Fact]
        public async Task OptionsConstructor_TimeoutSet_OverridesFallbackSeconds()
        {
            // Arrange
            var behavior = new TimeoutBehavior<UncooperativeSlowUnitCommand, EventFlux.Abstractions.Unit>(
                NullLogger<TimeoutBehavior<UncooperativeSlowUnitCommand, EventFlux.Abstractions.Unit>>.Instance,
                new EventFluxOptions { Timeout = ShortTimeout },
                timeoutSeconds: 30);
            EventHandlerDelegate<EventFlux.Abstractions.Unit> next = async _ =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);
                return EventFlux.Abstractions.Unit.Value;
            };
            var stopwatch = Stopwatch.StartNew();

            // Act
            var ex = await Assert.ThrowsAsync<OperationCanceledException>(
                () => behavior.Handle(new UncooperativeSlowUnitCommand(), next, CancellationToken.None));

            // Assert
            Assert.Equal("Timeout occurred", ex.Message);
            Assert.True(stopwatch.Elapsed < GenerousUpperBound, $"Timed out after {stopwatch.Elapsed}");
        }

        [Fact]
        public async Task OptionsConstructor_TimeoutNotSet_UsesFallbackSeconds()
        {
            // Arrange
            var behavior = new TimeoutBehavior<UncooperativeSlowNotification>(
                NullLogger<TimeoutBehavior<UncooperativeSlowNotification>>.Instance,
                new EventFluxOptions(),
                timeoutSeconds: 0.2);
            EventHandlerDelegate next = _ => Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<OperationCanceledException>(
                () => behavior.Handle(new UncooperativeSlowNotification(), next, CancellationToken.None));
            Assert.Equal("Timeout occurred", ex.Message);
        }

        [Fact]
        public async Task OptionsConstructor_NullOptions_UsesFallbackSeconds()
        {
            // Arrange
            var behavior = new TimeoutBehavior<UncooperativeSlowNotification>(
                NullLogger<TimeoutBehavior<UncooperativeSlowNotification>>.Instance,
                null,
                timeoutSeconds: 0.2);
            EventHandlerDelegate next = _ => Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => behavior.Handle(new UncooperativeSlowNotification(), next, CancellationToken.None));
        }

        [Fact]
        public async Task Dispatcher_SendAsync_WithConfiguredTimeout_TimesOutAtConfiguredDuration()
        {
            // Arrange
            using var provider = BuildProvider(options => options.Timeout = ShortTimeout);
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();
            var stopwatch = Stopwatch.StartNew();

            // Act
            var ex = await Assert.ThrowsAsync<OperationCanceledException>(
                () => dispatcher.SendAsync(new UncooperativeSlowUnitCommand()));

            // Assert
            Assert.Equal("Timeout occurred", ex.Message);
            Assert.True(stopwatch.Elapsed < GenerousUpperBound, $"Timed out after {stopwatch.Elapsed}");
        }

        [Fact]
        public async Task Dispatcher_PublishAsync_WithConfiguredTimeout_TimesOutAtConfiguredDuration()
        {
            // Arrange
            using var provider = BuildProvider(options => options.Timeout = ShortTimeout);
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();
            var stopwatch = Stopwatch.StartNew();

            // Act
            var ex = await Assert.ThrowsAsync<OperationCanceledException>(
                () => dispatcher.PublishAsync(new UncooperativeSlowNotification()));

            // Assert
            Assert.Equal("Timeout occurred", ex.Message);
            Assert.True(stopwatch.Elapsed < GenerousUpperBound, $"Timed out after {stopwatch.Elapsed}");
        }

        [Fact]
        public async Task Dispatcher_WithoutConfiguredTimeout_KeepsDefaultAndCompletesWork()
        {
            // Arrange
            using var provider = BuildProvider(null);
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();

            // Act
            await dispatcher.SendAsync(new UncooperativeSlowUnitCommand { Delay = TimeSpan.FromMilliseconds(400) });
            await dispatcher.PublishAsync(new UncooperativeSlowNotification { Delay = TimeSpan.FromMilliseconds(400) });

            // Assert
            var behaviors = provider.GetServices<IEventCustomPipeline<UncooperativeSlowUnitCommand, EventFlux.Abstractions.Unit>>();
            Assert.IsType<TimeoutBehavior<UncooperativeSlowUnitCommand, EventFlux.Abstractions.Unit>>(Assert.Single(behaviors));
        }

        [Fact]
        public void AddEventTimeout_WithoutRegisteredOptions_StillResolvesBehaviors()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventTimeout();
            using var provider = services.BuildServiceProvider();

            // Act
            var requestBehaviors = provider.GetServices<IEventCustomPipeline<SendEventRequest, SendEventResponse>>();
            var notificationBehaviors = provider.GetServices<IEventCustomPipeline<PublishEventRequest>>();

            // Assert
            Assert.IsType<TimeoutBehavior<SendEventRequest, SendEventResponse>>(Assert.Single(requestBehaviors));
            Assert.IsType<TimeoutBehavior<PublishEventRequest>>(Assert.Single(notificationBehaviors));
        }
    }
}
