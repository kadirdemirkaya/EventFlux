using EventFlux.Test.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace EventFlux.Test.Unit
{
    public class EventBusUnitTests
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly Mock<ILogger> _mockLogger;

        public EventBusUnitTests()
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
            var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
            var request = new TestEventRequest { DateTime = DateTime.Now.ToShortDateString() };

            // Act
            var response = await eventBus.SendAsync<TestEventResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.ABS);
            Assert.Contains("Processed", response.ABS);
        }

        [Fact]
        public async Task SendAsync_WithExampleRequest_ReturnsCalculatedResponse()
        {
            // Arrange
            var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
            var request = new ExampleEventRequest { Num = 123 };

            // Act
            var response = await eventBus.SendAsync<ExampleEventResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Result > 0);
            Assert.Equal(123 * 2, response.Result); // Assuming handler doubles the number
        }

        [Fact]
        public async Task SendAsync_WithNullRequest_ThrowsArgumentNullException()
        {
            // Arrange
            var eventBus = _serviceProvider.GetRequiredService<IEventBus>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                eventBus.SendAsync<TestEventResponse>(null));
        }

        [Fact]
        public async Task SendAsync_WithNoHandler_ThrowsInvalidOperationException()
        {
            // Arrange
            var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
            var request = new UnhandledEventRequest();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                eventBus.SendAsync<UnhandledEventResponse>(request));
        }


        [Fact]
        public async Task PublishAsync_WithValidRequest_ExecutesSuccessfully()
        {
            // Arrange
            PublishEventHandler.HandledCount = 0;
            var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
            var request = new PublishEventRequest { Data = "test-data-123" };

            // Act
            await eventBus.PublishAsync(request);

            // Assert
            Assert.Equal(1, PublishEventHandler.HandledCount);
            PublishEventHandler.HandledCount = 0;
        }

        [Fact]
        public async Task PublishAsync_WithMultipleHandlers_CallsAllHandlers()
        {
            // Arrange
            var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
            var request = new MultiHandlerEventRequest { Message = "multi-handler-test" };

            // Reset handler counters
            MultiHandlerEventHandler1.HandledCount = 0;
            MultiHandlerEventHandler2.HandledCount = 0;

            // Act
            await eventBus.PublishAsync(request);

            // Assert
            Assert.Equal(1, MultiHandlerEventHandler1.HandledCount);
            Assert.Equal(1, MultiHandlerEventHandler2.HandledCount);
        }

        [Fact]
        public async Task PublishAsync_WithNullRequest_ThrowsArgumentNullException()
        {
            // Arrange
            var eventBus = _serviceProvider.GetRequiredService<IEventBus>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                eventBus.PublishAsync(null));
        }

        [Fact]
        public async Task PublishAsync_WithNoHandlers_CompletesWithoutError()
        {
            // Arrange
            var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
            var request = new NoHandlerEventRequest();

            // Act & Assert (Should not throw)
            await eventBus.PublishAsync(request);
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