using MediatR;

namespace EventBus.Benchmarks.Events
{
    public class MediatRSendResponse
    {
        public int Result { get; set; }
    }

    public class MediatRSendRequest : IRequest<MediatRSendResponse>
    {
        public int Num { get; set; }
    }

    public class MediatRSendHandler : IRequestHandler<MediatRSendRequest, MediatRSendResponse>
    {
        private static readonly Task<MediatRSendResponse> Completed =
            Task.FromResult(new MediatRSendResponse { Result = 0 });

        public Task<MediatRSendResponse> Handle(MediatRSendRequest request, CancellationToken cancellationToken) => Completed;
    }

    public class MediatRPublishNotification : INotification
    {
    }

    public class MediatRPublishHandler : INotificationHandler<MediatRPublishNotification>
    {
        public Task Handle(MediatRPublishNotification notification, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class MediatRFanOutNotification : INotification
    {
    }

    public class MediatRFanOutHandlerA : INotificationHandler<MediatRFanOutNotification>
    {
        public Task Handle(MediatRFanOutNotification notification, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class MediatRFanOutHandlerB : INotificationHandler<MediatRFanOutNotification>
    {
        public Task Handle(MediatRFanOutNotification notification, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class MediatRFanOutHandlerC : INotificationHandler<MediatRFanOutNotification>
    {
        public Task Handle(MediatRFanOutNotification notification, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
