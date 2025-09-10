namespace EventFlux
{
    public interface IEventRequest<TResponse>
        where TResponse : IEventResponse
    {

    }

    public interface IEventRequest
    {

    }
}
