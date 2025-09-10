namespace EventFlux
{
    public interface IEventHandler<TRequest, TResponse>
        where TRequest : IEventRequest<TResponse>
        where TResponse : IEventResponse
    {
        bool CanHandle(TRequest @event) => true;
        Task<TResponse> Handle(TRequest @event);
    }
    public interface IEventHandler<TRequest>
      where TRequest : IEventRequest
    {
        bool CanHandle(TRequest @event) => true;
        Task Handle(TRequest @event);
    }
}
