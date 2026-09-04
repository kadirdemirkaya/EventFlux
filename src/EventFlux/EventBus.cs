using EventFlux.Abstractions;
using EventFlux.Internal;
using EventFlux.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EventFlux
{
    public class EventBus : IEventBus
    {
        private readonly ILogger<EventBus>? _logger;
        private readonly IServiceProvider? _serviceProvider;
        private readonly EventStackService _eventStackDictionaryService = new();

        public EventBus() { }

        public EventBus(IServiceProvider serviceProvider, ILogger<EventBus> logger)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, ILogger<EventBus> logger)
            : this(serviceProvider, logger)
        {
        }

        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, EventService dictionaryService, EventMapService eventDictionaryMapService, ILogger<EventBus> logger)
            : this(serviceProvider, logger)
        {
        }

        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, EventService dictionaryService, EventMapService eventDictionaryMapService, IEnumerable<Type> handlers, ILogger<EventBus> logger)
            : this(serviceProvider, logger)
        {
        }

        public async Task<TResponse?> SendAsync<TResponse>(
            IEventRequest<TResponse> request,
            CancellationToken cancellationToken = default)
            where TResponse : IEventResponse
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            var serviceProvider = RequireServiceProvider();

            var handlerType = DispatchTypeCache.RequestHandlerType(request.GetType(), typeof(TResponse));

            using var scope = serviceProvider.CreateScope();

            var handler = scope.ServiceProvider.GetRequiredService(handlerType);

            var task = (Task<TResponse>)DispatchTypeCache.ForInterface(handlerType).Handle(handler, request);

            return await task.ConfigureAwait(false);
        }

        public async Task PublishAsync(
            IEventRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            var eventType = request.GetType();

            var serviceProvider = RequireServiceProvider();

            using var scope = serviceProvider.CreateScope();

            var handlerType = DispatchTypeCache.NotificationHandlerType(eventType);

            var handlers = scope.ServiceProvider.GetServices(handlerType);

            var invocations = handlers
                .Where(handler => handler is not null)
                .Select(handler => (Handler: handler!, Accessor: HandlerAccessor.ForConcreteType(handler!.GetType(), eventType)))
                .Where(entry => entry.Accessor is not null)
                .OrderBy(entry => entry.Accessor!.Priority)
                .ToList();

            if (invocations.Count == 0)
                return;

            var tasks = invocations.Select(async entry =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var accessor = entry.Accessor!;

                if (accessor.CanHandle != null && !accessor.CanHandle(entry.Handler, request))
                    return;

                await ((Task)accessor.Handle(entry.Handler, request)).ConfigureAwait(false);
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

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

        public void AddStackRequestEvent<TEvent>(TEvent eventRequest) where TEvent : IEventRequest
            => _eventStackDictionaryService.AddEventRequest(eventRequest);
    }
}
