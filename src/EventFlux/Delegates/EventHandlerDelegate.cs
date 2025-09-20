namespace EventFlux.Delegates
{
    public delegate Task<TResponse> EventHandlerDelegate<TResponse>(CancellationToken cancellationToken);

    public delegate Task EventHandlerDelegate(CancellationToken cancellationToken);
}
