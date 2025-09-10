using Microsoft.Extensions.Logging;

namespace EventFlux.Behaviors
{
    public class TimeoutBehavior<TRequest> : IEventCustomPipeline<TRequest>
        where TRequest : IEventRequest
    {
        private readonly ILogger<TimeoutBehavior<TRequest>> _logger;
        private readonly double _timeoutSeconds;

        public TimeoutBehavior(ILogger<TimeoutBehavior<TRequest>> logger, double timeoutSeconds = 30)
        {
            _logger = logger;
            _timeoutSeconds = timeoutSeconds;
        }

        public async Task Handle(TRequest request, EventHandlerDelegate next, CancellationToken cancellationToken)
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                var task = next(linkedCts.Token);
                var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, linkedCts.Token));

                if (completedTask != task)
                {
                    _logger.LogWarning("[Timeout] Event processing exceeded the allowed time limit.");
                    throw new OperationCanceledException("Timeout occurred");
                }

                await task;
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested)
                    _logger.LogWarning("[Timeout] Event processing exceeded the allowed time limit.");
                throw;
            }
        }
    }


    public class TimeoutBehavior<TRequest, TResponse>
      : IEventCustomPipeline<TRequest, TResponse>
      where TRequest : IEventRequest<TResponse>
      where TResponse : IEventResponse
    {
        private readonly ILogger<TimeoutBehavior<TRequest, TResponse>> _logger;
        private readonly double _timeoutSeconds;

        public TimeoutBehavior(ILogger<TimeoutBehavior<TRequest, TResponse>> logger, double timeoutSeconds = 30)
        {
            _logger = logger;
            _timeoutSeconds = timeoutSeconds;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            EventHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                var task = next(linkedCts.Token);

                var timeoutTask = Task.Delay(Timeout.Infinite, linkedCts.Token);

                var completedTask = await Task.WhenAny(task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning("[Timeout] Event processing exceeded the allowed time limit.");
                    linkedCts.Cancel();
                    throw new OperationCanceledException("Timeout occurred");
                }

                return await task;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                _logger.LogWarning("[Timeout] Event processing exceeded the allowed time limit.");
                throw;
            }
        }
    }

}
