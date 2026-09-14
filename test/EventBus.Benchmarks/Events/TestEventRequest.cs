using EventFlux.Abstractions;

namespace EventBus.Benchmarks.Events
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
        public async Task<TestEventResponse> Handle(TestEventRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(50, cancellationToken); // Simulate work
            return new TestEventResponse
            {
                ABS = $"Processed at {request.DateTime}"
            };
        }
    }
}
