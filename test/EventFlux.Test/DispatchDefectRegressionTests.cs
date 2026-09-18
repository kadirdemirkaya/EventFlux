using System.Collections.Concurrent;
using System.Xml.Linq;
using EventFlux.Abstractions;
using EventFlux.Delegates;
using EventFlux.Extensions;
using EventFlux.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventFlux.Test
{
    public class DualAnswer : IEventResponse
    {
        public string Value { get; set; } = string.Empty;
    }

    public class DualAsk : IEventRequest<DualAnswer>, IEventRequest
    {
        public ConcurrentQueue<string> Trace { get; } = new();
    }

    public class DualAskRequestHandler : IEventHandler<DualAsk, DualAnswer>
    {
        public Task<DualAnswer> Handle(DualAsk request, CancellationToken cancellationToken)
        {
            request.Trace.Enqueue("request-handler");
            return Task.FromResult(new DualAnswer { Value = "answered" });
        }
    }

    public class DualAskNotificationHandler : IEventHandler<DualAsk>
    {
        public Task Handle(DualAsk @event, CancellationToken cancellationToken)
        {
            @event.Trace.Enqueue("notification-handler");
            return Task.CompletedTask;
        }
    }

    public class SameEventDualPipelineBehavior : IEventCustomPipeline<DualAsk, DualAnswer>, IEventCustomPipeline<DualAsk>
    {
        public async Task<DualAnswer> Handle(DualAsk request, EventHandlerDelegate<DualAnswer> next, CancellationToken cancellationToken)
        {
            request.Trace.Enqueue("request-behavior");
            return await next(cancellationToken);
        }

        public async Task Handle(DualAsk request, EventHandlerDelegate next, CancellationToken cancellationToken)
        {
            request.Trace.Enqueue("notification-behavior");
            await next(cancellationToken);
        }
    }

    public class GenericDualPipelineBehavior<TRequest, TResponse> : IEventCustomPipeline<TRequest, TResponse>, IEventCustomPipeline<TRequest>
        where TRequest : DualAsk, IEventRequest<TResponse>, IEventRequest
        where TResponse : IEventResponse
    {
        public async Task<TResponse> Handle(TRequest request, EventHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            request.Trace.Enqueue("generic-request-behavior");
            return await next(cancellationToken);
        }

        public async Task Handle(TRequest request, EventHandlerDelegate next, CancellationToken cancellationToken)
        {
            request.Trace.Enqueue("generic-notification-behavior");
            await next(cancellationToken);
        }
    }

    public class MultiFailureEvent : IEventRequest
    {
    }

    public class MultiFailureFirstHandler : IEventHandler<MultiFailureEvent>
    {
        public const string Message = "first handler failed";

        public async Task Handle(MultiFailureEvent @event, CancellationToken cancellationToken)
        {
            await Task.Yield();
            throw new InvalidOperationException(Message);
        }
    }

    public class MultiFailureSecondHandler : IEventHandler<MultiFailureEvent>
    {
        public const string Message = "second handler failed";

        public Task Handle(MultiFailureEvent @event, CancellationToken cancellationToken)
            => throw new ArgumentException(Message);
    }

    public class MultiFailureSucceedingHandler : IEventHandler<MultiFailureEvent>
    {
        public static int Invocations;

        public Task Handle(MultiFailureEvent @event, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Invocations);
            return Task.CompletedTask;
        }
    }

    public class SingleHandlerEvent : IEventRequest
    {
        public bool Fail { get; set; }

        public bool Skip { get; set; }
    }

    public class SingleHandlerOnlyHandler : IEventHandler<SingleHandlerEvent>
    {
        public const string Message = "single handler failed";

        public static int Invocations;

        public bool CanHandle(SingleHandlerEvent @event) => !@event.Skip;

        public Task Handle(SingleHandlerEvent @event, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Invocations);

            if (@event.Fail)
                throw new InvalidOperationException(Message);

            return Task.CompletedTask;
        }
    }

    public class DispatchDefectRegressionTests
    {
        private static ServiceProvider BuildProvider(PublishStrategy strategy, Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventDispatcher();
            services.AddEventBus(options => options.PublishStrategy = strategy, typeof(DispatchDefectRegressionTests).Assembly);
            configure?.Invoke(services);

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task EventDispatcher_BehaviorImplementingBothShapesForSameEvent_RunsOnSendAndPublish()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel, services =>
            {
                services.AddTransient<IEventCustomPipeline<DualAsk, DualAnswer>, SameEventDualPipelineBehavior>();
                services.AddTransient<IEventCustomPipeline<DualAsk>, SameEventDualPipelineBehavior>();
            });
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();
            var sent = new DualAsk();
            var published = new DualAsk();

            // Act
            var response = await dispatcher.SendAsync<DualAnswer>(sent);
            await dispatcher.PublishAsync(published);

            // Assert
            Assert.Equal("answered", response!.Value);
            Assert.Equal(new[] { "request-behavior", "request-handler" }, sent.Trace);
            Assert.Equal(new[] { "notification-behavior", "notification-handler" }, published.Trace);
        }

        [Fact]
        public async Task EventDispatcher_ClosedGenericBehaviorImplementingBothShapes_RunsOnSendAndPublish()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel, services =>
            {
                services.AddTransient<IEventCustomPipeline<DualAsk, DualAnswer>, GenericDualPipelineBehavior<DualAsk, DualAnswer>>();
                services.AddTransient<IEventCustomPipeline<DualAsk>, GenericDualPipelineBehavior<DualAsk, DualAnswer>>();
            });
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();
            var sent = new DualAsk();
            var published = new DualAsk();

            // Act
            var response = await dispatcher.SendAsync<DualAnswer>(sent);
            await dispatcher.PublishAsync(published);

            // Assert
            Assert.Equal("answered", response!.Value);
            Assert.Equal(new[] { "generic-request-behavior", "request-handler" }, sent.Trace);
            Assert.Equal(new[] { "generic-notification-behavior", "notification-handler" }, published.Trace);
        }

        [Fact]
        public async Task EventBus_PublishAsync_Parallel_WhenSeveralHandlersFail_ThrowsAggregateWithEveryException()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var bus = provider.GetRequiredService<IEventBus>();

            // Act
            var exception = await Assert.ThrowsAsync<AggregateException>(() => bus.PublishAsync(new MultiFailureEvent()));

            // Assert
            AssertContainsBothHandlerFailures(exception);
        }

        [Fact]
        public async Task EventDispatcher_PublishAsync_Parallel_WhenSeveralHandlersFail_ThrowsAggregateWithEveryException()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();

            // Act
            var exception = await Assert.ThrowsAsync<AggregateException>(() => dispatcher.PublishAsync(new MultiFailureEvent()));

            // Assert
            AssertContainsBothHandlerFailures(exception);
        }

        [Fact]
        public async Task EventDispatcher_PublishAsync_Parallel_WithBuiltInBehaviors_ThrowsAggregateWithEveryException()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel, services => services.AddEventLogging().AddEventTimeout());
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();

            // Act
            var exception = await Assert.ThrowsAsync<AggregateException>(() => dispatcher.PublishAsync(new MultiFailureEvent()));

            // Assert
            AssertContainsBothHandlerFailures(exception);
        }

        [Fact]
        public async Task EventBus_PublishAsync_Parallel_WhenSeveralHandlersFail_StillRunsSucceedingHandler()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var bus = provider.GetRequiredService<IEventBus>();
            var before = Volatile.Read(ref MultiFailureSucceedingHandler.Invocations);

            // Act
            await Assert.ThrowsAsync<AggregateException>(() => bus.PublishAsync(new MultiFailureEvent()));

            // Assert
            Assert.True(Volatile.Read(ref MultiFailureSucceedingHandler.Invocations) > before);
        }

        [Fact]
        public async Task EventBus_PublishAsync_Sequential_WhenSeveralHandlersFail_StillThrowsFirstExceptionOnly()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Sequential);
            var bus = provider.GetRequiredService<IEventBus>();

            // Act
            var exception = await Assert.ThrowsAnyAsync<Exception>(() => bus.PublishAsync(new MultiFailureEvent()));

            // Assert
            Assert.IsNotType<AggregateException>(exception);
        }

        [Fact]
        public async Task EventBus_PublishAsync_Parallel_SingleHandler_InvokesHandler()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var bus = provider.GetRequiredService<IEventBus>();
            var before = Volatile.Read(ref SingleHandlerOnlyHandler.Invocations);

            // Act
            await bus.PublishAsync(new SingleHandlerEvent());

            // Assert
            Assert.Equal(before + 1, Volatile.Read(ref SingleHandlerOnlyHandler.Invocations));
        }

        [Fact]
        public async Task EventBus_PublishAsync_Parallel_SingleHandler_WhenFaultsSynchronously_ThrowsOriginalExceptionNotAggregate()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var bus = provider.GetRequiredService<IEventBus>();

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => bus.PublishAsync(new SingleHandlerEvent { Fail = true }));

            // Assert
            Assert.Equal(SingleHandlerOnlyHandler.Message, exception.Message);
        }

        [Fact]
        public async Task EventBus_PublishAsync_Parallel_SingleHandler_WhenCanHandleFalse_SkipsHandler()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var bus = provider.GetRequiredService<IEventBus>();
            var before = Volatile.Read(ref SingleHandlerOnlyHandler.Invocations);

            // Act
            await bus.PublishAsync(new SingleHandlerEvent { Skip = true });

            // Assert
            Assert.Equal(before, Volatile.Read(ref SingleHandlerOnlyHandler.Invocations));
        }

        [Fact]
        public async Task EventDispatcher_PublishAsync_Parallel_SingleHandler_InvokesHandler()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();
            var before = Volatile.Read(ref SingleHandlerOnlyHandler.Invocations);

            // Act
            await dispatcher.PublishAsync(new SingleHandlerEvent());

            // Assert
            Assert.Equal(before + 1, Volatile.Read(ref SingleHandlerOnlyHandler.Invocations));
        }

        [Fact]
        public async Task EventDispatcher_PublishAsync_Parallel_SingleHandler_WhenFaultsSynchronously_ThrowsOriginalExceptionNotAggregate()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => dispatcher.PublishAsync(new SingleHandlerEvent { Fail = true }));

            // Assert
            Assert.Equal(SingleHandlerOnlyHandler.Message, exception.Message);
        }

        [Fact]
        public async Task EventDispatcher_PublishAsync_Parallel_SingleHandler_WhenCanHandleFalse_SkipsHandler()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();
            var before = Volatile.Read(ref SingleHandlerOnlyHandler.Invocations);

            // Act
            await dispatcher.PublishAsync(new SingleHandlerEvent { Skip = true });

            // Assert
            Assert.Equal(before, Volatile.Read(ref SingleHandlerOnlyHandler.Invocations));
        }

        [Fact]
        public async Task EventDispatcher_SendAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();

            // Act
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.SendAsync<DualAnswer>(null!));

            // Assert
            Assert.Equal("request", exception.ParamName);
        }

        [Fact]
        public async Task EventDispatcher_UnitSendAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();

            // Act
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.SendAsync((IEventRequest<EventFlux.Abstractions.Unit>)null!));

            // Assert
            Assert.Equal("request", exception.ParamName);
        }

        [Fact]
        public async Task EventDispatcher_PublishAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();

            // Act
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.PublishAsync(null!));

            // Assert
            Assert.Equal("request", exception.ParamName);
        }

        [Fact]
        public async Task EventDispatcher_PublishAsync_NullRequestWithCancelledToken_ThrowsArgumentNullExceptionLikeEventBus()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();
            var bus = provider.GetRequiredService<IEventBus>();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var busException = await Assert.ThrowsAnyAsync<Exception>(() => bus.PublishAsync(null!, cts.Token));
            var dispatcherException = await Assert.ThrowsAnyAsync<Exception>(() => dispatcher.PublishAsync(null!, cts.Token));

            // Assert
            Assert.IsType<ArgumentNullException>(busException);
            Assert.IsType<ArgumentNullException>(dispatcherException);
        }

        [Fact]
        public void EventDispatcher_Constructor_NullServiceProvider_ThrowsArgumentNullException()
        {
            // Act
            var exception = Assert.Throws<ArgumentNullException>(() => new EventDispatcher(null!, NullLogger<EventDispatcher>.Instance));

            // Assert
            Assert.Equal("serviceProvider", exception.ParamName);
        }

        [Fact]
        public void EventDispatcher_ConstructorWithOptions_NullServiceProvider_ThrowsArgumentNullException()
        {
            // Act
            var exception = Assert.Throws<ArgumentNullException>(() => new EventDispatcher(null!, NullLogger<EventDispatcher>.Instance, new EventFluxOptions()));

            // Assert
            Assert.Equal("serviceProvider", exception.ParamName);
        }

        [Fact]
        public void EventDispatcher_Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);

            // Act
            var exception = Assert.Throws<ArgumentNullException>(() => new EventDispatcher(provider, null!));

            // Assert
            Assert.Equal("logger", exception.ParamName);
        }

        [Theory]
        [InlineData("M:EventFlux.Abstractions.IEventDispatcher.PublishAsync(EventFlux.Abstractions.IEventRequest,System.Threading.CancellationToken)")]
        [InlineData("M:EventFlux.Abstractions.IEventDispatcher.SendAsync``1(EventFlux.Abstractions.IEventRequest{``0},System.Threading.CancellationToken)")]
        [InlineData("M:EventFlux.Abstractions.IEventDispatcher.SendAsync(EventFlux.Abstractions.IEventRequest{EventFlux.Abstractions.Unit},System.Threading.CancellationToken)")]
        public void EventDispatcher_XmlDocs_DocumentArgumentNullException(string memberId)
        {
            // Arrange
            var path = Path.ChangeExtension(typeof(IEventDispatcher).Assembly.Location, ".xml");

            // Act
            var member = XDocument.Load(path).Descendants("member").SingleOrDefault(m => (string?)m.Attribute("name") == memberId);

            // Assert
            Assert.NotNull(member);
            Assert.Contains(member!.Elements("exception"), e => (string?)e.Attribute("cref") == "T:System.ArgumentNullException");
        }

        [Theory]
        [InlineData("M:EventFlux.Abstractions.IEventBus.PublishAsync(EventFlux.Abstractions.IEventRequest,System.Threading.CancellationToken)")]
        [InlineData("M:EventFlux.Abstractions.IEventDispatcher.PublishAsync(EventFlux.Abstractions.IEventRequest,System.Threading.CancellationToken)")]
        public void PublishAsync_XmlDocs_DescribeAggregateExceptionForSeveralParallelFailures(string memberId)
        {
            // Arrange
            var path = Path.ChangeExtension(typeof(IEventBus).Assembly.Location, ".xml");

            // Act
            var member = XDocument.Load(path).Descendants("member").SingleOrDefault(m => (string?)m.Attribute("name") == memberId);

            // Assert
            Assert.NotNull(member);
            Assert.Contains(member!.Element("remarks")!.Descendants("see"), e => (string?)e.Attribute("cref") == "T:System.AggregateException");
            Assert.DoesNotContain("not observed", member.Element("remarks")!.Value);
        }

        private static void AssertContainsBothHandlerFailures(AggregateException exception)
        {
            Assert.Equal(2, exception.InnerExceptions.Count);
            Assert.Contains(exception.InnerExceptions, e => e is InvalidOperationException && e.Message == MultiFailureFirstHandler.Message);
            Assert.Contains(exception.InnerExceptions, e => e is ArgumentException && e.Message == MultiFailureSecondHandler.Message);
        }
    }
}
