using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Options
{
    /// <summary>
    /// Configuration options for EventFlux behavior.
    /// </summary>
    public class EventFluxOptions
    {
        private TimeSpan? _timeout;

        /// <summary>
        /// Gets or sets a value indicating whether a new dependency injection scope is created for each event dispatch.
        /// When <c>false</c> (default in v2.0+), handlers and behaviors are resolved directly from the ambient service provider.
        /// When <c>true</c>, each event runs in an isolated child scope.
        /// </summary>
        public bool CreateScopePerEvent { get; set; } = false;

        /// <summary>
        /// Gets or sets the execution strategy for notification handlers during publish operations.
        /// When <see cref="PublishStrategy.Parallel"/> (default), handlers run concurrently via <see cref="System.Threading.Tasks.Task.WhenAll(System.Collections.Generic.IEnumerable{System.Threading.Tasks.Task})"/>.
        /// When <see cref="PublishStrategy.Sequential"/>, handlers run one after another in order of priority.
        /// </summary>
        public PublishStrategy PublishStrategy { get; set; } = PublishStrategy.Parallel;

        /// <summary>
        /// Gets or sets the lifetime used when registering event handlers.
        /// Defaults to <see cref="ServiceLifetime.Transient"/>.
        /// </summary>
        public ServiceLifetime HandlerLifetime { get; set; } = ServiceLifetime.Transient;

        /// <summary>
        /// Gets or sets the time limit applied by the built-in timeout behavior registered with <c>AddEventTimeout()</c>.
        /// </summary>
        /// <remarks>
        /// When <c>null</c> (default), the timeout behavior keeps its built-in limit of 30 seconds.
        /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> disables the limit.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is zero, negative (other than
        /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>), or greater than <see cref="int.MaxValue"/> milliseconds.</exception>
        public TimeSpan? Timeout
        {
            get => _timeout;
            set
            {
                if (value is TimeSpan timeout
                    && timeout != System.Threading.Timeout.InfiniteTimeSpan
                    && (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), timeout, "Timeout must be a positive duration of at most int.MaxValue milliseconds, or Timeout.InfiniteTimeSpan.");
                }

                _timeout = value;
            }
        }
    }
}
