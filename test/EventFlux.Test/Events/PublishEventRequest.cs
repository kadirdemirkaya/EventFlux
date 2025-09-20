using EventFlux.Abstractions;

namespace EventFlux.Test.Events
{
    public class PublishEventRequest : IEventRequest
    {
        public string Data { get; set; } = string.Empty;
    }
    public class PublishEventHandler : IEventHandler<PublishEventRequest>
    {
        public static int HandledCount = 0;


        public async Task Handle(PublishEventRequest request)
        {
            await Task.Delay(50);
            Interlocked.Increment(ref HandledCount);
        }
    }
}
