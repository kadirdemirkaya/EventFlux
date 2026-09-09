using System.Threading;
using System.Threading.Tasks;

namespace EventFlux.Abstractions
{
    /// <summary>
    /// Defines the contract for an event bus supporting point-to-point requests (Send),
    /// multi-cast notifications (Publish), and deferred/stacked event batch dispatching.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Queues an event request to the deferred execution stack.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event request.</typeparam>
        /// <param name="eventRequest">The event request to enqueue.</param>
        void AddStackRequestEvent<TEvent>(TEvent eventRequest) where TEvent : IEventRequest;

        /// <summary>
        /// Publishes an event to all registered notification handlers concurrently.
        /// </summary>
        /// <param name="request">The notification event to publish.</param>
        /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
        /// <returns>A task representing the asynchronous publish operation.</returns>
        Task PublishAsync(IEventRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a request to a single registered request handler and returns its response.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
        /// <returns>The response produced by the handler, or <c>null</c> if the handler cannot handle the request.</returns>
        Task<TResponse?> SendAsync<TResponse>(IEventRequest<TResponse> request, CancellationToken cancellationToken = default) where TResponse : IEventResponse;

        /// <summary>
        /// Dispatches all events currently accumulated in the deferred execution stack in sequence.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
        /// <returns>A task representing the asynchronous dispatch operation.</returns>
        Task StackEventDispatcherAsync(CancellationToken cancellationToken = default);
    }
}