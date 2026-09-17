using EventFlux.Delegates;

namespace EventFlux.Test.Events
{
    using EventFlux.Abstractions;

    public interface ITraceableEvent
    {
        List<string> Trace { get; }
    }

    public class RecordUnitCommand : IEventRequest<Unit>, ITraceableEvent
    {
        public List<string> Trace { get; } = new();
    }

    public class RecordUnitCommandHandler : IEventHandler<RecordUnitCommand, Unit>
    {
        public Task<Unit> Handle(RecordUnitCommand @event, CancellationToken cancellationToken)
        {
            @event.Trace.Add("handler");
            return Unit.Task;
        }
    }

    public class UnhandledUnitCommand : IEventRequest<Unit>
    {
    }

    public class DeclinedUnitCommand : IEventRequest<Unit>, ITraceableEvent
    {
        public List<string> Trace { get; } = new();
    }

    public class DeclinedUnitCommandHandler : IEventHandler<DeclinedUnitCommand, Unit>
    {
        public bool CanHandle(DeclinedUnitCommand @event) => false;

        public Task<Unit> Handle(DeclinedUnitCommand @event, CancellationToken cancellationToken)
        {
            @event.Trace.Add("handler");
            return Unit.Task;
        }
    }

    public class UncooperativeSlowUnitCommand : IEventRequest<Unit>
    {
        public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(5);
    }

    public class UncooperativeSlowUnitCommandHandler : IEventHandler<UncooperativeSlowUnitCommand, Unit>
    {
        public async Task<Unit> Handle(UncooperativeSlowUnitCommand @event, CancellationToken cancellationToken)
        {
            await Task.Delay(@event.Delay, CancellationToken.None);
            return Unit.Value;
        }
    }

    public class UncooperativeSlowNotification : IEventRequest
    {
        public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(5);
    }

    public class UncooperativeSlowNotificationHandler : IEventHandler<UncooperativeSlowNotification>
    {
        public Task Handle(UncooperativeSlowNotification @event, CancellationToken cancellationToken)
            => Task.Delay(@event.Delay, CancellationToken.None);
    }

    public class TracedNotification : IEventRequest, ITraceableEvent
    {
        public List<string> Trace { get; } = new();
    }

    public class TracedNotificationHandler : IEventHandler<TracedNotification>
    {
        public Task Handle(TracedNotification @event, CancellationToken cancellationToken)
        {
            @event.Trace.Add("handler");
            return Task.CompletedTask;
        }
    }

    public class RecordUnitCommandBehavior : IEventCustomPipeline<RecordUnitCommand, Unit>
    {
        public async Task<Unit> Handle(RecordUnitCommand request, EventHandlerDelegate<Unit> next, CancellationToken cancellationToken)
        {
            request.Trace.Add("closed-behavior");
            return await next(cancellationToken);
        }
    }

    public class DualShapeBehavior : IEventCustomPipeline<RecordUnitCommand, Unit>, IEventCustomPipeline<TracedNotification>
    {
        public async Task<Unit> Handle(RecordUnitCommand request, EventHandlerDelegate<Unit> next, CancellationToken cancellationToken)
        {
            request.Trace.Add("dual-behavior");
            return await next(cancellationToken);
        }

        public async Task Handle(TracedNotification request, EventHandlerDelegate next, CancellationToken cancellationToken)
        {
            request.Trace.Add("dual-behavior");
            await next(cancellationToken);
        }
    }

    public class ExplicitlyImplementedBehavior : IEventCustomPipeline<RecordUnitCommand, Unit>, IEventCustomPipeline<TracedNotification>
    {
        async Task<Unit> IEventCustomPipeline<RecordUnitCommand, Unit>.Handle(RecordUnitCommand request, EventHandlerDelegate<Unit> next, CancellationToken cancellationToken)
        {
            request.Trace.Add("explicit-behavior");
            return await next(cancellationToken);
        }

        async Task IEventCustomPipeline<TracedNotification>.Handle(TracedNotification request, EventHandlerDelegate next, CancellationToken cancellationToken)
        {
            request.Trace.Add("explicit-behavior");
            await next(cancellationToken);
        }
    }

    public class TracingRequestBehavior<TRequest, TResponse> : IEventCustomPipeline<TRequest, TResponse>
        where TRequest : IEventRequest<TResponse>
        where TResponse : IEventResponse
    {
        public async Task<TResponse> Handle(TRequest request, EventHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            (request as ITraceableEvent)?.Trace.Add("open-behavior");
            return await next(cancellationToken);
        }
    }

    public class TracingNotificationBehavior<TRequest> : IEventCustomPipeline<TRequest>
        where TRequest : IEventRequest
    {
        public async Task Handle(TRequest request, EventHandlerDelegate next, CancellationToken cancellationToken)
        {
            (request as ITraceableEvent)?.Trace.Add("open-behavior");
            await next(cancellationToken);
        }
    }

    public class PartiallyClosedBehavior<TRequest> : IEventCustomPipeline<TRequest, Unit>
        where TRequest : IEventRequest<Unit>
    {
        public Task<Unit> Handle(TRequest request, EventHandlerDelegate<Unit> next, CancellationToken cancellationToken)
            => next(cancellationToken);
    }

    public class SwappedParametersBehavior<TResponse, TRequest> : IEventCustomPipeline<TRequest, TResponse>
        where TRequest : IEventRequest<TResponse>
        where TResponse : IEventResponse
    {
        public Task<TResponse> Handle(TRequest request, EventHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
            => next(cancellationToken);
    }

    public class NotABehavior<T>
    {
    }

    public class NotAClosedBehavior
    {
    }

    public abstract class AbstractRequestBehavior<TRequest, TResponse> : IEventCustomPipeline<TRequest, TResponse>
        where TRequest : IEventRequest<TResponse>
        where TResponse : IEventResponse
    {
        public abstract Task<TResponse> Handle(TRequest request, EventHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
    }

    public abstract class AbstractClosedBehavior : IEventCustomPipeline<RecordUnitCommand, Unit>
    {
        public abstract Task<Unit> Handle(RecordUnitCommand request, EventHandlerDelegate<Unit> next, CancellationToken cancellationToken);
    }
}
