namespace EventFlux.Test.Events
{

    public class UnhandledEventRequest : IEventRequest<UnhandledEventResponse>
    {
    }

    public class UnhandledEventResponse : IEventResponse
    {
    }
}
