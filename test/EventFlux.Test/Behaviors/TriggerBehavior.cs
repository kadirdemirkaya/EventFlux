
using EventFlux.Abstractions;
using EventFlux.Delegates;

namespace EventFlux.Test.Behaviors
{
    public class TriggerBehavior<TRequest> : IEventCustomPipeline<TRequest>
        where TRequest : IEventRequest
    {
        public static int Count = 0;

        public async Task Handle(TRequest request, EventHandlerDelegate next, CancellationToken cancellationToken)
        {
            Console.WriteLine("TriggerBehavior", ++Count);
        }
    }
}
