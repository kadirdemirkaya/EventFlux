using System.Collections.Concurrent;
using EventFlux.Abstractions;
using EventFlux.Attributes;
using EventFlux.Delegates;
using EventFlux.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventFlux.Test
{
    public class FastPathResult : IEventResponse
    {
        public int Value { get; set; }
    }

    public class FastPathQuery : IEventRequest<FastPathResult>
    {
        public int Value { get; set; }

        public bool Accept { get; set; } = true;
    }

    public class FastPathEvent : IEventRequest
    {
        public bool FailFirst { get; set; }

        public bool FailSecond { get; set; }
    }

    public class FastPathRecorder
    {
        public ConcurrentQueue<string> Entries { get; } = new();
    }

    public class FastPathQueryHandler : IEventHandler<FastPathQuery, FastPathResult>
    {
        private readonly FastPathRecorder _recorder;

        public FastPathQueryHandler(FastPathRecorder recorder) => _recorder = recorder;

        public bool CanHandle(FastPathQuery @event) => @event.Accept;

        public Task<FastPathResult> Handle(FastPathQuery @event, CancellationToken cancellationToken)
        {
            _recorder.Entries.Enqueue("handler");
            return Task.FromResult(new FastPathResult { Value = @event.Value * 2 });
        }
    }

    [HandlerOrder(3)]
    public class FastPathThirdHandler : IEventHandler<FastPathEvent>
    {
        private readonly FastPathRecorder _recorder;

        public FastPathThirdHandler(FastPathRecorder recorder) => _recorder = recorder;

        public Task Handle(FastPathEvent @event, CancellationToken cancellationToken)
        {
            _recorder.Entries.Enqueue("third");
            return Task.CompletedTask;
        }
    }

    [HandlerOrder(1)]
    public class FastPathFirstHandler : IEventHandler<FastPathEvent>
    {
        private readonly FastPathRecorder _recorder;

        public FastPathFirstHandler(FastPathRecorder recorder) => _recorder = recorder;

        public Task Handle(FastPathEvent @event, CancellationToken cancellationToken)
        {
            _recorder.Entries.Enqueue("first");

            return @event.FailFirst
                ? Task.FromException(new InvalidOperationException("first failed"))
                : Task.CompletedTask;
        }
    }

    [HandlerOrder(2)]
    public class FastPathSecondHandler : IEventHandler<FastPathEvent>
    {
        private readonly FastPathRecorder _recorder;

        public FastPathSecondHandler(FastPathRecorder recorder) => _recorder = recorder;

        public Task Handle(FastPathEvent @event, CancellationToken cancellationToken)
        {
            _recorder.Entries.Enqueue("second");

            return @event.FailSecond
                ? Task.FromException(new ArgumentException("second failed"))
                : Task.CompletedTask;
        }
    }

    public class FastPathSendBehavior : IEventCustomPipeline<FastPathQuery, FastPathResult>
    {
        private readonly FastPathRecorder _recorder;

        public FastPathSendBehavior(FastPathRecorder recorder) => _recorder = recorder;

        public async Task<FastPathResult> Handle(FastPathQuery request, EventHandlerDelegate<FastPathResult> next, CancellationToken cancellationToken)
        {
            _recorder.Entries.Enqueue("behavior-before");
            var result = await next(cancellationToken);
            _recorder.Entries.Enqueue("behavior-after");
            return new FastPathResult { Value = result.Value + 1 };
        }
    }

    public class FastPathPublishBehavior : IEventCustomPipeline<FastPathEvent>
    {
        private readonly FastPathRecorder _recorder;

        public FastPathPublishBehavior(FastPathRecorder recorder) => _recorder = recorder;

        public async Task Handle(FastPathEvent request, EventHandlerDelegate next, CancellationToken cancellationToken)
        {
            _recorder.Entries.Enqueue("behavior-before");
            await next(cancellationToken);
            _recorder.Entries.Enqueue("behavior-after");
        }
    }

    public class FastPathCapturingLogger : ILogger<EventDispatcher>
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Enqueue(formatter(state, exception));
    }

    public class DispatcherFastPathTests : IDisposable
    {
        private ServiceProvider? _provider;

        public void Dispose() => _provider?.Dispose();

        private (EventDispatcher Dispatcher, FastPathRecorder Recorder, FastPathCapturingLogger Logger) Build(
            bool withBehaviors,
            PublishStrategy strategy = PublishStrategy.Parallel)
        {
            var services = new ServiceCollection();
            services.AddSingleton<FastPathRecorder>();
            services.AddTransient<IEventHandler<FastPathQuery, FastPathResult>, FastPathQueryHandler>();
            services.AddTransient<IEventHandler<FastPathEvent>, FastPathThirdHandler>();
            services.AddTransient<IEventHandler<FastPathEvent>, FastPathFirstHandler>();
            services.AddTransient<IEventHandler<FastPathEvent>, FastPathSecondHandler>();

            if (withBehaviors)
            {
                services.AddTransient<IEventCustomPipeline<FastPathQuery, FastPathResult>, FastPathSendBehavior>();
                services.AddTransient<IEventCustomPipeline<FastPathEvent>, FastPathPublishBehavior>();
            }

            _provider = services.BuildServiceProvider();
            var logger = new FastPathCapturingLogger();
            var dispatcher = new EventDispatcher(_provider, logger, new EventFluxOptions { PublishStrategy = strategy });

            return (dispatcher, _provider.GetRequiredService<FastPathRecorder>(), logger);
        }

        [Fact]
        public async Task SendAsync_WithoutBehaviors_ReturnsTheHandlerResult()
        {
            // Arrange
            var (dispatcher, recorder, _) = Build(withBehaviors: false);

            // Act
            var result = await dispatcher.SendAsync(new FastPathQuery { Value = 21 });

            // Assert
            Assert.NotNull(result);
            Assert.Equal(42, result!.Value);
            Assert.Equal(new[] { "handler" }, recorder.Entries.ToArray());
        }

        [Fact]
        public async Task SendAsync_WithoutBehaviors_WhenCanHandleIsFalse_ReturnsNullAndLogsTheSkip()
        {
            // Arrange
            var (dispatcher, recorder, logger) = Build(withBehaviors: false);

            // Act
            var result = await dispatcher.SendAsync(new FastPathQuery { Value = 21, Accept = false });

            // Assert
            Assert.Null(result);
            Assert.Empty(recorder.Entries);
            Assert.Contains(logger.Messages, m => m.Contains("cannot handle event FastPathQuery. Skipping."));
        }

        [Fact]
        public async Task SendAsync_WithOneBehavior_StillWrapsTheHandler()
        {
            // Arrange
            var (dispatcher, recorder, _) = Build(withBehaviors: true);

            // Act
            var result = await dispatcher.SendAsync(new FastPathQuery { Value = 21 });

            // Assert
            Assert.Equal(43, result!.Value);
            Assert.Equal(new[] { "behavior-before", "handler", "behavior-after" }, recorder.Entries.ToArray());
        }

        [Theory]
        [InlineData(PublishStrategy.Parallel)]
        [InlineData(PublishStrategy.Sequential)]
        public async Task PublishAsync_WithoutBehaviors_StartsHandlersInHandlerOrder(PublishStrategy strategy)
        {
            // Arrange
            var (dispatcher, recorder, _) = Build(withBehaviors: false, strategy);

            // Act
            await dispatcher.PublishAsync(new FastPathEvent());

            // Assert
            Assert.Equal(new[] { "first", "second", "third" }, recorder.Entries.ToArray());
        }

        [Theory]
        [InlineData(PublishStrategy.Parallel)]
        [InlineData(PublishStrategy.Sequential)]
        public async Task PublishAsync_WithoutBehaviors_RethrowsASingleFailureAsIs(PublishStrategy strategy)
        {
            // Arrange
            var (dispatcher, _, _) = Build(withBehaviors: false, strategy);

            // Act
            var exception = await Record.ExceptionAsync(() => dispatcher.PublishAsync(new FastPathEvent { FailSecond = true }));

            // Assert
            var failure = Assert.IsType<ArgumentException>(exception);
            Assert.Equal("second failed", failure.Message);
        }

        [Fact]
        public async Task PublishAsync_WithoutBehaviors_WhenTwoHandlersFail_ThrowsAggregateException()
        {
            // Arrange
            var (dispatcher, recorder, _) = Build(withBehaviors: false);

            // Act
            var exception = await Record.ExceptionAsync(() => dispatcher.PublishAsync(new FastPathEvent { FailFirst = true, FailSecond = true }));

            // Assert
            var aggregate = Assert.IsType<AggregateException>(exception);
            Assert.Equal(2, aggregate.InnerExceptions.Count);
            Assert.Contains(aggregate.InnerExceptions, e => e is InvalidOperationException);
            Assert.Contains(aggregate.InnerExceptions, e => e is ArgumentException);
            Assert.Equal(new[] { "first", "second", "third" }, recorder.Entries.ToArray());
        }

        [Fact]
        public async Task PublishAsync_WithoutBehaviors_Sequential_StopsAtTheFirstFailure()
        {
            // Arrange
            var (dispatcher, recorder, _) = Build(withBehaviors: false, PublishStrategy.Sequential);

            // Act
            var exception = await Record.ExceptionAsync(() => dispatcher.PublishAsync(new FastPathEvent { FailFirst = true, FailSecond = true }));

            // Assert
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Equal(new[] { "first" }, recorder.Entries.ToArray());
        }

        [Fact]
        public async Task PublishAsync_WithoutBehaviors_WhenCancelled_ThrowsBeforeAnyHandlerRuns()
        {
            // Arrange
            var (dispatcher, recorder, _) = Build(withBehaviors: false);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var exception = await Record.ExceptionAsync(() => dispatcher.PublishAsync(new FastPathEvent(), cts.Token));

            // Assert
            Assert.IsAssignableFrom<OperationCanceledException>(exception);
            Assert.Empty(recorder.Entries);
        }

        [Fact]
        public async Task PublishAsync_WithOneBehavior_StillWrapsTheHandlers()
        {
            // Arrange
            var (dispatcher, recorder, _) = Build(withBehaviors: true, PublishStrategy.Sequential);

            // Act
            await dispatcher.PublishAsync(new FastPathEvent());

            // Assert
            Assert.Equal(new[] { "behavior-before", "first", "second", "third", "behavior-after" }, recorder.Entries.ToArray());
        }
    }
}
