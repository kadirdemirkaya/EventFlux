using System;
using System.Threading.Tasks;
using EventFlux.Abstractions;
using EventFlux.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventFlux.Test
{
    public class ConditionalRequest : IEventRequest<ConditionalResponse>
    {
        public bool Allow { get; set; }

        public ConditionalRequest(bool allow)
        {
            Allow = allow;
        }
    }

    public class ConditionalResponse : IEventResponse
    {
        public string Status { get; set; } = string.Empty;

        public ConditionalResponse()
        {
        }

        public ConditionalResponse(string status)
        {
            Status = status;
        }
    }

    public class ConditionalRequestHandler : IEventHandler<ConditionalRequest, ConditionalResponse>
    {
        public bool CanHandle(ConditionalRequest @event) => @event.Allow;

        public Task<ConditionalResponse> Handle(ConditionalRequest @event)
        {
            return Task.FromResult(new ConditionalResponse("Handled"));
        }
    }

    public class CanHandleSendConsistencyTests
    {
        [Fact]
        public async Task EventBus_SendAsync_WhenCanHandleReturnsFalse_SkipsHandlerAndReturnsNull()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(CanHandleSendConsistencyTests).Assembly);
            using var sp = services.BuildServiceProvider();
            var eventBus = sp.GetRequiredService<IEventBus>();

            // Act
            var response = await eventBus.SendAsync(new ConditionalRequest(allow: false));

            // Assert
            Assert.Null(response);
        }

        [Fact]
        public async Task EventBus_SendAsync_WhenCanHandleReturnsTrue_ExecutesHandlerAndReturnsResponse()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(CanHandleSendConsistencyTests).Assembly);
            using var sp = services.BuildServiceProvider();
            var eventBus = sp.GetRequiredService<IEventBus>();

            // Act
            var response = await eventBus.SendAsync(new ConditionalRequest(allow: true));

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Handled", response.Status);
        }

        [Fact]
        public async Task EventDispatcher_SendAsync_WhenCanHandleReturnsFalse_SkipsHandlerAndReturnsNull()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(CanHandleSendConsistencyTests).Assembly);
            services.AddEventDispatcher();
            using var sp = services.BuildServiceProvider();
            var dispatcher = sp.GetRequiredService<IEventDispatcher>();

            // Act
            var response = await dispatcher.SendAsync(new ConditionalRequest(allow: false));

            // Assert
            Assert.Null(response);
        }

        [Fact]
        public async Task EventDispatcher_SendAsync_WhenCanHandleReturnsTrue_ExecutesHandlerAndReturnsResponse()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(CanHandleSendConsistencyTests).Assembly);
            services.AddEventDispatcher();
            using var sp = services.BuildServiceProvider();
            var dispatcher = sp.GetRequiredService<IEventDispatcher>();

            // Act
            var response = await dispatcher.SendAsync(new ConditionalRequest(allow: true));

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Handled", response.Status);
        }

        [Fact]
        public async Task SendAsync_BothBusAndDispatcher_ProduceIdenticalOutcome()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(CanHandleSendConsistencyTests).Assembly);
            services.AddEventDispatcher();
            using var sp = services.BuildServiceProvider();
            var eventBus = sp.GetRequiredService<IEventBus>();
            var dispatcher = sp.GetRequiredService<IEventDispatcher>();

            // Act
            var busSkipped = await eventBus.SendAsync(new ConditionalRequest(allow: false));
            var dispatcherSkipped = await dispatcher.SendAsync(new ConditionalRequest(allow: false));

            var busHandled = await eventBus.SendAsync(new ConditionalRequest(allow: true));
            var dispatcherHandled = await dispatcher.SendAsync(new ConditionalRequest(allow: true));

            // Assert
            Assert.Null(busSkipped);
            Assert.Null(dispatcherSkipped);

            Assert.NotNull(busHandled);
            Assert.NotNull(dispatcherHandled);
            Assert.Equal(busHandled.Status, dispatcherHandled.Status);
        }
    }
}
