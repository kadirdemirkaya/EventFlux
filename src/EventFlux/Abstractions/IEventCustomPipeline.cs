using EventFlux.Delegates;

namespace EventFlux.Abstractions
{
    public interface IEventCustomPipeline<TRequest, TResponse>
        where TRequest : IEventRequest<TResponse>
        where TResponse : IEventResponse
    {
        Task<TResponse> Handle(
            TRequest request,
            EventHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken);
    }

    public interface IEventCustomPipeline<TRequest>
    where TRequest : IEventRequest
    {
        Task Handle(
            TRequest request,
            EventHandlerDelegate next,
            CancellationToken cancellationToken);
    }
}
