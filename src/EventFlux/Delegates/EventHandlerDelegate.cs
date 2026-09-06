namespace EventFlux.Delegates
{
    /// <summary>
    /// Represents an asynchronous continuation delegate in a request-response pipeline.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
    /// <returns>A task that produces the response.</returns>
    public delegate Task<TResponse> EventHandlerDelegate<TResponse>(CancellationToken cancellationToken);

    /// <summary>
    /// Represents an asynchronous continuation delegate in a notification pipeline.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel execution.</param>
    /// <returns>A task representing the continuation operation.</returns>
    public delegate Task EventHandlerDelegate(CancellationToken cancellationToken);
}
