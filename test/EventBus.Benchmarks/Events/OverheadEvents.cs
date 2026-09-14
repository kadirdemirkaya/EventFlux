using EventFlux.Abstractions;

namespace EventBus.Benchmarks.Events
{
    public class OverheadResponse : IEventResponse
    {
        public int Result { get; set; }
    }

    public class OverheadSendRequest : IEventRequest<OverheadResponse>
    {
        public int Num { get; set; }
    }

    public class OverheadSendHandler : IEventHandler<OverheadSendRequest, OverheadResponse>
    {
        private static readonly Task<OverheadResponse> Completed =
            Task.FromResult(new OverheadResponse { Result = 0 });

        public Task<OverheadResponse> Handle(OverheadSendRequest request, CancellationToken cancellationToken = default) => Completed;
    }

    public class OverheadPublishRequest : IEventRequest
    {
    }

    public class OverheadPublishHandler : IEventHandler<OverheadPublishRequest>
    {
        public Task Handle(OverheadPublishRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public class OverheadFanOutRequest : IEventRequest
    {
    }

    public class OverheadFanOutHandlerA : IEventHandler<OverheadFanOutRequest>
    {
        public Task Handle(OverheadFanOutRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public class OverheadFanOutHandlerB : IEventHandler<OverheadFanOutRequest>
    {
        public Task Handle(OverheadFanOutRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public class OverheadFanOutHandlerC : IEventHandler<OverheadFanOutRequest>
    {
        public Task Handle(OverheadFanOutRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
