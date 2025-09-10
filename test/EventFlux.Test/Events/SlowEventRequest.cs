namespace EventFlux.Test.Events
{
    public class SlowEventRequest : IEventRequest<SlowEventResponse>
    {
    }

    public class SlowEventResponse : IEventResponse
    {
        public string Result { get; set; } = string.Empty;
    }

    public class SlowEventHandler : IEventHandler<SlowEventRequest, SlowEventResponse>
    {
        public async Task<SlowEventResponse> Handle(SlowEventRequest request)
        {
            Console.WriteLine("SlowEventHandler: Starting slow operation...");
            await Task.Delay(31000); // Simulate slow operation
            Console.WriteLine("SlowEventHandler: Completed slow operation");
            return new SlowEventResponse { Result = "Completed" };
        }
    }
}
