using EventFlux.Abstractions;
using EventFlux.Attributes;
using EventFlux.Delegates;
using EventFlux.Internal;
using EventFlux.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace EventFlux
{
    /// <summary>
    /// Transient implementation of <see cref="IEventDispatcher"/> supporting custom pipeline behaviors.
    /// </summary>
    [RequiresDynamicCode(AotMessages.DynamicCode)]
    [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> or <paramref name="logger"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> or <paramref name="logger"/> is null.</exception>
        public EventDispatcher(IServiceProvider serviceProvider, ILogger<EventDispatcher> logger, EventFluxOptions? options)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? serviceProvider.GetService<EventFluxOptions>() ?? new EventFluxOptions();
        }

        /// <inheritdoc />
        public async Task<TResponse?> SendAsync<TResponse>(
           IEventRequest<TResponse> request,
           CancellationToken cancellationToken = default)
           where TResponse : IEventResponse
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (_options.CreateScopePerEvent)
            {
                using var scope = _serviceProvider.CreateScope();
                return await DispatchSendAsync(scope.ServiceProvider, request, cancellationToken).ConfigureAwait(false);
            }

            return await DispatchSendAsync(_serviceProvider, request, cancellationToken).ConfigureAwait(false);
        }

        private Task<TResponse?> DispatchSendAsync<TResponse>(
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

            if (behaviors.Count == 0)
                return InvokeSendHandler(accessor, handler, request, handlerType, requestType, cancellationToken)!;

            return BuildSendPipeline(behaviors, behaviorType, accessor, handler, request, handlerType, requestType)(cancellationToken)!;
        }

        private EventHandlerDelegate<TResponse> BuildSendPipeline<TResponse>(
            IReadOnlyList<object?> behaviors,
            Type behaviorType,
            InterfaceAccessor accessor,
            object handler,
            IEventRequest<TResponse> request,
            Type handlerType,
            Type requestType)
            where TResponse : IEventResponse
        {
            EventHandlerDelegate<TResponse> handlerDelegate = (cancellationToken) =>
                InvokeSendHandler(accessor, handler, request, handlerType, requestType, cancellationToken);

            for (var i = behaviors.Count - 1; i >= 0; i--)
            {
                var behavior = behaviors[i]!;
                var next = handlerDelegate;
                var behaviorInvoker = DispatchTypeCache.BehaviorInvoker(behaviorType);

                handlerDelegate = (cancellationToken) =>
                    (Task<TResponse>)behaviorInvoker(behavior, request, next, cancellationToken);
            }

            return handlerDelegate;
        }

        private Task<TResponse> InvokeSendHandler<TResponse>(
            InterfaceAccessor accessor,
            object handler,
            IEventRequest<TResponse> request,
            Type handlerType,
            Type requestType,
            CancellationToken cancellationToken)
            where TResponse : IEventResponse
        {
            if (accessor.CanHandle != null && accessor.CanHandle(handler, request) is bool result && !result)
            {
                _logger.LogInformation("Handler {Handler} cannot handle event {EventName}. Skipping.", handlerType.Name, requestType.Name);

                return Task.FromResult<TResponse>(default!);
            }

            return (Task<TResponse>)accessor.Handle(handler, request, cancellationToken);
        }

        /// <inheritdoc />
        public async Task PublishAsync(
           IEventRequest request,
           CancellationToken cancellationToken = default)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

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

        private Task DispatchPublishAsync(
            IServiceProvider serviceProvider,
            IEventRequest request,
            CancellationToken cancellationToken)
        {
            var requestType = request.GetType();
            var handlerInterface = DispatchTypeCache.NotificationHandlerType(requestType);

            var handlers = HandlerInvocationBuilder.AsReadOnlyList(serviceProvider.GetServices(handlerInterface));

            if (handlers.Count == 0)
                return Task.CompletedTask;

            var orderedHandlers = new object[handlers.Count];

            if (handlers.Count == 1)
            {
                orderedHandlers[0] = handlers[0]!;
            }
            else
            {
                var priorities = new int[handlers.Count];

                for (var i = 0; i < handlers.Count; i++)
                {
                    var handler = handlers[i]!;

                    orderedHandlers[i] = handler;
                    priorities[i] = GetHandlerPriority(handler.GetType(), requestType);
                }

                SortByPriority(orderedHandlers, priorities);
            }

            var behaviorType = DispatchTypeCache.NotificationPipelineType(requestType);

            var behaviors = HandlerInvocationBuilder.AsReadOnlyList(serviceProvider.GetServices(behaviorType));

            if (behaviors.Count == 0)
                return InvokeHandlersAsync(orderedHandlers, request, cancellationToken);

            EventHandlerDelegate handlerDelegate = ct => InvokeHandlersAsync(orderedHandlers, request, ct);

            for (var i = behaviors.Count - 1; i >= 0; i--)
            {
                var behavior = behaviors[i]!;
                var next = handlerDelegate;
                var behaviorInvoker = DispatchTypeCache.BehaviorInvoker(behaviorType);

                handlerDelegate = ct => (Task)behaviorInvoker(behavior, request, next, ct);
            }

            return handlerDelegate(cancellationToken);
        }

        private Task InvokeHandlersAsync(
            object[] orderedHandlers,
            IEventRequest request,
            CancellationToken ct)
        {
            if (orderedHandlers.Length == 1)
                return InvokeHandlerAsync(orderedHandlers[0], request, ct);

            return InvokeManyHandlersAsync(orderedHandlers, request, ct);
        }

        private async Task InvokeManyHandlersAsync(
            object[] orderedHandlers,
            IEventRequest request,
            CancellationToken ct)
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
                var allCompletedSuccessfully = true;

                for (var i = 0; i < orderedHandlers.Length; i++)
                {
                    var task = InvokeHandlerAsync(orderedHandlers[i], request, ct);
                    tasks[i] = task;

                    if (!task.IsCompletedSuccessfully)
                        allCompletedSuccessfully = false;
                }

                if (!allCompletedSuccessfully)
                {
                    await ParallelPublish.WhenAllAsync(tasks).ConfigureAwait(false);
                }
            }
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
