using EventFlux.Abstractions;

namespace EventFlux.Test.Events
{
    public class TestEventRequest : IEventRequest<TestEventResponse>
    {
        public string DateTime { get; set; } = string.Empty;
    }

    public class TestEventResponse : IEventResponse
    {
        public string ABS { get; set; } = string.Empty;
    }

    public class TestEventHandler : IEventHandler<TestEventRequest, TestEventResponse>
    {
        public async Task<TestEventResponse> Handle(TestEventRequest request)
        {
            await Task.Delay(50); // Simulate work
            return new TestEventResponse
            {
                ABS = $"Processed at {request.DateTime}"
            };
        }
    }
}
