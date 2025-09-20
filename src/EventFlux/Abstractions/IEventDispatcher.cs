namespace EventFlux.Abstractions
{
    public interface IEventDispatcher
    {
        Task PublishAsync(IEventRequest request, CancellationToken cancellationToken = default);
        Task<TResponse> SendAsync<TResponse>(IEventRequest<TResponse> request, CancellationToken cancellationToken = default) where TResponse : IEventResponse;
    }
}