using EventFlux.Abstractions;
using EventFlux.Extensions;
using EventFlux.Options;
using EventFlux.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventFlux.Test
{
    public class RequeueProbeEvent : IEventRequest
    {
        public RequeueProbeEvent(string id, List<string> handled)
        {
            Id = id;
            Handled = handled;
        }

        public string Id { get; }

        public List<string> Handled { get; }

        public CancellationTokenSource? CancelOnHandle { get; set; }

        public bool ThrowCancellationAfterCancel { get; set; }

        public IEventRequest? EnqueueOnHandle { get; set; }

        public EventStackService? Stack { get; set; }

        public Exception? Failure { get; set; }
    }

    public class RequeueProbeEventHandler : IEventHandler<RequeueProbeEvent>
    {
        public Task Handle(RequeueProbeEvent request, CancellationToken cancellationToken = default)
        {
            lock (request.Handled)
            {
                request.Handled.Add(request.Id);
            }

            if (request.Failure != null)
                throw request.Failure;

            if (request.EnqueueOnHandle != null)
                request.Stack!.AddEventRequest(request.EnqueueOnHandle);

            if (request.CancelOnHandle != null)
            {
                request.CancelOnHandle.Cancel();
                if (request.ThrowCancellationAfterCancel)
                    cancellationToken.ThrowIfCancellationRequested();
            }

            return Task.CompletedTask;
        }
    }

    public class RecordedLogEntry
    {
        public RecordedLogEntry(LogLevel level, string message, Exception? exception)
        {
            Level = level;
            Message = message;
            Exception = exception;
        }

        public LogLevel Level { get; }

        public string Message { get; }

        public Exception? Exception { get; }
    }

    public sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<RecordedLogEntry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(this);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger : ILogger
        {
            private readonly RecordingLoggerProvider _provider;

            public RecordingLogger(RecordingLoggerProvider provider) => _provider = provider;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (_provider.Entries)
                {
                    _provider.Entries.Add(new RecordedLogEntry(logLevel, formatter(state, exception), exception));
                }
            }
        }
    }

    public class StackCancellationRequeueTests
    {
        private static ServiceProvider CreateProvider(bool requeue, RecordingLoggerProvider? loggerProvider = null)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                if (loggerProvider != null)
                    builder.AddProvider(loggerProvider);
            });
            services.AddEventBus(options => options.RequeueStackOnCancellation = requeue, typeof(StackCancellationRequeueTests).Assembly);
            return services.BuildServiceProvider();
        }

        private static List<string> QueuedIds(EventStackService stack)
            => stack.Drain().Cast<RequeueProbeEvent>().Select(e => e.Id).ToList();

        [Fact]
        public void RequeueStackOnCancellation_DefaultsToFalse()
        {
            // Arrange
            using var provider = CreateProvider(false);

            // Act
            var options = new EventFluxOptions();

            // Assert
            Assert.False(options.RequeueStackOnCancellation);
            Assert.False(provider.GetRequiredService<EventFluxOptions>().RequeueStackOnCancellation);
        }

        [Fact]
        public async Task DefaultMode_PreCancelledToken_ThrowsAndDiscardsQueuedEvents()
        {
            // Arrange
            using var provider = CreateProvider(false);
            var stack = provider.GetRequiredService<EventStackService>();
            var handled = new List<string>();
            using var scope = provider.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            bus.AddStackRequestEvent(new RequeueProbeEvent("a", handled));
            bus.AddStackRequestEvent(new RequeueProbeEvent("b", handled));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            await Assert.ThrowsAsync<OperationCanceledException>(() => bus.StackEventDispatcherAsync(cts.Token));

            // Assert
            Assert.Empty(handled);
            Assert.True(stack.IsEmpty);
        }

        [Fact]
        public async Task DefaultMode_CancelledAfterFirstEvent_ThrowsAndDiscardsRemainingEvents()
        {
            // Arrange
            using var provider = CreateProvider(false);
            var stack = provider.GetRequiredService<EventStackService>();
            var handled = new List<string>();
            using var cts = new CancellationTokenSource();
            var bus = provider.GetRequiredService<IEventBus>();
            bus.AddStackRequestEvent(new RequeueProbeEvent("a", handled) { CancelOnHandle = cts });
            bus.AddStackRequestEvent(new RequeueProbeEvent("b", handled));
            bus.AddStackRequestEvent(new RequeueProbeEvent("c", handled));

            // Act
            await Assert.ThrowsAsync<OperationCanceledException>(() => bus.StackEventDispatcherAsync(cts.Token));

            // Assert
            Assert.Equal(new[] { "a" }, handled);
            Assert.True(stack.IsEmpty);
        }

        [Fact]
        public async Task RequeueMode_PreCancelledToken_ThrowsAndKeepsAllEventsInOrder()
        {
            // Arrange
            using var provider = CreateProvider(true);
            var stack = provider.GetRequiredService<EventStackService>();
            var handled = new List<string>();
            var bus = provider.GetRequiredService<IEventBus>();
            bus.AddStackRequestEvent(new RequeueProbeEvent("a", handled));
            bus.AddStackRequestEvent(new RequeueProbeEvent("b", handled));
            bus.AddStackRequestEvent(new RequeueProbeEvent("c", handled));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            await Assert.ThrowsAsync<OperationCanceledException>(() => bus.StackEventDispatcherAsync(cts.Token));

            // Assert
            Assert.Empty(handled);
            Assert.Equal(new[] { "a", "b", "c" }, QueuedIds(stack));
        }

        [Fact]
        public async Task RequeueMode_CancelledAfterFirstEvent_RequeuesRemainingEventsWithoutDuplicatingFirst()
        {
            // Arrange
            using var provider = CreateProvider(true);
            var stack = provider.GetRequiredService<EventStackService>();
            var handled = new List<string>();
            using var cts = new CancellationTokenSource();
            var bus = provider.GetRequiredService<IEventBus>();
            bus.AddStackRequestEvent(new RequeueProbeEvent("a", handled) { CancelOnHandle = cts });
            bus.AddStackRequestEvent(new RequeueProbeEvent("b", handled));
            bus.AddStackRequestEvent(new RequeueProbeEvent("c", handled));

            // Act
            await Assert.ThrowsAsync<OperationCanceledException>(() => bus.StackEventDispatcherAsync(cts.Token));

            // Assert
            Assert.Equal(new[] { "a" }, handled);
            Assert.Equal(new[] { "b", "c" }, QueuedIds(stack));
        }

        [Fact]
        public async Task RequeueMode_CancellationThrownByInProgressPublish_DoesNotRequeueThatEvent()
        {
            // Arrange
            using var provider = CreateProvider(true);
            var stack = provider.GetRequiredService<EventStackService>();
            var handled = new List<string>();
            using var cts = new CancellationTokenSource();
            var bus = provider.GetRequiredService<IEventBus>();
            bus.AddStackRequestEvent(new RequeueProbeEvent("a", handled));
            bus.AddStackRequestEvent(new RequeueProbeEvent("b", handled) { CancelOnHandle = cts, ThrowCancellationAfterCancel = true });
            bus.AddStackRequestEvent(new RequeueProbeEvent("c", handled));

            // Act
            await Assert.ThrowsAsync<OperationCanceledException>(() => bus.StackEventDispatcherAsync(cts.Token));

            // Assert
            Assert.Equal(new[] { "a", "b" }, handled);
            Assert.Equal(new[] { "c" }, QueuedIds(stack));
        }

        [Fact]
        public async Task RequeueMode_EventsQueuedByAnotherScope_SurviveCancellationAndStayAheadOfNewEvents()
        {
            // Arrange
            using var provider = CreateProvider(true);
            var stack = provider.GetRequiredService<EventStackService>();
            var handled = new List<string>();
            using var cts = new CancellationTokenSource();
            using var otherScope = provider.CreateScope();
            using var dispatchingScope = provider.CreateScope();
            var otherBus = otherScope.ServiceProvider.GetRequiredService<IEventBus>();
            var dispatchingBus = dispatchingScope.ServiceProvider.GetRequiredService<IEventBus>();
            dispatchingBus.AddStackRequestEvent(new RequeueProbeEvent("own-1", handled)
            {
                CancelOnHandle = cts,
                Stack = stack,
                EnqueueOnHandle = new RequeueProbeEvent("late", handled)
            });
            otherBus.AddStackRequestEvent(new RequeueProbeEvent("other-1", handled));
            otherBus.AddStackRequestEvent(new RequeueProbeEvent("other-2", handled));

            // Act
            await Assert.ThrowsAsync<OperationCanceledException>(() => dispatchingBus.StackEventDispatcherAsync(cts.Token));

            // Assert
            Assert.Equal(new[] { "own-1" }, handled);
            Assert.Equal(new[] { "other-1", "other-2", "late" }, QueuedIds(stack));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CallerCancellation_IsNotLoggedAsDispatchFailure(bool requeue)
        {
            // Arrange
            var loggerProvider = new RecordingLoggerProvider();
            using var provider = CreateProvider(requeue, loggerProvider);
            var handled = new List<string>();
            using var cts = new CancellationTokenSource();
            var bus = provider.GetRequiredService<IEventBus>();
            bus.AddStackRequestEvent(new RequeueProbeEvent("a", handled) { CancelOnHandle = cts, ThrowCancellationAfterCancel = true });
            bus.AddStackRequestEvent(new RequeueProbeEvent("b", handled));

            // Act
            await Assert.ThrowsAsync<OperationCanceledException>(() => bus.StackEventDispatcherAsync(cts.Token));

            // Assert
            Assert.Equal(new[] { "a" }, handled);
            Assert.DoesNotContain(loggerProvider.Entries, e => e.Level >= LogLevel.Error);
            Assert.DoesNotContain(loggerProvider.Entries, e => e.Message.Contains("Stack dispatch failed"));
        }

        [Fact]
        public async Task HandlerFailure_IsStillLoggedAtErrorAndNextEventDispatched()
        {
            // Arrange
            var loggerProvider = new RecordingLoggerProvider();
            using var provider = CreateProvider(true, loggerProvider);
            var stack = provider.GetRequiredService<EventStackService>();
            var handled = new List<string>();
            var bus = provider.GetRequiredService<IEventBus>();
            bus.AddStackRequestEvent(new RequeueProbeEvent("a", handled) { Failure = new InvalidOperationException("handler failed") });
            bus.AddStackRequestEvent(new RequeueProbeEvent("b", handled));

            // Act
            await bus.StackEventDispatcherAsync();

            // Assert
            Assert.Equal(new[] { "a", "b" }, handled);
            Assert.True(stack.IsEmpty);
            var error = Assert.Single(loggerProvider.Entries, e => e.Level == LogLevel.Error);
            Assert.Contains("Stack dispatch failed", error.Message);
            Assert.IsType<InvalidOperationException>(error.Exception);
        }
    }
}
