using EventFlux.Delegates;

namespace EventFlux.Abstractions
{
    /// <summary>
    /// Defines a custom pipeline behavior executed around request-response handlers.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request being handled.</typeparam>
    /// <typeparam name="TResponse">The type of the response produced.</typeparam>
    public interface IEventCustomPipeline<TRequest, TResponse>
        where TRequest : IEventRequest<TResponse>
        where TResponse : IEventResponse
    {
        /// <summary>
        /// Pipeline behavior execution method wrapping the next step in the pipeline.
        /// </summary>
        /// <param name="request">The incoming event request.</param>
        /// <param name="next">Delegate to invoke the next behavior or handler in the pipeline.</param>
        /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
        /// <returns>The response produced by the pipeline.</returns>
        Task<TResponse> Handle(
            TRequest request,
            EventHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Defines a custom pipeline behavior executed around notification handlers.
    /// </summary>
    /// <typeparam name="TRequest">The type of the notification event being handled.</typeparam>
    public interface IEventCustomPipeline<TRequest>
    where TRequest : IEventRequest
    {
        /// <summary>
        /// Pipeline behavior execution method wrapping the next step in the pipeline.
        /// </summary>
        /// <param name="request">The incoming notification event.</param>
        /// <param name="next">Delegate to invoke the next behavior or handler in the pipeline.</param>
        /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Handle(
            TRequest request,
            EventHandlerDelegate next,
            CancellationToken cancellationToken);
    }
}
