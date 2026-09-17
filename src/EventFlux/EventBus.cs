using EventFlux.Abstractions;
using EventFlux.Internal;
using EventFlux.Options;
using EventFlux.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EventFlux
{
    /// <summary>
    /// Default scoped implementation of <see cref="IEventBus"/> providing request-response and notification dispatching.
    /// </summary>
    [RequiresDynamicCode(AotMessages.DynamicCode)]
    [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
    public class EventBus : IEventBus
    {
        private readonly ILogger<EventBus>? _logger;
        private readonly IServiceProvider? _serviceProvider;
        private readonly EventFluxOptions _options;
        private readonly EventStackService _eventStackDictionaryService;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class.
        /// </summary>
        public EventBus()
        {
            _options = new EventFluxOptions();
            _eventStackDictionaryService = new EventStackService();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class with DI service provider and logger.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
        /// <param name="logger">Logger instance.</param>
        public EventBus(IServiceProvider serviceProvider, ILogger<EventBus> logger)
            : this(serviceProvider, logger, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class with DI service provider, logger, and options.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="options">Configuration options.</param>
        public EventBus(IServiceProvider serviceProvider, ILogger<EventBus> logger, EventFluxOptions? options)
            : this(serviceProvider, logger, options, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class with DI service provider, logger, options, and event stack service.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="options">Configuration options.</param>
        /// <param name="eventStackService">Event stack service for deferred batch events.</param>
        public EventBus(IServiceProvider serviceProvider, ILogger<EventBus> logger, EventFluxOptions? options, EventStackService? eventStackService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _options = options ?? serviceProvider?.GetService<EventFluxOptions>() ?? new EventFluxOptions();
            _eventStackDictionaryService = eventStackService ?? serviceProvider?.GetService<EventStackService>() ?? new EventStackService();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class with assemblies.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
        /// <param name="assemblies">Ignored: handler discovery no longer scans assemblies passed to this constructor.</param>
        /// <param name="logger">Logger instance.</param>
        [Obsolete("The 'assemblies' parameter is ignored; use EventBus(IServiceProvider, ILogger<EventBus>, EventFluxOptions?, EventStackService?) or resolve IEventBus from DI after AddEventBus().")]
        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, ILogger<EventBus> logger)
            : this(serviceProvider, logger, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class with services.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
        /// <param name="assemblies">Ignored: handler discovery no longer scans assemblies passed to this constructor.</param>
        /// <param name="dictionaryService">Ignored: not used by this constructor.</param>
        /// <param name="eventDictionaryMapService">Ignored: not used by this constructor.</param>
        /// <param name="logger">Logger instance.</param>
        [Obsolete("The 'assemblies', 'dictionaryService' and 'eventDictionaryMapService' parameters are ignored; use EventBus(IServiceProvider, ILogger<EventBus>, EventFluxOptions?, EventStackService?) or resolve IEventBus from DI after AddEventBus().")]
        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, EventService dictionaryService, EventMapService eventDictionaryMapService, ILogger<EventBus> logger)
            : this(serviceProvider, logger, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class with handlers list.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
        /// <param name="assemblies">Ignored: handler discovery no longer scans assemblies passed to this constructor.</param>
        /// <param name="dictionaryService">Ignored: not used by this constructor.</param>
        /// <param name="eventDictionaryMapService">Ignored: not used by this constructor.</param>
        /// <param name="handlers">Ignored: not used by this constructor.</param>
        /// <param name="logger">Logger instance.</param>
        [Obsolete("The 'assemblies', 'dictionaryService', 'eventDictionaryMapService' and 'handlers' parameters are ignored; use EventBus(IServiceProvider, ILogger<EventBus>, EventFluxOptions?, EventStackService?) or resolve IEventBus from DI after AddEventBus().")]
        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, EventService dictionaryService, EventMapService eventDictionaryMapService, IEnumerable<Type> handlers, ILogger<EventBus> logger)
            : this(serviceProvider, logger, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class with full parameters including options.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
        /// <param name="assemblies">Ignored: handler discovery no longer scans assemblies passed to this constructor.</param>
        /// <param name="dictionaryService">Ignored: not used by this constructor.</param>
        /// <param name="eventDictionaryMapService">Ignored: not used by this constructor.</param>
        /// <param name="handlers">Ignored: not used by this constructor.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="options">Configuration options.</param>
        [Obsolete("The 'assemblies', 'dictionaryService', 'eventDictionaryMapService' and 'handlers' parameters are ignored; use EventBus(IServiceProvider, ILogger<EventBus>, EventFluxOptions?, EventStackService?) or resolve IEventBus from DI after AddEventBus().")]
        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, EventService dictionaryService, EventMapService eventDictionaryMapService, IEnumerable<Type> handlers, ILogger<EventBus> logger, EventFluxOptions? options)
            : this(serviceProvider, logger, options)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class with full parameters including event stack service.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
        /// <param name="assemblies">Ignored: handler discovery no longer scans assemblies passed to this constructor.</param>
        /// <param name="dictionaryService">Ignored: not used by this constructor.</param>
        /// <param name="eventDictionaryMapService">Ignored: not used by this constructor.</param>
        /// <param name="handlers">Ignored: not used by this constructor.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="options">Configuration options.</param>
        /// <param name="eventStackService">Event stack service for deferred batch events.</param>
        [Obsolete("The 'assemblies', 'dictionaryService', 'eventDictionaryMapService' and 'handlers' parameters are ignored; use EventBus(IServiceProvider, ILogger<EventBus>, EventFluxOptions?, EventStackService?) or resolve IEventBus from DI after AddEventBus().")]
        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, EventService dictionaryService, EventMapService eventDictionaryMapService, IEnumerable<Type> handlers, ILogger<EventBus> logger, EventFluxOptions? options, EventStackService? eventStackService)
            : this(serviceProvider, logger, options, eventStackService)
        {
        }

        /// <inheritdoc />
        public async Task<TResponse?> SendAsync<TResponse>(
            IEventRequest<TResponse> request,
            CancellationToken cancellationToken = default)
            where TResponse : IEventResponse
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            var serviceProvider = RequireServiceProvider();

            if (_options.CreateScopePerEvent)
            {
                using var scope = serviceProvider.CreateScope();
                return await DispatchSendAsync<TResponse>(scope.ServiceProvider, request, cancellationToken).ConfigureAwait(false);
            }

            return await DispatchSendAsync<TResponse>(serviceProvider, request, cancellationToken).ConfigureAwait(false);
        }

        private async Task<TResponse?> DispatchSendAsync<TResponse>(
            IServiceProvider serviceProvider,
            IEventRequest<TResponse> request,
            CancellationToken cancellationToken)
            where TResponse : IEventResponse
        {
            var handlerType = DispatchTypeCache.RequestHandlerType(request.GetType(), typeof(TResponse));

            var handler = serviceProvider.GetRequiredService(handlerType);

            var accessor = DispatchTypeCache.ForInterface(handlerType);
            if (accessor.CanHandle != null && accessor.CanHandle(handler, request) is bool result && !result)
            {
                _logger?.LogInformation("Handler {Handler} cannot handle event {EventName}. Skipping.", handlerType.Name, request.GetType().Name);
                return default;
            }

            var task = (Task<TResponse>)accessor.Handle(handler, request, cancellationToken);

            return await task.ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task PublishAsync(
            IEventRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            var serviceProvider = RequireServiceProvider();

            if (_options.CreateScopePerEvent)
            {
                using var scope = serviceProvider.CreateScope();
                await DispatchPublishAsync(scope.ServiceProvider, request, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await DispatchPublishAsync(serviceProvider, request, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task DispatchPublishAsync(
            IServiceProvider serviceProvider,
            IEventRequest request,
            CancellationToken cancellationToken)
        {
            var eventType = request.GetType();

            var handlerType = DispatchTypeCache.NotificationHandlerType(eventType);

            var invocations = HandlerInvocationBuilder.Build(
                serviceProvider.GetServices(handlerType),
                eventType,
                out var invocationCount);

            if (invocationCount == 0)
                return;

            if (_options.PublishStrategy == PublishStrategy.Sequential)
            {
                for (var i = 0; i < invocationCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var entry = invocations[i];

                    if (entry.Accessor.CanHandle != null && !entry.Accessor.CanHandle(entry.Handler, request))
                        continue;

                    await ((Task)entry.Accessor.Handle(entry.Handler, request, cancellationToken)).ConfigureAwait(false);
                }
            }
            else
            {
                var tasks = new Task[invocationCount];

                for (var i = 0; i < invocationCount; i++)
                {
                    tasks[i] = InvokeInvocationAsync(invocations[i], request, cancellationToken);
                }

                await ParallelPublish.WhenAllAsync(tasks).ConfigureAwait(false);
            }
        }

        private static async Task InvokeInvocationAsync(
            HandlerInvocation invocation,
            IEventRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (invocation.Accessor.CanHandle != null && !invocation.Accessor.CanHandle(invocation.Handler, request))
                return;

            await ((Task)invocation.Accessor.Handle(invocation.Handler, request, cancellationToken)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task StackEventDispatcherAsync(CancellationToken cancellationToken = default)
        {
            var events = _eventStackDictionaryService.Drain();
            if (events.Count == 0)
                return;

            RequireServiceProvider();

            foreach (var evt in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await PublishAsync(evt, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Stack dispatch failed for {EventName}", evt.GetType().Name);
                }
            }
        }

        private IServiceProvider RequireServiceProvider()
        {
            return _serviceProvider ?? throw new InvalidOperationException(
                $"{nameof(EventBus)} was created without a service provider. Resolve {nameof(IEventBus)} from the dependency injection container after calling AddEventBus(), instead of constructing it directly.");
        }

        /// <inheritdoc />
        public void AddStackRequestEvent<TEvent>(TEvent eventRequest) where TEvent : IEventRequest
            => _eventStackDictionaryService.AddEventRequest(eventRequest);
    }
}
