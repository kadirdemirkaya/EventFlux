using EventFlux.Abstractions;
using EventFlux.Attributes;
using EventFlux.Delegates;
using EventFlux.Internal;
using EventFlux.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace EventFlux
{
    /// <summary>
    /// Transient implementation of <see cref="IEventDispatcher"/> supporting custom pipeline behaviors.
    /// </summary>
    public class EventDispatcher : IEventDispatcher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EventDispatcher> _logger;
        private readonly EventFluxOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventDispatcher"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve handlers and behaviors.</param>
        /// <param name="logger">Logger instance.</param>
        public EventDispatcher(IServiceProvider serviceProvider, ILogger<EventDispatcher> logger)
            : this(serviceProvider, logger, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventDispatcher"/> class with options.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve handlers and behaviors.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="options">Configuration options.</param>
        public EventDispatcher(IServiceProvider serviceProvider, ILogger<EventDispatcher> logger, EventFluxOptions? options)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options ?? serviceProvider?.GetService<EventFluxOptions>() ?? new EventFluxOptions();
        }

        /// <inheritdoc />
        public async Task<TResponse?> SendAsync<TResponse>(
           IEventRequest<TResponse> request,
           CancellationToken cancellationToken = default)
           where TResponse : IEventResponse
        {
            if (_options.CreateScopePerEvent)
            {
                using var scope = _serviceProvider.CreateScope();
                return await DispatchSendAsync(scope.ServiceProvider, request, cancellationToken).ConfigureAwait(false);
            }

            return await DispatchSendAsync(_serviceProvider, request, cancellationToken).ConfigureAwait(false);
        }

        private async Task<TResponse?> DispatchSendAsync<TResponse>(
            IServiceProvider serviceProvider,
            IEventRequest<TResponse> request,
            CancellationToken cancellationToken)
            where TResponse : IEventResponse
        {
            var requestType = request.GetType();

            var handlerType = DispatchTypeCache.RequestHandlerType(requestType, typeof(TResponse));
            var handler = serviceProvider.GetRequiredService(handlerType);

            var behaviorType = DispatchTypeCache.RequestPipelineType(requestType, typeof(TResponse));
            var behaviors = HandlerInvocationBuilder.AsReadOnlyList(serviceProvider.GetServices(behaviorType));

            var accessor = DispatchTypeCache.ForInterface(handlerType);

            EventHandlerDelegate<TResponse> handlerDelegate = (cancellationToken) =>
            {
                if (accessor.CanHandle != null && accessor.CanHandle(handler, request) is bool result && !result)
                {
                    _logger.LogInformation("Handler {Handler} cannot handle event {EventName}. Skipping.", handlerType.Name, requestType.Name);

                    return Task.FromResult<TResponse>(default!);
                }

                return (Task<TResponse>)accessor.Handle(handler, request, cancellationToken);
            };

            for (var i = behaviors.Count - 1; i >= 0; i--)
            {
                var behavior = behaviors[i];
                var next = handlerDelegate;
                var behaviorInvoker = DispatchTypeCache.BehaviorInvoker(behavior!.GetType());

                handlerDelegate = (cancellationToken) =>
                    (Task<TResponse>)behaviorInvoker(behavior, request, next, cancellationToken);
            }

            return await handlerDelegate(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task PublishAsync(
           IEventRequest request,
           CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_options.CreateScopePerEvent)
            {
                using var scope = _serviceProvider.CreateScope();
                await DispatchPublishAsync(scope.ServiceProvider, request, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await DispatchPublishAsync(_serviceProvider, request, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task DispatchPublishAsync(
            IServiceProvider serviceProvider,
            IEventRequest request,
            CancellationToken cancellationToken)
        {
            var requestType = request.GetType();
            var handlerInterface = DispatchTypeCache.NotificationHandlerType(requestType);

            var handlers = HandlerInvocationBuilder.AsReadOnlyList(serviceProvider.GetServices(handlerInterface));

            if (handlers.Count == 0)
                return;

            var orderedHandlers = new object[handlers.Count];
            var priorities = new int[handlers.Count];

            for (var i = 0; i < handlers.Count; i++)
            {
                var handler = handlers[i]!;

                orderedHandlers[i] = handler;
                priorities[i] = GetHandlerPriority(handler.GetType(), requestType);
            }

            SortByPriority(orderedHandlers, priorities);

            var behaviorType = DispatchTypeCache.NotificationPipelineType(requestType);

            var behaviors = HandlerInvocationBuilder.AsReadOnlyList(serviceProvider.GetServices(behaviorType));

            EventHandlerDelegate handlerDelegate = async ct =>
            {
                if (_options.PublishStrategy == PublishStrategy.Sequential)
                {
                    for (var i = 0; i < orderedHandlers.Length; i++)
                    {
                        await InvokeHandlerAsync(orderedHandlers[i], request, ct).ConfigureAwait(false);
                    }
                }
                else
                {
                    var tasks = new Task[orderedHandlers.Length];

                    for (var i = 0; i < orderedHandlers.Length; i++)
                    {
                        tasks[i] = InvokeHandlerAsync(orderedHandlers[i], request, ct);
                    }

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            };

            for (var i = behaviors.Count - 1; i >= 0; i--)
            {
                var behavior = behaviors[i];
                var next = handlerDelegate;
                var behaviorInvoker = DispatchTypeCache.BehaviorInvoker(behavior!.GetType());

                handlerDelegate = ct => (Task)behaviorInvoker(behavior, request, next, ct);
            }

            await handlerDelegate(cancellationToken).ConfigureAwait(false);
        }

        private static int GetHandlerPriority(Type handlerType, Type requestType)
        {
            var accessor = HandlerAccessor.ForConcreteType(handlerType, requestType);

            return accessor?.Priority
                ?? handlerType.GetCustomAttribute<HandlerOrderAttribute>()?.Priority
                ?? 0;
        }

        private static void SortByPriority(object[] handlers, int[] priorities)
        {
            for (var i = 1; i < handlers.Length; i++)
            {
                var currentHandler = handlers[i];
                var currentPriority = priorities[i];
                var j = i - 1;

                while (j >= 0 && priorities[j] > currentPriority)
                {
                    handlers[j + 1] = handlers[j];
                    priorities[j + 1] = priorities[j];
                    j--;
                }

                handlers[j + 1] = currentHandler;
                priorities[j + 1] = currentPriority;
            }
        }

        private async Task InvokeHandlerAsync(
            object handler,
            IEventRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var handlerType = handler.GetType();
            var requestType = request.GetType();

            var accessor = HandlerAccessor.ForConcreteType(handlerType, requestType)
                ?? throw new InvalidOperationException($"Handle method not found on {handlerType.Name}");

            if (accessor.CanHandle != null && !accessor.CanHandle(handler, request))
                return;

            if (accessor.Handle(handler, request, cancellationToken) is Task task)
                await task.ConfigureAwait(false);
            else
                throw new InvalidOperationException($"{handlerType.Name}.Handle must return Task");
        }
    }
}
