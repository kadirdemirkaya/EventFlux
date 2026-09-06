using EventFlux.Abstractions;
using EventFlux.Delegates;
using Microsoft.Extensions.Logging;

namespace EventFlux.Behaviors
{
    /// <summary>
    /// Pipeline behavior that cancels notification execution and throws <see cref="OperationCanceledException"/> if processing exceeds the configured timeout.
    /// </summary>
    /// <typeparam name="TRequest">The notification event request type.</typeparam>
    public class TimeoutBehavior<TRequest> : IEventCustomPipeline<TRequest>
        where TRequest : IEventRequest
    {
        private readonly ILogger<TimeoutBehavior<TRequest>> _logger;
        private readonly double _timeoutSeconds;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutBehavior{TRequest}"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="timeoutSeconds">The timeout duration in seconds (default: 30).</param>
        public TimeoutBehavior(ILogger<TimeoutBehavior<TRequest>> logger, double timeoutSeconds = 30)
        {
            _logger = logger;
            _timeoutSeconds = timeoutSeconds;
        }

        /// <inheritdoc />
        public async Task Handle(TRequest request, EventHandlerDelegate next, CancellationToken cancellationToken)
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                var task = next(linkedCts.Token);
                var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, linkedCts.Token)).ConfigureAwait(false);

                if (completedTask != task)
                {
                    _logger.LogWarning("[Timeout] Event processing exceeded the allowed time limit.");
                    throw new OperationCanceledException("Timeout occurred");
                }

                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested)
                    _logger.LogWarning("[Timeout] Event processing exceeded the allowed time limit.");
                throw;
            }
        }
    }


    /// <summary>
    /// Pipeline behavior that cancels request-response execution and throws <see cref="OperationCanceledException"/> if processing exceeds the configured timeout.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    public class TimeoutBehavior<TRequest, TResponse>
      : IEventCustomPipeline<TRequest, TResponse>
      where TRequest : IEventRequest<TResponse>
      where TResponse : IEventResponse
    {
        private readonly ILogger<TimeoutBehavior<TRequest, TResponse>> _logger;
        private readonly double _timeoutSeconds;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutBehavior{TRequest, TResponse}"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="timeoutSeconds">The timeout duration in seconds (default: 30).</param>
        public TimeoutBehavior(ILogger<TimeoutBehavior<TRequest, TResponse>> logger, double timeoutSeconds = 30)
        {
            _logger = logger;
            _timeoutSeconds = timeoutSeconds;
        }

        /// <inheritdoc />
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

                var completedTask = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);

                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning("[Timeout] Event processing exceeded the allowed time limit.");
                    linkedCts.Cancel();
                    throw new OperationCanceledException("Timeout occurred");
                }

                return await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                _logger.LogWarning("[Timeout] Event processing exceeded the allowed time limit.");
                throw;
            }
        }
    }

}
