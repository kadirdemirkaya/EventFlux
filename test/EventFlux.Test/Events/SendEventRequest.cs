

namespace EventFlux.Test.Events
{
    public class SendEventRequest : IEventRequest<SendEventResponse>
    {
        public string Data { get; set; }
    }
    public class SendEventResponse : IEventResponse
    {
        public string Data { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class SendEventHandler : IEventHandler<SendEventRequest, SendEventResponse>
    {
        public async Task<SendEventResponse> Handle(SendEventRequest @event)
        {
            return new SendEventResponse { Data = @event.Data };
        }
    }
}
