namespace EventFlux.Abstractions
{
    public interface IEventHandler<TRequest, TResponse>
        where TRequest : IEventRequest<TResponse>
        where TResponse : IEventResponse
    {
        int Priority => 0;
        bool CanHandle(TRequest @event) => true;
        Task<TResponse> Handle(TRequest @event);
    }
    public interface IEventHandler<TRequest>
      where TRequest : IEventRequest
    {
        int Priority => 0;
        bool CanHandle(TRequest @event) => true;
        Task Handle(TRequest @event);
    }
}
