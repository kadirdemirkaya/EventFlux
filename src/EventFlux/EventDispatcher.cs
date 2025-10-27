using EventFlux.Abstractions;
using EventFlux.Attributes;
using EventFlux.Delegates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
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

                var handlerType = typeof(IEventHandler<,>).MakeGenericType(requestType, typeof(TResponse));
                var handler = scope.ServiceProvider.GetRequiredService(handlerType);

                var behaviorType = typeof(IEventCustomPipeline<,>).MakeGenericType(requestType, typeof(TResponse));
                var behaviors = scope.ServiceProvider.GetServices(behaviorType).Reverse().ToList(); // it could be more of pipeline

                EventHandlerDelegate<TResponse> handlerDelegate = async (cancellationToken) =>
                {
                    var handleMethod = handlerType.GetMethod("Handle");
                    var canHandleMethod = handlerType.GetMethod("CanHandle");

                    if (handleMethod == null)
                        throw new InvalidOperationException($"Handler method 'Handle' not found for {requestType.Name}");

                    bool canHandle = true;
                    if (canHandleMethod != null)
                    {
                        var canHandleResult = canHandleMethod.Invoke(handler, new object[] { request });
                        if (canHandleResult is bool result)
                            canHandle = result;
                    }

                    if (!canHandle)
                    {
                        _logger.LogInformation(
                            $"Handler {handlerType.Name} cannot handle event {requestType.Name}. Skipping.");
                        return default;
                    }

                    var task = (Task<TResponse>)handleMethod.Invoke(handler, new object[] { request })!;
                    return await task.ConfigureAwait(false);
                };


                foreach (var behavior in behaviors)
                {
                    var next = handlerDelegate;
                    var behaviorMethod = behavior.GetType().GetMethod("Handle");

                    if (behaviorMethod == null)
                        throw new InvalidOperationException($"Pipeline Handle method not found for {behavior.GetType().Name}");

                    handlerDelegate = async (cancellationToken) =>
                    {
                        var task = (Task<TResponse>)behaviorMethod.Invoke(behavior, new object[] { request, next, cancellationToken })!;
                        return await task;
                    };
                }
                return await handlerDelegate(cancellationToken);
            }
        }

        public async Task PublishAsync(
            IEventRequest request,
            CancellationToken cancellationToken = default)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var requestType = request.GetType();

                var handlerType = typeof(IEventHandler<>).MakeGenericType(requestType);
                var handlers = scope.ServiceProvider.GetServices(handlerType).Cast<object>();

                var behaviorType = typeof(IEventCustomPipeline<>).MakeGenericType(requestType);
                var behaviors = scope.ServiceProvider.GetServices(behaviorType).Reverse().ToList(); // it could be more of pipeline

                EventHandlerDelegate handlerDelegate = async (cancellationToken) =>
                {
                    var orderedHandlers = handlers
                        .OrderBy(h =>
                        {
                            var type = h.GetType();
                            var orderAttr = type.GetCustomAttribute<HandlerOrderAttribute>();
                            return orderAttr?.Priority ?? 0;
                        })
                        .ToList();

                    foreach (var handler in orderedHandlers)
                    {
                        var handleMethod = handler.GetType().GetMethod("Handle");
                        var canHandleMethod = handler.GetType().GetMethod("CanHandle");

                        if (handleMethod == null)
                        {
                            _logger.LogError($"Handler method 'Handle' not found for {requestType.Name}");
                            continue;
                        }

                        bool canHandle = true;
                        if (canHandleMethod != null)
                        {
                            var canHandleResult = canHandleMethod.Invoke(handler, new object[] { request });
                            if (canHandleResult is bool result)
                                canHandle = result;
                        }

                        if (!canHandle)
                        {
                            _logger.LogInformation(
                                $"Handler {handler.GetType().Name} cannot handle event {requestType.Name}. Skipping.");
                            continue;
                        }

                        try
                        {
                            if (handleMethod.Invoke(handler, new object[] { request }) is Task task)
                                await task.ConfigureAwait(false);
                            else
                                _logger.LogError($"Handler {handler.GetType().Name}.Handle did not return a Task.");
                        }
                        catch (TargetInvocationException ex)
                        {
                            var inner = ex.InnerException ?? ex;
                            _logger.LogError(inner, $"Error in handler {handler.GetType().Name}");
                            throw new InvalidOperationException(
                                $"Handler {handler.GetType().Name} failed for event {requestType.Name}", inner);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error in handler {handler.GetType().Name}");
                            throw new InvalidOperationException(
                                $"Handler {handler.GetType().Name} failed for event {requestType.Name}", ex);
                        }
                    }
                };


                foreach (var behavior in behaviors)
                {
                    var next = handlerDelegate;
                    var behaviorMethod = behavior.GetType().GetMethod("Handle");

                    if (behaviorMethod == null)
                        throw new InvalidOperationException($"Pipeline Handle method not found for {behavior.GetType().Name}");

                    handlerDelegate = async (cancellationToken) =>
                    {
                        var task = (Task)behaviorMethod.Invoke(behavior, new object[] { request, next, cancellationToken });

                        using (cancellationToken.Register(() =>
                        {
                            if (!task.IsCompleted)
                            {
                                throw new OperationCanceledException("Task has been cancelled.");
                            }
                        }))
                        {
                            await task;
                        }

                    };
                }

                await handlerDelegate(cancellationToken);
            }
        }
    }
}
