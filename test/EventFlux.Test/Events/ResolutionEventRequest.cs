using EventFlux.Abstractions;
using EventFlux.Attributes;

namespace EventFlux.Test.Events
{
    public class ResolutionEventRequest : IEventRequest
    {
        public string Data { get; set; } = string.Empty;
    }

    public class ResolutionEventHandler : IEventHandler<ResolutionEventRequest>
    {
        public static int HandledCount;

        public Task Handle(ResolutionEventRequest request)
        {
            Interlocked.Increment(ref HandledCount);

            return Task.CompletedTask;
        }
    }

    public class OrderedResolutionEventRequest : IEventRequest
    {
    }

    [HandlerOrder(2)]
    public class OrderedResolutionEventHandlerSecond : IEventHandler<OrderedResolutionEventRequest>
    {
        public static int HandledCount;

        public Task Handle(OrderedResolutionEventRequest request)
        {
            Interlocked.Increment(ref HandledCount);

            return Task.CompletedTask;
        }
    }

    [HandlerOrder(1)]
    public class OrderedResolutionEventHandlerFirst : IEventHandler<OrderedResolutionEventRequest>
    {
        public static int HandledCount;

        public Task Handle(OrderedResolutionEventRequest request)
        {
            Interlocked.Increment(ref HandledCount);

            return Task.CompletedTask;
        }
    }

    public class GatedResolutionEventRequest : IEventRequest
    {
        public bool Allow { get; set; }
    }

    public class GatedResolutionEventHandler : IEventHandler<GatedResolutionEventRequest>
    {
        public static int HandledCount;

        public bool CanHandle(GatedResolutionEventRequest @event) => @event.Allow;

        public Task Handle(GatedResolutionEventRequest request)
        {
            Interlocked.Increment(ref HandledCount);

            return Task.CompletedTask;
        }
    }
}
