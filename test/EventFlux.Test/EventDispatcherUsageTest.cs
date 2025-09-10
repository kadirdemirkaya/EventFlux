using Castle.Core.Logging;
using EventFlux.Test.Behaviors;
using EventFlux.Test.Events;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventFlux.Test
{
    public class EventDispatcherUsageTest
    {
        private readonly ServiceProvider _serviceProvider;
        private ServiceCollection services;
        public EventDispatcherUsageTest()
        {
            services = new ServiceCollection();

            services.AddLogging();

            services.AddEventDispatcher()
                    .AddEventLogging()
                    .AddEventTimeout();

            services.AddEventBus(typeof(EventFlux.Test.Events.SendEventResponse).Assembly);

            services.AddTransient(typeof(IEventCustomPipeline<>), typeof(TriggerBehavior<>));

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task SendAsync_WithValidRequest_NoneReturns()
        {
            // Arrange
            var eventBus = _serviceProvider.GetRequiredService<IEventDispatcher>();
            var request = new SendEventRequest { Data = "data-test" };

            // Act
            SendEventResponse response = await eventBus.SendAsync(request);

            // Assert
            Assert.NotNull(response);
        }

        //[Fact]
        //public async Task PublishAsync_WithValidRequest_InvokesHandler()
        //{
        //    // Arrange
        //    PublishEventHandler.HandledCount = 0;
        //    var eventBus = _serviceProvider.GetRequiredService<IEventDispatcher>();
        //    var request = new PublishEventRequest { Data = "publish-test" };

        //    // Act
        //    await eventBus.PublishAsync(request);

        //    // Assert
        //    Assert.Equal(1, PublishEventHandler.HandledCount);
        //    PublishEventHandler.HandledCount = 0;
        //}

        [Fact]
        public async Task PublishAsync_WithValidRequest_Behavior()
        {
            // Arrange
            TriggerBehavior<PublishEventRequest>.Count = 0;
            var eventBus = _serviceProvider.GetRequiredService<IEventDispatcher>();
            var request = new PublishEventRequest { Data = "publish-test" };

            // Act
            await eventBus.PublishAsync(request);

            // Assert
            Assert.Equal(1, TriggerBehavior<PublishEventRequest>.Count);

            TriggerBehavior<PublishEventRequest>.Count = 0;
        }
    }
}
