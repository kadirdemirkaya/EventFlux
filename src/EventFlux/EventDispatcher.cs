using EventFlux.Abstractions;
using EventFlux.Attributes;
using EventFlux.Delegates;
using EventFlux.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace EventFlux
{
    public class EventDispatcher : IEventDispatcher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EventDispatcher> _logger;

        public EventDispatcher(IServiceProvider serviceProvider, ILogger<EventDispatcher> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<TResponse> SendAsync<TResponse>(
           IEventRequest<TResponse> request,
           CancellationToken cancellationToken = default)
           where TResponse : IEventResponse
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var requestType = request.GetType();

                var handlerType = DispatchTypeCache.RequestHandlerType(requestType, typeof(TResponse));
                var handler = scope.ServiceProvider.GetRequiredService(handlerType);

                var behaviorType = DispatchTypeCache.RequestPipelineType(requestType, typeof(TResponse));
                var behaviors = scope.ServiceProvider.GetServices(behaviorType).Reverse().ToList();

                var accessor = DispatchTypeCache.ForInterface(handlerType);

                EventHandlerDelegate<TResponse> handlerDelegate = (cancellationToken) =>
                {
                    if (accessor.CanHandle != null && accessor.CanHandle(handler, request) is bool result && !result)
                    {
                        _logger.LogInformation("Handler {Handler} cannot handle event {EventName}. Skipping.", handlerType.Name, requestType.Name);

                        return Task.FromResult<TResponse>(default!);
                    }

                    return (Task<TResponse>)accessor.Handle(handler, request);
                };


                foreach (var behavior in behaviors)
                {
                    var next = handlerDelegate;
                    var behaviorInvoker = DispatchTypeCache.BehaviorInvoker(behavior!.GetType());

                    handlerDelegate = (cancellationToken) =>
                        (Task<TResponse>)behaviorInvoker(behavior, request, next, cancellationToken);
                }

                return await handlerDelegate(cancellationToken);
            }
        }

        public async Task PublishAsync(
           IEventRequest request,
           CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var requestType = request.GetType();
            var handlerInterface = DispatchTypeCache.NotificationHandlerType(requestType);

            using var scope = _serviceProvider.CreateScope();

            var handlerTypes = scope.ServiceProvider
                .GetServices(handlerInterface)
                .OrderBy(h =>
                    h.GetType().GetCustomAttribute<HandlerOrderAttribute>()?.Priority ?? 0)
                .ToList();

            if (handlerTypes.Count == 0)
                return;

            var behaviorType = DispatchTypeCache.NotificationPipelineType(requestType);

            var behaviors = scope.ServiceProvider
                .GetServices(behaviorType)
                .Cast<object>()
                .Reverse()
                .ToList();

            EventHandlerDelegate handlerDelegate = async ct =>
            {
                var tasks = handlerTypes.Select(handler => InvokeHandlerAsync(handler, request, ct));

                await Task.WhenAll(tasks).ConfigureAwait(false);
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

            if (accessor.Handle(handler, request) is Task task)
                await task.ConfigureAwait(false);
            else
                throw new InvalidOperationException($"{handlerType.Name}.Handle must return Task");
        }
    }
}
