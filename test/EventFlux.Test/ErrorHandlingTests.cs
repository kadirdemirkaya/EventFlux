using EventFlux.Abstractions;
using EventFlux.Extensions;
using EventFlux.Test.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    public class ErrorHandlingTests
    {
        private static ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventDispatcher();
            services.AddEventBus(typeof(ErrorHandlingTests).Assembly);

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task EventBus_PublishAsync_SurfacesOriginalExceptionFromHandler()
        {
            using var provider = BuildProvider();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.GetRequiredService<IEventBus>().PublishAsync(new SyncThrowEventRequest()));

            Assert.Equal(SyncThrowEventHandler.Message, exception.Message);
        }

        [Fact]
        public async Task EventBus_SendAsync_SurfacesOriginalExceptionFromHandler()
        {
            using var provider = BuildProvider();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.GetRequiredService<IEventBus>()
                    .SendAsync<SyncThrowResponse>(new SyncThrowResponseRequest()));

            Assert.Equal(SyncThrowResponseHandler.Message, exception.Message);
        }

        [Fact]
        public async Task EventDispatcher_PublishAsync_SurfacesOriginalExceptionFromHandler()
        {
            using var provider = BuildProvider();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.GetRequiredService<IEventDispatcher>().PublishAsync(new SyncThrowEventRequest()));

            Assert.Equal(SyncThrowEventHandler.Message, exception.Message);
        }

        [Fact]
        public async Task EventDispatcher_SendAsync_SurfacesOriginalExceptionFromHandler()
        {
            using var provider = BuildProvider();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.GetRequiredService<IEventDispatcher>()
                    .SendAsync<SyncThrowResponse>(new SyncThrowResponseRequest()));

            Assert.Equal(SyncThrowResponseHandler.Message, exception.Message);
        }

        [Fact]
        public async Task EventBus_ConstructedWithoutServiceProvider_PublishThrowsDescriptiveError()
        {
            var eventBus = new EventBus();

            var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
                eventBus.PublishAsync(new SyncThrowEventRequest()));

            Assert.IsNotType<NullReferenceException>(exception);
        }

        [Fact]
        public async Task EventBus_ConstructedWithoutServiceProvider_SendThrowsDescriptiveError()
        {
            var eventBus = new EventBus();

            var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
                eventBus.SendAsync<SyncThrowResponse>(new SyncThrowResponseRequest()));

            Assert.IsNotType<NullReferenceException>(exception);
        }

        [Fact]
        public void EventBus_ConstructedWithoutServiceProvider_AddStackRequestEventDoesNotThrow()
        {
            var eventBus = new EventBus();

            eventBus.AddStackRequestEvent(new SyncThrowEventRequest());
        }

        [Fact]
        public async Task EventBus_ConstructedWithoutServiceProvider_StackDispatchThrowsDescriptiveError()
        {
            var eventBus = new EventBus();
            eventBus.AddStackRequestEvent(new SyncThrowEventRequest());

            var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
                eventBus.StackEventDispatcherAsync());

            Assert.IsNotType<NullReferenceException>(exception);
        }
    }
}
