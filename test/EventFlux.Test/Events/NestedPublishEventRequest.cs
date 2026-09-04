using EventFlux.Abstractions;

namespace EventFlux.Test.Events
{
    public class NestedOuterEventRequest : IEventRequest
    {
    }

    public class NestedInnerEventRequest : IEventRequest
    {
    }

    public class NestedOuterEventHandler : IEventHandler<NestedOuterEventRequest>
    {
        private readonly IEventDispatcher _dispatcher;

        public NestedOuterEventHandler(IEventDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public async Task Handle(NestedOuterEventRequest request)
        {
            await Task.Delay(50);

            await _dispatcher.PublishAsync(new NestedInnerEventRequest());
        }
    }

    public class NestedInnerEventHandler : IEventHandler<NestedInnerEventRequest>
    {
        public static int HandledCount;

        public Task Handle(NestedInnerEventRequest request)
        {
            Interlocked.Increment(ref HandledCount);

            return Task.CompletedTask;
        }
    }
}
