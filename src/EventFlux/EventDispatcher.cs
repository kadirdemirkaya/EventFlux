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
        public async Task<TResponse> SendAsync<TResponse>(
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

        private async Task<TResponse> DispatchSendAsync<TResponse>(
            IServiceProvider serviceProvider,
            IEventRequest<TResponse> request,
            CancellationToken cancellationToken)
            where TResponse : IEventResponse
        {
            var requestType = request.GetType();

            var handlerType = DispatchTypeCache.RequestHandlerType(requestType, typeof(TResponse));
            var handler = serviceProvider.GetRequiredService(handlerType);

            var behaviorType = DispatchTypeCache.RequestPipelineType(requestType, typeof(TResponse));
            var behaviors = serviceProvider.GetServices(behaviorType).Reverse().ToList();

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

            foreach (var behavior in behaviors)
            {
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

            var handlerTypes = serviceProvider
                .GetServices(handlerInterface)
                .OrderBy(h =>
                    h.GetType().GetCustomAttribute<HandlerOrderAttribute>()?.Priority ?? 0)
                .ToList();

            if (handlerTypes.Count == 0)
                return;

            var behaviorType = DispatchTypeCache.NotificationPipelineType(requestType);

            var behaviors = serviceProvider
                .GetServices(behaviorType)
                .Cast<object>()
                .Reverse()
                .ToList();

            EventHandlerDelegate handlerDelegate = async ct =>
            {
                if (_options.PublishStrategy == PublishStrategy.Sequential)
                {
                    foreach (var handler in handlerTypes)
                    {
                        await InvokeHandlerAsync(handler, request, ct).ConfigureAwait(false);
                    }
                }
                else
                {
                    var tasks = handlerTypes.Select(handler => InvokeHandlerAsync(handler, request, ct));
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            };

            foreach (var behavior in behaviors)
            {
                var next = handlerDelegate;
                var behaviorInvoker = DispatchTypeCache.BehaviorInvoker(behavior.GetType());

                handlerDelegate = ct => (Task)behaviorInvoker(behavior, request, next, ct);
            }

            await handlerDelegate(cancellationToken).ConfigureAwait(false);
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
