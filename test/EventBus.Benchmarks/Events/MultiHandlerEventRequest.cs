using EventFlux.Abstractions;
using EventFlux.Attributes;

namespace EventBus.Benchmarks.Events
{
    public class MultiHandlerEventRequest : IEventRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    [HandlerOrder(2)]
    public class MultiHandlerEventHandler1 : IEventHandler<MultiHandlerEventRequest>
    {
        public static int HandledCount;

        public async Task Handle(MultiHandlerEventRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(30, cancellationToken);
            Interlocked.Increment(ref HandledCount);
        }
    }

    [HandlerOrder(1)]
    public class MultiHandlerEventHandler2 : IEventHandler<MultiHandlerEventRequest>
    {
        public static int HandledCount;

        public async Task Handle(MultiHandlerEventRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(40, cancellationToken);
            Interlocked.Increment(ref HandledCount);
        }
    }
}
