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

        public async Task Handle(NestedOuterEventRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(50, cancellationToken);

            await _dispatcher.PublishAsync(new NestedInnerEventRequest(), cancellationToken);
        }
    }

    public class NestedInnerEventHandler : IEventHandler<NestedInnerEventRequest>
    {
        public static int HandledCount;

        public Task Handle(NestedInnerEventRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref HandledCount);

            return Task.CompletedTask;
        }
    }
}
