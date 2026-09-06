using EventFlux.Abstractions;
using EventFlux.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventFlux.Test
{
    [Collection("SharedHandlerCounters")]
    public class CancellationTokenHandlerTests
    {
        public record LegacyCommand(string Name) : IEventRequest<LegacyCommandResponse>;
        public record LegacyCommandResponse(string Result) : IEventResponse;

        public class LegacyCommandHandler : IEventHandler<LegacyCommand, LegacyCommandResponse>
        {
            public Task<LegacyCommandResponse> Handle(LegacyCommand @event)
            {
                return Task.FromResult(new LegacyCommandResponse($"Hello {@event.Name}"));
            }
        }

        public record TokenAwareCommand(string Name) : IEventRequest<TokenAwareCommandResponse>;
        public record TokenAwareCommandResponse(bool ReceivedCancellationToken) : IEventResponse;

        public class TokenAwareCommandHandler : IEventHandler<TokenAwareCommand, TokenAwareCommandResponse>
        {
            public static CancellationToken LastToken { get; private set; }

            public Task<TokenAwareCommandResponse> Handle(TokenAwareCommand @event, CancellationToken cancellationToken)
            {
                LastToken = cancellationToken;
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new TokenAwareCommandResponse(cancellationToken.CanBeCanceled));
            }
        }

        public record LegacyNotification(string Message) : IEventRequest;

        public class LegacyNotificationHandler : IEventHandler<LegacyNotification>
        {
            public static bool WasCalled { get; set; }

            public Task Handle(LegacyNotification @event)
            {
                WasCalled = true;
                return Task.CompletedTask;
            }
        }

        public record TokenAwareNotification(string Message) : IEventRequest;

        public class TokenAwareNotificationHandler : IEventHandler<TokenAwareNotification>
        {
            public static CancellationToken LastToken { get; private set; }
            public static bool WasCalled { get; set; }

            public Task Handle(TokenAwareNotification @event, CancellationToken cancellationToken)
            {
                WasCalled = true;
                LastToken = cancellationToken;
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        public record LongRunningCommand(int DelayMs) : IEventRequest<LongRunningResponse>;
        public record LongRunningResponse(bool Completed) : IEventResponse;

        public class LongRunningCommandHandler : IEventHandler<LongRunningCommand, LongRunningResponse>
        {
            public async Task<LongRunningResponse> Handle(LongRunningCommand @event, CancellationToken cancellationToken)
            {
                await Task.Delay(@event.DelayMs, cancellationToken).ConfigureAwait(false);
                return new LongRunningResponse(true);
            }
        }

        private static ServiceProvider CreateProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(CancellationTokenHandlerTests).Assembly);
            services.AddEventDispatcher();
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task EventBus_SendAsync_WithLegacyHandler_ExecutesSuccessfully()
        {
            // Arrange
            using var sp = CreateProvider();
            var bus = sp.GetRequiredService<IEventBus>();

            // Act
            var response = await bus.SendAsync(new LegacyCommand("World"));

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Hello World", response.Result);
        }

        [Fact]
        public async Task EventBus_SendAsync_WithTokenAwareHandler_ReceivesCancellationToken()
        {
            // Arrange
            using var sp = CreateProvider();
            var bus = sp.GetRequiredService<IEventBus>();
            using var cts = new CancellationTokenSource();

            // Act
            var response = await bus.SendAsync(new TokenAwareCommand("Test"), cts.Token);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.ReceivedCancellationToken);
            Assert.Equal(cts.Token, TokenAwareCommandHandler.LastToken);
        }

        [Fact]
        public async Task EventBus_SendAsync_WithCancelledToken_ThrowsOperationCanceledExceptionFromHandler()
        {
            // Arrange
            using var sp = CreateProvider();
            var bus = sp.GetRequiredService<IEventBus>();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await bus.SendAsync(new TokenAwareCommand("Test"), cts.Token);
            });
        }

        [Fact]
        public async Task EventDispatcher_SendAsync_WithTokenAwareHandler_ReceivesCancellationToken()
        {
            // Arrange
            using var sp = CreateProvider();
            var dispatcher = sp.GetRequiredService<IEventDispatcher>();
            using var cts = new CancellationTokenSource();

            // Act
            var response = await dispatcher.SendAsync(new TokenAwareCommand("DispatcherTest"), cts.Token);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.ReceivedCancellationToken);
            Assert.Equal(cts.Token, TokenAwareCommandHandler.LastToken);
        }

        [Fact]
        public async Task EventBus_PublishAsync_WithLegacyNotificationHandler_ExecutesSuccessfully()
        {
            // Arrange
            using var sp = CreateProvider();
            var bus = sp.GetRequiredService<IEventBus>();
            LegacyNotificationHandler.WasCalled = false;

            // Act
            await bus.PublishAsync(new LegacyNotification("Hello"));

            // Assert
            Assert.True(LegacyNotificationHandler.WasCalled);
        }

        [Fact]
        public async Task EventBus_PublishAsync_WithTokenAwareNotificationHandler_ReceivesCancellationToken()
        {
            // Arrange
            using var sp = CreateProvider();
            var bus = sp.GetRequiredService<IEventBus>();
            using var cts = new CancellationTokenSource();
            TokenAwareNotificationHandler.WasCalled = false;

            // Act
            await bus.PublishAsync(new TokenAwareNotification("HelloToken"), cts.Token);

            // Assert
            Assert.True(TokenAwareNotificationHandler.WasCalled);
            Assert.Equal(cts.Token, TokenAwareNotificationHandler.LastToken);
        }

        [Fact]
        public async Task EventDispatcher_PublishAsync_WithTokenAwareNotificationHandler_ReceivesCancellationToken()
        {
            // Arrange
            using var sp = CreateProvider();
            var dispatcher = sp.GetRequiredService<IEventDispatcher>();
            using var cts = new CancellationTokenSource();
            TokenAwareNotificationHandler.WasCalled = false;

            // Act
            await dispatcher.PublishAsync(new TokenAwareNotification("HelloDispatcherToken"), cts.Token);

            // Assert
            Assert.True(TokenAwareNotificationHandler.WasCalled);
            Assert.Equal(cts.Token, TokenAwareNotificationHandler.LastToken);
        }

        [Fact]
        public async Task EventBus_SendAsync_WithInFlightCancellation_CancelsHandlerOperation()
        {
            // Arrange
            using var sp = CreateProvider();
            var bus = sp.GetRequiredService<IEventBus>();
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(20);

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await bus.SendAsync(new LongRunningCommand(200), cts.Token);
            });
        }

        [Fact]
        public async Task EventDispatcher_SendAsync_WithInFlightCancellation_CancelsHandlerOperation()
        {
            // Arrange
            using var sp = CreateProvider();
            var dispatcher = sp.GetRequiredService<IEventDispatcher>();
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(20);

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await dispatcher.SendAsync(new LongRunningCommand(200), cts.Token);
            });
        }
    }
}
