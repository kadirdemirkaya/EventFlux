using EventFlux.Abstractions;
using EventFlux.Attributes;
using EventFlux.Descriptors;
using EventFlux.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EventFlux
{
    public class EventBus : IEventBus
    {
        private readonly ILogger<EventBus> _logger;
        private readonly IEnumerable<Type> _handlers;
        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<Assembly> _assemblies;
        private EventStackService _eventStackDictionaryService;

        private readonly ConcurrentDictionary<Type, IReadOnlyList<HandlerDescriptor>> _handlerCache = new();

        public EventBus() { }

        public EventBus(IServiceProvider serviceProvider, ILogger<EventBus> logger)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _eventStackDictionaryService = new();
        }

        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, ILogger<EventBus> logger)
        {
            _logger = logger;
            _assemblies = assemblies;
            _serviceProvider = serviceProvider;
            _eventStackDictionaryService = new();
        }

        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, EventService dictionaryService, EventMapService eventDictionaryMapService, ILogger<EventBus> logger)
        {
            _logger = logger;
            _assemblies = assemblies;
            _serviceProvider = serviceProvider;
            _eventStackDictionaryService = new();
        }

        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, EventService dictionaryService, EventMapService eventDictionaryMapService, IEnumerable<Type> handlers, ILogger<EventBus> logger)
        {
            _logger = logger;
            _handlers = handlers;
            _serviceProvider = serviceProvider;
            _assemblies = assemblies;
            _eventStackDictionaryService = new();
        }

        public async Task<TResponse?> SendAsync<TResponse>(
            IEventRequest<TResponse> request,
            CancellationToken cancellationToken = default)
            where TResponse : IEventResponse
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            var handlerType = typeof(IEventHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));

            using var scope = _serviceProvider.CreateScope();

            var handler = scope.ServiceProvider.GetRequiredService(handlerType);

            var method = handlerType.GetMethod("Handle")!;

            var task = (Task<TResponse>)method.Invoke(handler, new[] { request })!;

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

            var descriptors = GetOrBuildHandlers(eventType);

            if (descriptors.Count == 0)
                return;

            using var scope = _serviceProvider.CreateScope();

            var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);

            var handlers = scope.ServiceProvider.GetServices(handlerType).ToDictionary(h => h.GetType());

            var tasks = descriptors.Select(async desc =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!handlers.TryGetValue(desc.HandlerType, out var handler))
                    return;

                if (desc.CanHandleMethod != null && desc.CanHandleMethod.Invoke(handler, new[] { request }) is false)
                    return;

                await ((Task)desc.HandleMethod.Invoke(handler, new[] { request })!).ConfigureAwait(false);
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        public async Task StackEventDispatcherAsync(CancellationToken cancellationToken = default)
        {
            var events = _eventStackDictionaryService.Drain();
            if (events.Count == 0)
                return;

            foreach (var evt in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await PublishAsync(evt, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Stack dispatch failed for {evt.GetType().Name}");
                }
            }
        }

        private IReadOnlyList<HandlerDescriptor> GetOrBuildHandlers(Type eventType)
        {
            return _handlerCache.GetOrAdd(eventType, BuildHandlers);
        }

        private IReadOnlyList<HandlerDescriptor> BuildHandlers(Type eventType)
        {
            var handlers = new List<HandlerDescriptor>();

            var assemblies = _assemblies ?? AppDomain.CurrentDomain.GetAssemblies();

            var handlerTypes = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract)
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IEventHandler<>) &&
                    i.GenericTypeArguments[0] == eventType));

            foreach (var type in handlerTypes)
            {
                var handleMethod = type.GetMethod("Handle", BindingFlags.Instance | BindingFlags.Public, binder: null, new[] { eventType }, modifiers: null);

                if (handleMethod == null)
                    continue;

                var canHandleMethod = type.GetMethod("CanHandle", BindingFlags.Instance | BindingFlags.Public, binder: null, new[] { eventType }, modifiers: null);

                var priority = type.GetCustomAttribute<HandlerOrderAttribute>()?.Priority ?? 0;

                handlers.Add(new HandlerDescriptor(type, handleMethod, canHandleMethod, priority));
            }

            return handlers.OrderBy(h => h.Priority).ToList().AsReadOnly();
        }

        public void AddStackRequestEvent<TEvent>(TEvent eventRequest) where TEvent : IEventRequest
            => _eventStackDictionaryService.AddEventRequest(eventRequest);
    }
}
