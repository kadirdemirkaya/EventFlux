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
        /// <remarks>
        /// <para>
        /// Publishing an event that has no registered handlers completes without error. Handlers whose
        /// <c>CanHandle</c> returns <c>false</c> are skipped. Handler exceptions are never swallowed; how they
        /// surface depends on <see cref="EventFlux.Options.EventFluxOptions.PublishStrategy"/>:
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        Task PublishAsync(IEventRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a request to a single registered request handler and returns its response.
        /// </summary>
        /// <remarks>
        /// If the handler's <c>CanHandle</c> returns <c>false</c>, the handler is not invoked and the result is
        /// <c>null</c> — no exception is thrown, so callers must check the response. Exceptions thrown by the handler
        /// propagate to the caller unchanged.
        /// </remarks>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
        /// <returns>The response produced by the handler, or <c>null</c> if the handler cannot handle the request.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no handler is registered for the request type.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        Task<TResponse?> SendAsync<TResponse>(IEventRequest<TResponse> request, CancellationToken cancellationToken = default) where TResponse : IEventResponse;

        /// <summary>
        /// Sends a request that returns no value to its single registered request handler.
        /// </summary>
        /// <remarks>
        /// The request is dispatched exactly like <see cref="SendAsync{TResponse}(IEventRequest{TResponse}, CancellationToken)"/>
        /// with <see cref="Unit"/> as the response type; the <see cref="Unit"/> result is discarded. Call
        /// <c>SendAsync&lt;Unit&gt;(request)</c> explicitly to observe it.
        /// </remarks>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no handler is registered for the request type.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        Task SendAsync(IEventRequest<Unit> request, CancellationToken cancellationToken = default)
            => SendAsync<Unit>(request, cancellationToken);

        /// <summary>
        /// Dispatches all events currently accumulated in the deferred execution stack in sequence.
        /// </summary>
        /// <remarks>
        /// The queue is drained first and every event is then published in order through <see cref="PublishAsync"/>.
        /// An exception thrown while publishing one event — including an <see cref="OperationCanceledException"/>
        /// raised by a handler — is logged at error level and not rethrown; dispatch continues with the next event.
        /// Cancellation of <paramref name="cancellationToken"/> is checked before each event and is rethrown, in which
        /// case the drained events that have not been dispatched yet are discarded.
        /// </remarks>
        /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
        /// <returns>A task representing the asynchronous dispatch operation.</returns>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled between events.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the event bus was constructed without a service provider and the queue is not empty.</exception>
        Task StackEventDispatcherAsync(CancellationToken cancellationToken = default);
    }
}