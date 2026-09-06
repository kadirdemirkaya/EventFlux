namespace EventFlux.Abstractions
{
    /// <summary>
    /// Marker interface for a request that expects a response of type <typeparamref name="TResponse"/>.
    /// </summary>
    /// <typeparam name="TResponse">The expected response type, implementing <see cref="IEventResponse"/>.</typeparam>
    public interface IEventRequest<TResponse>
        where TResponse : IEventResponse
    {

    }

    /// <summary>
    /// Marker interface for a notification event that does not produce a response.
    /// </summary>
    public interface IEventRequest
    {

    }
}
