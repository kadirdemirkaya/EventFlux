using EventFlux.Abstractions;

namespace EventFlux.Test.Events
{
    public class ExampleEventRequest : IEventRequest<ExampleEventResponse>
    {
        public int Num { get; set; }
    }

    public class ExampleEventResponse : IEventResponse
    {
        public int Result { get; set; }
    }

    public class ExampleEventHandler : IEventHandler<ExampleEventRequest, ExampleEventResponse>
    {
        public async Task<ExampleEventResponse> Handle(ExampleEventRequest request)
        {
            await Task.Delay(50);
            return new ExampleEventResponse
            {
                Result = request.Num * 2
            };
        }
    }
}
