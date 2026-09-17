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
        /// <remarks>
        /// <para>
        /// Publishing an event that has no registered handlers completes without error and without running pipeline
        /// behaviors. Handlers whose <c>CanHandle</c> returns <c>false</c> are skipped. Handler exceptions are never
        /// swallowed by the dispatcher and pass back through the pipeline behaviors; how they surface depends on
        /// <see cref="EventFlux.Options.EventFluxOptions.PublishStrategy"/>:
        /// </para>
        /// <list type="bullet">
        /// <item><description><see cref="EventFlux.Options.PublishStrategy.Parallel"/> (default): every handler is started.
        /// Once all of them have finished, the exception of the first failing handler in handler order is rethrown;
        /// exceptions of any other failing handlers are not observed.</description></item>
        /// <item><description><see cref="EventFlux.Options.PublishStrategy.Sequential"/>: the first failing handler's exception is
        /// rethrown immediately and the remaining handlers are not invoked.</description></item>
        /// </list>
        /// </remarks>
        /// <param name="request">The notification event to publish.</param>
        /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
        /// <returns>A task representing the asynchronous publish operation.</returns>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled, or when a timeout behavior registered with <c>AddEventTimeout()</c> expires.</exception>
        Task PublishAsync(IEventRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a request through the pipeline behavior chain to its registered handler and returns the response.
        /// </summary>
        /// <remarks>
        /// If the handler's <c>CanHandle</c> returns <c>false</c>, the handler is not invoked and the result is
        /// <c>null</c> — no exception is thrown, and pipeline behaviors still run around the skipped handler.
        /// Exceptions thrown by the handler pass back through the pipeline behaviors and reach the caller unless a
        /// behavior handles them.
        /// </remarks>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
        /// <returns>The response produced by the handler, or <c>null</c> if the handler cannot handle the request.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no handler is registered for the request type.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the handler or a pipeline behavior observes cancellation of <paramref name="cancellationToken"/>, or when a timeout behavior registered with <c>AddEventTimeout()</c> expires.</exception>
        Task<TResponse?> SendAsync<TResponse>(IEventRequest<TResponse> request, CancellationToken cancellationToken = default) where TResponse : IEventResponse;

        /// <summary>
        /// Sends a request that returns no value through the pipeline behavior chain to its registered handler.
        /// </summary>
        /// <remarks>
        /// The request is dispatched exactly like <see cref="SendAsync{TResponse}(IEventRequest{TResponse}, CancellationToken)"/>
        /// with <see cref="Unit"/> as the response type, so <see cref="IEventCustomPipeline{TRequest, TResponse}"/> behaviors
        /// run around the handler; the <see cref="Unit"/> result is discarded.
        /// </remarks>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no handler is registered for the request type.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the handler or a pipeline behavior observes cancellation of <paramref name="cancellationToken"/>, or when a timeout behavior registered with <c>AddEventTimeout()</c> expires.</exception>
        Task SendAsync(IEventRequest<Unit> request, CancellationToken cancellationToken = default)
            => SendAsync<Unit>(request, cancellationToken);
    }
}