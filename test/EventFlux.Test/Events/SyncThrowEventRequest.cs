using EventFlux.Abstractions;

namespace EventFlux.Test.Events
{
    public class SyncThrowEventRequest : IEventRequest
    {
    }

    public class SyncThrowEventHandler : IEventHandler<SyncThrowEventRequest>
    {
        public const string Message = "handler threw before returning a task";

        public Task Handle(SyncThrowEventRequest request)
            => throw new InvalidOperationException(Message);
    }

    public class SyncThrowResponse : IEventResponse
    {
    }

    public class SyncThrowResponseRequest : IEventRequest<SyncThrowResponse>
    {
    }

    public class SyncThrowResponseHandler : IEventHandler<SyncThrowResponseRequest, SyncThrowResponse>
    {
        public const string Message = "response handler threw before returning a task";

        public Task<SyncThrowResponse> Handle(SyncThrowResponseRequest request)
            => throw new InvalidOperationException(Message);
    }
}
