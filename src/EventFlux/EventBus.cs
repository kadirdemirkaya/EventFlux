using EventFlux.Abstractions;
using EventFlux.Internal;
using EventFlux.Options;
using EventFlux.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EventFlux
{
    /// <summary>
    /// Default scoped implementation of <see cref="IEventBus"/> providing request-response and notification dispatching.
    /// </summary>
    public class EventBus : IEventBus
    {
        private readonly ILogger<EventBus>? _logger;
        private readonly IServiceProvider? _serviceProvider;
        private readonly EventFluxOptions _options;
        private readonly EventStackService _eventStackDictionaryService = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class.
        /// </summary>
        public EventBus()
        {
            _options = new EventFluxOptions();
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
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _options = options ?? serviceProvider?.GetService<EventFluxOptions>() ?? new EventFluxOptions();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class with assemblies.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
        /// <param name="assemblies">Assemblies containing handlers.</param>
        /// <param name="logger">Logger instance.</param>
        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, ILogger<EventBus> logger)
            : this(serviceProvider, logger, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class with services.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
        /// <param name="assemblies">Assemblies containing handlers.</param>
        /// <param name="dictionaryService">Event service registry.</param>
        /// <param name="eventDictionaryMapService">Event map service registry.</param>
        /// <param name="logger">Logger instance.</param>
        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, EventService dictionaryService, EventMapService eventDictionaryMapService, ILogger<EventBus> logger)
            : this(serviceProvider, logger, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class with handlers list.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
        /// <param name="assemblies">Assemblies containing handlers.</param>
        /// <param name="dictionaryService">Event service registry.</param>
        /// <param name="eventDictionaryMapService">Event map service registry.</param>
        /// <param name="handlers">Handler types list.</param>
        /// <param name="logger">Logger instance.</param>
        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, EventService dictionaryService, EventMapService eventDictionaryMapService, IEnumerable<Type> handlers, ILogger<EventBus> logger)
            : this(serviceProvider, logger, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class with full parameters including options.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
        /// <param name="assemblies">Assemblies containing handlers.</param>
        /// <param name="dictionaryService">Event service registry.</param>
        /// <param name="eventDictionaryMapService">Event map service registry.</param>
        /// <param name="handlers">Handler types list.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="options">Configuration options.</param>
        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, EventService dictionaryService, EventMapService eventDictionaryMapService, IEnumerable<Type> handlers, ILogger<EventBus> logger, EventFluxOptions? options)
            : this(serviceProvider, logger, options)
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

        private static async Task<TResponse?> DispatchSendAsync<TResponse>(
            IServiceProvider serviceProvider,
            IEventRequest<TResponse> request,
            CancellationToken cancellationToken)
            where TResponse : IEventResponse
        {
            var handlerType = DispatchTypeCache.RequestHandlerType(request.GetType(), typeof(TResponse));

            var handler = serviceProvider.GetRequiredService(handlerType);

            var task = (Task<TResponse>)DispatchTypeCache.ForInterface(handlerType).Handle(handler, request, cancellationToken);

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

            var handlers = serviceProvider.GetServices(handlerType);

            var invocations = handlers
                .Where(handler => handler is not null)
                .Select(handler => (Handler: handler!, Accessor: HandlerAccessor.ForConcreteType(handler!.GetType(), eventType)))
                .Where(entry => entry.Accessor is not null)
                .OrderBy(entry => entry.Accessor!.Priority)
                .ToList();

            if (invocations.Count == 0)
                return;

            if (_options.PublishStrategy == PublishStrategy.Sequential)
            {
                foreach (var entry in invocations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var accessor = entry.Accessor!;

                    if (accessor.CanHandle != null && !accessor.CanHandle(entry.Handler, request))
                        continue;

                    await ((Task)accessor.Handle(entry.Handler, request, cancellationToken)).ConfigureAwait(false);
                }
            }
            else
            {
                var tasks = invocations.Select(async entry =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var accessor = entry.Accessor!;

                    if (accessor.CanHandle != null && !accessor.CanHandle(entry.Handler, request))
                        return;

                    await ((Task)accessor.Handle(entry.Handler, request, cancellationToken)).ConfigureAwait(false);
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
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
