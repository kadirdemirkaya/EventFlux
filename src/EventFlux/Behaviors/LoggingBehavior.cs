using EventFlux.Abstractions;
using EventFlux.Delegates;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace EventFlux.Behaviors
{
    /// <summary>
    /// Pipeline behavior that logs notification execution start, completion, failure, and duration.
    /// </summary>
    /// <typeparam name="TRequest">The notification event request type.</typeparam>
    public class LoggingBehavior<TRequest> : IEventCustomPipeline<TRequest>
        where TRequest : IEventRequest
    {
        private readonly ILogger<LoggingBehavior<TRequest>> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingBehavior{TRequest}"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public LoggingBehavior(ILogger<LoggingBehavior<TRequest>> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task Handle(TRequest request, EventHandlerDelegate next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var handlerName = this.GetType().Name;

            _logger.LogInformation(
                "[Event Started] Handler: {Handler}, Event: {EventName}, Request Data: {@Request}",
                handlerName, requestName, request
            );

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await next(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(
                    ex,
                    "[Event Failed] Handler: {Handler}, Event: {EventName}, Execution Time: {ElapsedMs} ms",
                    handlerName, requestName, stopwatch.ElapsedMilliseconds
                );

                throw;
            }

            stopwatch.Stop();

            _logger.LogInformation(
                "[Event Completed] Handler: {Handler}, Event: {EventName}, Execution Time: {ElapsedMs} ms",
                handlerName, requestName, stopwatch.ElapsedMilliseconds
            );
        }
    }

    /// <summary>
    /// Pipeline behavior that logs request-response execution start, completion, failure, and duration.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    public class LoggingBehavior<TRequest, TResponse> : IEventCustomPipeline<TRequest, TResponse>
        where TRequest : IEventRequest<TResponse>
        where TResponse : IEventResponse
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingBehavior{TRequest, TResponse}"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<TResponse> Handle(TRequest request, EventHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            TResponse? response = default;

            var requestName = typeof(TRequest).Name;
            var handlerName = this.GetType().Name;

            _logger.LogInformation(
                "[Event Started] Handler: {Handler}, Event: {EventName}, Request Data: {@Request}",
                handlerName, requestName, request
            );

            var stopwatch = Stopwatch.StartNew();

            try
            {
                response = await next(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(
                    ex,
                    "[Event Failed] Handler: {Handler}, Event: {EventName}, Execution Time: {ElapsedMs} ms",
                    handlerName, requestName, stopwatch.ElapsedMilliseconds
                );

                throw;
            }

            stopwatch.Stop();

            _logger.LogInformation(
                "[Event Completed] Handler: {Handler}, Event: {EventName}, Execution Time: {ElapsedMs} ms",
                handlerName, requestName, stopwatch.ElapsedMilliseconds
            );

            return response;
        }
    }
}
