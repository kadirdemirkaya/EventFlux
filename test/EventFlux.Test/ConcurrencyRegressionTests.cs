using EventFlux.Abstractions;
using EventFlux.Extensions;
using EventFlux.Test.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    public class ConcurrencyRegressionTests : IDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        private readonly ServiceProvider _serviceProvider;

        public ConcurrencyRegressionTests()
        {
            var services = new ServiceCollection();

            services.AddLogging();

            services.AddEventDispatcher();

            services.AddEventBus(typeof(ConcurrencyRegressionTests).Assembly);

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task PublishAsync_WithNestedPublish_DoesNotDeadlock()
        {
            // Arrange
            var fanout = Environment.ProcessorCount * 2;
            NestedInnerEventHandler.HandledCount = 0;

            // Act
            var work = Task.WhenAll(Enumerable.Range(0, fanout).Select(_ =>
                _serviceProvider.GetRequiredService<IEventDispatcher>()
                                .PublishAsync(new NestedOuterEventRequest())));

            var completed = await Task.WhenAny(work, Task.Delay(Timeout));

            // Assert
            Assert.True(
                completed == work,
                $"Nested PublishAsync deadlocked: {fanout} concurrent events did not " +
                $"complete within {Timeout.TotalSeconds}s (ProcessorCount = {Environment.ProcessorCount}).");

            await work; // surface any handler exception
            Assert.Equal(fanout, NestedInnerEventHandler.HandledCount);
        }

        [Fact]
        public async Task PublishAsync_AcrossIndependentContainers_DoesNotDeadlock()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventDispatcher();
            services.AddEventBus(typeof(ConcurrencyRegressionTests).Assembly);
            using var otherProvider = services.BuildServiceProvider();

            var fanout = Environment.ProcessorCount;

            // Act
            var work = Task.WhenAll(
                Enumerable.Range(0, fanout).Select(_ =>
                    _serviceProvider.GetRequiredService<IEventDispatcher>()
                                    .PublishAsync(new NestedOuterEventRequest()))
                .Concat(Enumerable.Range(0, fanout).Select(_ =>
                    otherProvider.GetRequiredService<IEventDispatcher>()
                                 .PublishAsync(new NestedOuterEventRequest()))));

            var completed = await Task.WhenAny(work, Task.Delay(Timeout));

            // Assert
            Assert.True(
                completed == work,
                $"Dispatch across two independent containers deadlocked within {Timeout.TotalSeconds}s.");

            await work;
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
