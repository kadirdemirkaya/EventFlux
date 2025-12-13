using EventFlux.Abstractions;

namespace EventBus.Benchmarks.Events
{

    public class UnhandledEventRequest : IEventRequest<UnhandledEventResponse>
    {
    }

    public class UnhandledEventResponse : IEventResponse
    {
    }
}
