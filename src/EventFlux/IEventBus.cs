
namespace EventFlux
{
    public interface IEventBus
    {
        void AddStackRequestEvent<TEvent>(TEvent eventRequest) where TEvent : IEventRequest;
        Task PublishAsync(IEventRequest request);
        void RemoveStackRequestEvent<TEvent>(TEvent eventRequest) where TEvent : IEventRequest;
        Task<TResponse> SendAsync<TResponse>(IEventRequest<TResponse> request) where TResponse : IEventResponse;
        Task StackEventDispatcherAsync();
    }
}