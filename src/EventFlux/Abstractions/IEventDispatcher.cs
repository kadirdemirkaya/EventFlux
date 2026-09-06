namespace EventFlux.Abstractions
{
    /// <summary>
    /// Defines the contract for an event dispatcher that executes pipeline behaviors
    /// (<see cref="IEventCustomPipeline{TRequest}"/> and <see cref="IEventCustomPipeline{TRequest, TResponse}"/>)
    /// around handlers.
    /// </summary>
    public interface IEventDispatcher
    {
        /// <summary>
        /// Publishes a notification event through the pipeline behavior chain to all registered handlers.
        /// </summary>
        /// <param name="request">The notification event to publish.</param>
        /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
        /// <returns>A task representing the asynchronous publish operation.</returns>
        Task PublishAsync(IEventRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a request through the pipeline behavior chain to its registered handler and returns the response.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
        /// <returns>The response produced by the handler.</returns>
        Task<TResponse> SendAsync<TResponse>(IEventRequest<TResponse> request, CancellationToken cancellationToken = default) where TResponse : IEventResponse;
    }
}