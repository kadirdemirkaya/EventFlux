using Castle.Core.Logging;
using EventFlux.Test.Events;
using EventFlux.Test.Unit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventFlux.Test
{
    public class EventBusUsageTests
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly Mock<ILogger> _mockLogger;

        public EventBusUsageTests()
        {
            var services = new ServiceCollection();

            services.AddLogging();

            services.AddEventDispatcher()
                    .AddEventLogging()
                    .AddEventTimeout();

            services.AddEventBus(typeof(EventFlux.Test.Events.SendEventResponse).Assembly);

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task SendAsync_WithValidRequest_NoneReturns()
        {
            // Arrange
            var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
            var request = new SendEventRequest { Data = "data-test" };

            // Act
            SendEventResponse response = await eventBus.SendAsync(request);

            // Assert
            Assert.NotNull(response);
        }

        [Fact]
        public async Task PublishAsync_WithValidRequest_InvokesHandler()
        {
            // Arrange
            var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
            var request = new PublishEventRequest { Data = "publish-test" };

            // Reset handler counter
            PublishEventHandler.HandledCount = 0;

            // Act
            await eventBus.PublishAsync(request);

            // Assert
            Assert.Equal(1, PublishEventHandler.HandledCount);
        }

        [Fact]
        public async Task PublishAsync_WithNotValidRequest_CanHandle()
        {
            // Arrange
            var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
            var request = new CanHandleEventRequest { Data = "canhandle-test" };

            // Act
            var exception = await Record.ExceptionAsync(() => eventBus.PublishAsync(request));

            // Assert
            Assert.Null(exception);
        }
    }
}
