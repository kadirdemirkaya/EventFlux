namespace EventFlux.Abstractions
{
    /// <summary>
    /// Defines a handler for a request that produces a response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request being handled.</typeparam>
    /// <typeparam name="TResponse">The type of the response produced.</typeparam>
    public interface IEventHandler<TRequest, TResponse>
        where TRequest : IEventRequest<TResponse>
        where TResponse : IEventResponse
    {
        /// <summary>
        /// Determines whether the handler can process the given request. Default returns <c>true</c>.
        /// </summary>
        /// <param name="event">The incoming event request.</param>
        /// <returns><c>true</c> if the handler can process the request; otherwise <c>false</c>.</returns>
        bool CanHandle(TRequest @event) => true;

        /// <summary>
        /// Handles the request and returns a response asynchronously.
        /// </summary>
        /// <param name="event">The incoming event request.</param>
        /// <returns>The response produced by the handler.</returns>
        Task<TResponse> Handle(TRequest @event) => Handle(@event, CancellationToken.None);

        /// <summary>
        /// Handles the request and returns a response asynchronously with cancellation token support.
        /// </summary>
        /// <param name="event">The incoming event request.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The response produced by the handler.</returns>
        Task<TResponse> Handle(TRequest @event, CancellationToken cancellationToken) => Handle(@event);
    }

    /// <summary>
    /// Defines a handler for a notification event that does not produce a response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the notification event being handled.</typeparam>
    public interface IEventHandler<TRequest>
      where TRequest : IEventRequest
    {
        /// <summary>
        /// Determines whether the handler can process the given notification event. Default returns <c>true</c>.
        /// </summary>
        /// <param name="event">The incoming notification event.</param>
        /// <returns><c>true</c> if the handler can process the event; otherwise <c>false</c>.</returns>
        bool CanHandle(TRequest @event) => true;

        /// <summary>
        /// Handles the notification event asynchronously.
        /// </summary>
        /// <param name="event">The incoming notification event.</param>
        /// <returns>A task representing the asynchronous handle operation.</returns>
        Task Handle(TRequest @event) => Handle(@event, CancellationToken.None);

        /// <summary>
        /// Handles the notification event asynchronously with cancellation token support.
        /// </summary>
        /// <param name="event">The incoming notification event.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous handle operation.</returns>
        Task Handle(TRequest @event, CancellationToken cancellationToken) => Handle(@event);
    }
}
