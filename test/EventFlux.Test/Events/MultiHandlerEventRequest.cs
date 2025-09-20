using EventFlux.Abstractions;

namespace EventFlux.Test.Events
{
    public class MultiHandlerEventRequest : IEventRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    public class MultiHandlerEventHandler1 : IEventHandler<MultiHandlerEventRequest>
    {
        public static int HandledCount;

        public async Task Handle(MultiHandlerEventRequest request)
        {
            await Task.Delay(30);
            Interlocked.Increment(ref HandledCount);
        }
    }
    public class MultiHandlerEventHandler2 : IEventHandler<MultiHandlerEventRequest>
    {
        public static int HandledCount;

        public async Task Handle(MultiHandlerEventRequest request)
        {
            await Task.Delay(40);
            Interlocked.Increment(ref HandledCount);
        }
    }
}
