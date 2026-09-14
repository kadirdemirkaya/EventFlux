using EventFlux.Behaviors;
using EventFlux.Delegates;
using EventFlux.Test.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventFlux.Test
{
    public class TimeoutBehaviorTests
    {
        private const double ShortTimeoutSeconds = 0.2;
        private static readonly TimeSpan HandlerWork = TimeSpan.FromSeconds(5);

        private static TimeoutBehavior<PublishEventRequest> CreateNotificationBehavior(double timeoutSeconds = ShortTimeoutSeconds)
            => new(NullLogger<TimeoutBehavior<PublishEventRequest>>.Instance, timeoutSeconds);

        private static TimeoutBehavior<SendEventRequest, SendEventResponse> CreateRequestBehavior(double timeoutSeconds = ShortTimeoutSeconds)
            => new(NullLogger<TimeoutBehavior<SendEventRequest, SendEventResponse>>.Instance, timeoutSeconds);

        [Fact]
        public async Task Handle_Notification_WhenHandlerCompletesBeforeTimeout_RunsToCompletion()
        {
            // Arrange
            var behavior = CreateNotificationBehavior();
            var executed = false;
            EventHandlerDelegate next = _ =>
            {
                executed = true;
                return Task.CompletedTask;
            };

            // Act
            await behavior.Handle(new PublishEventRequest(), next, CancellationToken.None);

            // Assert
            Assert.True(executed);
        }

        [Fact]
        public async Task Handle_Request_WhenHandlerCompletesBeforeTimeout_ReturnsResponse()
        {
            // Arrange
            var behavior = CreateRequestBehavior();
            var expected = new SendEventResponse { Data = "ok" };
            EventHandlerDelegate<SendEventResponse> next = _ => Task.FromResult(expected);

            // Act
            var response = await behavior.Handle(new SendEventRequest(), next, CancellationToken.None);

            // Assert
            Assert.Same(expected, response);
        }

        [Fact]
        public async Task Handle_Notification_WhenUncooperativeHandlerExceedsTimeout_ThrowsOperationCanceledException()
        {
            // Arrange
            var behavior = CreateNotificationBehavior();
            EventHandlerDelegate next = _ => Task.Delay(HandlerWork, CancellationToken.None);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => behavior.Handle(new PublishEventRequest(), next, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_Request_WhenUncooperativeHandlerExceedsTimeout_ThrowsOperationCanceledException()
        {
            // Arrange
            var behavior = CreateRequestBehavior();
            EventHandlerDelegate<SendEventResponse> next = async _ =>
            {
                await Task.Delay(HandlerWork, CancellationToken.None);
                return new SendEventResponse();
            };

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => behavior.Handle(new SendEventRequest(), next, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_Request_WhenTimeoutElapses_SignalsCancellationToHandler()
        {
            // Arrange
            var behavior = CreateRequestBehavior();
            CancellationToken observedToken = default;
            EventHandlerDelegate<SendEventResponse> next = async ct =>
            {
                observedToken = ct;
                await Task.Delay(HandlerWork, ct);
                return new SendEventResponse();
            };

            // Act
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => behavior.Handle(new SendEventRequest(), next, CancellationToken.None));

            // Assert
            Assert.True(observedToken.CanBeCanceled);
            Assert.True(observedToken.IsCancellationRequested);
        }

        [Fact]
        public async Task Handle_Request_WhenCallerCancels_ThrowsOperationCanceledException()
        {
            // Arrange
            var behavior = CreateRequestBehavior(timeoutSeconds: 30);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            EventHandlerDelegate<SendEventResponse> next = async ct =>
            {
                await Task.Delay(HandlerWork, ct);
                return new SendEventResponse();
            };

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => behavior.Handle(new SendEventRequest(), next, cts.Token));
        }

        [Fact]
        public async Task Handle_Request_WhenHandlerThrows_PropagatesOriginalException()
        {
            // Arrange
            var behavior = CreateRequestBehavior();
            EventHandlerDelegate<SendEventResponse> next = async _ =>
            {
                await Task.Yield();
                throw new InvalidOperationException("boom");
            };

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => behavior.Handle(new SendEventRequest(), next, CancellationToken.None));

            // Assert
            Assert.Equal("boom", exception.Message);
        }
    }
}
