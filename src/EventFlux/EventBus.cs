using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;

namespace EventFlux
{
    public class EventBus : IEventBus
    {
        private bool _isSendRetry = false;
        private bool _isPublishRetry = false;
        private bool _isStackPublishRetry;
        private readonly ILogger<EventBus> _logger;
        private readonly IEnumerable<Type> _handlers;
        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<Assembly> _assemblies;
        private readonly EventService _eventDictionaryService;
        private EventStackService _eventStackDictionaryService;
        private readonly EventMapService _eventDictionaryMapService;

        public EventBus() { }

        public EventBus(IServiceProvider serviceProvider, ILogger<EventBus> logger)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _eventDictionaryService = new();
            _eventDictionaryMapService = new();
            _eventStackDictionaryService = new();
        }

        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, ILogger<EventBus> logger)
        {
            _logger = logger;
            _assemblies = assemblies;
            _serviceProvider = serviceProvider;
            _eventDictionaryService = new();
            _eventDictionaryMapService = new();
            _eventStackDictionaryService = new();
        }

        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, EventService dictionaryService, EventMapService eventDictionaryMapService, ILogger<EventBus> logger)
        {
            _logger = logger;
            _assemblies = assemblies;
            _serviceProvider = serviceProvider;
            _eventDictionaryService = dictionaryService;
            _eventDictionaryMapService = eventDictionaryMapService;
            _eventStackDictionaryService = new();
        }

        public EventBus(IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies, EventService dictionaryService, EventMapService eventDictionaryMapService, IEnumerable<Type> handlers, ILogger<EventBus> logger)
        {
            _logger = logger;
            _handlers = handlers;
            _serviceProvider = serviceProvider;
            _assemblies = assemblies;
            _eventDictionaryService = dictionaryService;
            _eventDictionaryMapService = eventDictionaryMapService;
            _eventStackDictionaryService = new();
        }

        public async virtual Task<TResponse> SendAsync<TResponse>(IEventRequest<TResponse> request)
            where TResponse : IEventResponse
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (_eventDictionaryMapService.GetValue(request.ToString(), out Type? responseType))
            {
                using var scope = _serviceProvider.CreateScope();
                var handlerInterface = typeof(IEventHandler<,>).MakeGenericType(request.GetType(), responseType);

                var handler = scope.ServiceProvider.GetService(handlerInterface);

                if (handler is null)
                {
                    var handlers = _eventDictionaryService.GetHandlersForEvent(request.GetType());
                    if (handlers is null || handlers.Count == 0)
                        return default;

                    foreach (var eventHandler in handlers)
                    {
                        var handlerInstance = Activator.CreateInstance(eventHandler);
                        if (!TryInvokeHandler(handlerInterface, handlerInstance, request, out TResponse result))
                            continue;
                        return result;
                    }
                    return default;
                }

                if (!TryInvokeHandler(handlerInterface, handler, request, out TResponse handlerResult))
                    return default;

                if (_eventDictionaryMapService.GetValue(request.GetType().ToString(), out var modelType))
                    handlerResult = (TResponse)Convert.ChangeType(handlerResult, modelType);

                return handlerResult;
            }
            else
            {
                if (!_isSendRetry)
                {
                    _isSendRetry = true;
                    if (!_eventDictionaryMapService.InternalEventMaps.Any())
                        _eventDictionaryMapService.FindEvents();
                    await SendAsync(request);
                }
                else
                {
                    throw new InvalidOperationException($"No handler found for event {request.GetType().Name}");
                }
            }
            return default;
        }

        public virtual async Task PublishAsync(IEventRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (!_isPublishRetry)
            {
                using var scope = _serviceProvider.CreateScope();
                var handlerInterface = typeof(IEventHandler<>).MakeGenericType(request.GetType());
                var enumerableHandlerInterface = typeof(IEnumerable<>).MakeGenericType(handlerInterface);

                if (scope.ServiceProvider.GetService(enumerableHandlerInterface) is IEnumerable<object> handlers)
                {
                    foreach (var handler in handlers)
                        await TryInvokeHandlerAsync(handlerInterface, handler, request);
                }
                else
                {
                    _isPublishRetry = true;
                    if (!_eventDictionaryService.InternalEventHandlers.Any())
                        _eventDictionaryService.FindEventHandlers();
                    await PublishAsync(request);
                }
            }
            else
            {
                var handlerEvents = _eventDictionaryService.GetHandlersForEvent(request.GetType());
                if (handlerEvents is not null)
                {
                    foreach (var handlerType in handlerEvents)
                    {
                        var handlerInterface = typeof(IEventHandler<>).MakeGenericType(request.GetType());
                        var handler = Activator.CreateInstance(handlerType);
                        await TryInvokeHandlerAsync(handlerInterface, handler, request);
                    }
                }
                else
                {
                    _logger.LogError("No handlers found for event type");
                }
                _isPublishRetry = false;
            }
        }

        public virtual async Task StackEventDispatcherAsync()
        {
            var stackEvents = _eventStackDictionaryService.GetAllEventRequest();

            if (!_isStackPublishRetry)
            {
                foreach (var eventRequest in stackEvents)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var handlerInterface = typeof(IEventHandler<>).MakeGenericType(eventRequest.GetType());
                    var enumerableHandlerInterface = typeof(IEnumerable<>).MakeGenericType(handlerInterface);

                    if (scope.ServiceProvider.GetService(enumerableHandlerInterface) is IEnumerable<object> handlers)
                    {
                        foreach (var handler in handlers)
                            await TryInvokeHandlerAsync(handlerInterface, handler, eventRequest);
                    }
                    else
                    {
                        _isStackPublishRetry = true;
                        if (!_eventDictionaryService.InternalEventHandlers.Any())
                            _eventDictionaryService.FindEventHandlers();
                        await StackEventDispatcherAsync().ConfigureAwait(false);
                        return;
                    }
                }
            }
            else
            {
                foreach (var eventRequest in stackEvents)
                {
                    var handlerInterface = typeof(IEventHandler<>).MakeGenericType(eventRequest.GetType());
                    var handlerTypes = _eventDictionaryService.GetHandlersForEvent(eventRequest.GetType());
                    if (handlerTypes == null)
                    {
                        _logger.LogError($"No handlers found for event type {eventRequest.GetType().Name}");
                        continue;
                    }
                    foreach (var handlerType in handlerTypes)
                    {
                        var handler = Activator.CreateInstance(handlerType);
                        await TryInvokeHandlerAsync(handlerInterface, handler, eventRequest);
                    }
                }
                _isStackPublishRetry = false;
            }
            _eventStackDictionaryService.ClearEventRequest();
        }

        public void AddStackRequestEvent<TEvent>(TEvent eventRequest) where TEvent : IEventRequest
            => _eventStackDictionaryService.AddEventRequest(eventRequest);

        public void RemoveStackRequestEvent<TEvent>(TEvent eventRequest) where TEvent : IEventRequest
            => _eventStackDictionaryService.RemoveEventRequest(eventRequest);

        private bool TryInvokeHandler<TResponse>(Type handlerInterface, object handler, object request, out TResponse result)
        {
            result = default;

            if (handler == null)
            {
                _logger.LogError("Handler instance is null.");
                return false;
            }

            if (!handlerInterface.IsAssignableFrom(handler.GetType()))
            {
                _logger.LogWarning($"Handler {handler.GetType().Name} does not implement {handlerInterface.Name}.");
                return false;
            }

            var eventType = handlerInterface.GetGenericArguments()[0];

            var canHandleMethod = handlerInterface.GetMethod("CanHandle", new[] { eventType });
            var handleMethod = handlerInterface.GetMethod("Handle", new[] { eventType });

            if (handleMethod == null)
            {
                _logger.LogError($"Handler {handler.GetType().Name} missing required Handle method.");
                return false;
            }

            try
            {
                if (canHandleMethod != null)
                {
                    var canHandleResult = canHandleMethod.Invoke(handler, new[] { request });
                    if (canHandleResult is bool shouldHandle && !shouldHandle)
                    {
                        _logger.LogInformation(
                            $"Handler {handler.GetType().Name} skipped because CanHandle returned false for {request.GetType().Name}.");
                        return false;
                    }
                }

                var taskObj = handleMethod.Invoke(handler, new[] { request });
                if (taskObj is not Task<TResponse> typedTask)
                {
                    _logger.LogError($"Handler {handler.GetType().Name}.Handle did not return Task<{typeof(TResponse).Name}>.");
                    return false;
                }

                typedTask.ConfigureAwait(false).GetAwaiter().GetResult();
                result = typedTask.Result;
                return true;
            }
            catch (TargetInvocationException tex)
            {
                _logger.LogError(tex.InnerException ?? tex, $"Handler {handler.GetType().Name} threw an exception.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Handler {handler.GetType().Name} invocation failed.");
                return false;
            }
        }

        private async Task TryInvokeHandlerAsync(Type handlerInterface, object handler, object request)
        {
            if (handler == null)
            {
                _logger.LogError("Handler instance is null.");
                return;
            }

            if (!handlerInterface.IsAssignableFrom(handler.GetType()))
            {
                _logger.LogWarning($"Handler {handler.GetType().Name} does not implement {handlerInterface.Name}.");
                return;
            }

            var eventType = handlerInterface.GetGenericArguments()[0];

            var canHandleMethod = handlerInterface.GetMethod("CanHandle", new[] { eventType });
            var handleMethod = handlerInterface.GetMethod("Handle", new[] { eventType });

            if (handleMethod == null)
            {
                _logger.LogError($"Handler {handler.GetType().Name} missing required Handle method.");
                return;
            }

            try
            {
                if (canHandleMethod != null)
                {
                    var canHandleResult = canHandleMethod.Invoke(handler, new[] { request });
                    if (canHandleResult is bool shouldHandle && !shouldHandle)
                    {
                        _logger.LogInformation(
                            $"Handler {handler.GetType().Name} skipped because CanHandle returned false for {request.GetType().Name}.");
                        return;
                    }
                }

                if (handleMethod.Invoke(handler, new[] { request }) is Task task)
                {
                    await task.ConfigureAwait(false);
                }
                else
                {
                    _logger.LogError($"Handler {handler.GetType().Name}.Handle did not return a Task.");
                }
            }
            catch (TargetInvocationException ex)
            {
                _logger.LogError(ex.InnerException ?? ex, $"Error handling {request.GetType().Name} in {handler.GetType().Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling {request.GetType().Name} in {handler.GetType().Name}");
            }
        }
    }
}
