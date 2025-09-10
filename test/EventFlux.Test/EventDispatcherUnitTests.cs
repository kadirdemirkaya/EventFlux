using Castle.Core.Logging;
using EventFlux.Test.Events;
using EventFlux.Test.Unit;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace EventFlux.Test
{
    public class EventDispatcherUnitTests
    {
        private readonly ServiceProvider _serviceProvider;

        public EventDispatcherUnitTests()
        {
            var services = new ServiceCollection();

            services.AddLogging();

            services.AddEventDispatcher()
                    .AddEventLogging()
                    .AddEventTimeout();

            services.AddEventBus(typeof(EventBusUnitTests).Assembly);

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task SendAsync_WithValidRequest_ReturnsExpectedResponse()
        {
            // Arrange
            var dispatcher = _serviceProvider.GetRequiredService<IEventDispatcher>();
            var request = new TestEventRequest { DateTime = DateTime.Now.ToShortTimeString() };

            // Act
            var response = await dispatcher.SendAsync<TestEventResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.ABS);
            Assert.Contains("Processed", response.ABS);
        }

        [Fact]
        public async Task SendAsync_WithExampleRequest_ReturnsCalculatedResponse()
        {
            // Arrange
            var dispatcher = _serviceProvider.GetRequiredService<IEventDispatcher>();
            var request = new ExampleEventRequest { Num = 123 };

            // Act
            var response = await dispatcher.SendAsync<ExampleEventResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Result > 0);
            Assert.Equal(123 * 2, response.Result);
        }

        [Fact]
        public async Task SendAsync_WithTimeout_ThrowsTimeoutException()
        {
            // Arrange
            var dispatcher = _serviceProvider.GetRequiredService<IEventDispatcher>();
            var request = new SlowEventRequest(); // Handler that takes long time
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                dispatcher.SendAsync<SlowEventResponse>(request, cts.Token));
        }

        //[Fact]
        //public async Task PublishAsync_WithValidRequest_ExecutesSuccessfully()
        //{
        //    // Arrange
        //    PublishEventHandler.HandledCount = 0;
        //    var dispatcher = _serviceProvider.GetRequiredService<IEventDispatcher>();
        //    var request = new PublishEventRequest { Data = "dispatcher-test-123" };

        //    // Act
        //    await dispatcher.PublishAsync(request);

        //    // Assert
        //    Assert.Equal(1, PublishEventHandler.HandledCount);

        //    PublishEventHandler.HandledCount = 0;
        //}

        [Fact]
        public async Task PublishAsync_WithCancellationToken_RespectsCancellation()
        {
            // Arrange
            var dispatcher = _serviceProvider.GetRequiredService<IEventDispatcher>();
            var request = new PublishEventRequest { Data = "cancellation-test" };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                dispatcher.PublishAsync(request, cts.Token));
        }

        [Fact]
        public async Task PublishAsync_WithHandlerException_HandlesGracefully()
        {
            // Arrange
            var dispatcher = _serviceProvider.GetRequiredService<IEventDispatcher>();
            var request = new ExceptionThrowingEventRequest();

            // Act & Assert (Should handle exception gracefully or rethrow based on implementation)
            var exception = await Record.ExceptionAsync(() => dispatcher.PublishAsync(request));

            // Option 2: Should rethrow
            Assert.NotNull(exception);
            Assert.IsType<InvalidOperationException>(exception);
        }

        [Fact]
        public async Task PublishAsync_WithMultipleHandlers_CallsAllHandlersSequentially()
        {
            // Arrange
            var dispatcher = _serviceProvider.GetRequiredService<IEventDispatcher>();
            var request = new MultiHandlerEventRequest { Message = "dispatcher-multi-test" };

            // Reset handler counters
            MultiHandlerEventHandler1.HandledCount = 0;
            MultiHandlerEventHandler2.HandledCount = 0;

            // Act
            await dispatcher.PublishAsync(request);

            // Assert
            Assert.Equal(1, MultiHandlerEventHandler1.HandledCount);
            Assert.Equal(1, MultiHandlerEventHandler2.HandledCount);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _serviceProvider?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
