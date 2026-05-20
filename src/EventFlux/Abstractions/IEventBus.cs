using System.Threading;
using System.Threading.Tasks;

namespace EventFlux.Abstractions
{
    public interface IEventBus
    {
        void AddStackRequestEvent<TEvent>(TEvent eventRequest) where TEvent : IEventRequest;
        Task PublishAsync(IEventRequest request, CancellationToken cancellationToken = default);
        //void RemoveStackRequestEvent<TEvent>(TEvent eventRequest) where TEvent : IEventRequest;
        Task<TResponse> SendAsync<TResponse>(IEventRequest<TResponse> request, CancellationToken cancellationToken = default) where TResponse : IEventResponse;
        Task StackEventDispatcherAsync(CancellationToken cancellationToken = default);
    }
}