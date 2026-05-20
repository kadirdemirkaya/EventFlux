using EventFlux.Abstractions;
using EventFlux.Attributes;
using EventFlux.Delegates;
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
        private static readonly SemaphoreSlim _publishSemaphore = new(Environment.ProcessorCount);

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
                var behaviors = scope.ServiceProvider.GetServices(behaviorType).Reverse().ToList();

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
                        _logger.LogInformation($"Handler {handlerType.Name} cannot handle event {requestType.Name}. Skipping.");

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
            cancellationToken.ThrowIfCancellationRequested();

            var requestType = request.GetType();
            var handlerInterface = typeof(IEventHandler<>).MakeGenericType(requestType);

            using var scope = _serviceProvider.CreateScope();

            var handlerTypes = scope.ServiceProvider
                .GetServices(handlerInterface)
                .OrderBy(h =>
                    h.GetType().GetCustomAttribute<HandlerOrderAttribute>()?.Priority ?? 0)
                .ToList();

            if (handlerTypes.Count == 0)
                return;

            var behaviorType = typeof(IEventCustomPipeline<>).MakeGenericType(requestType);

            var behaviors = scope.ServiceProvider
                .GetServices(behaviorType)
                .Cast<object>()
                .Reverse()
                .ToList();

            EventHandlerDelegate handlerDelegate = async ct =>
            {
                var tasks = handlerTypes.Select(handler => InvokeHandlerInstanceWithSemaphoreAsync(handler, request, ct));

                await Task.WhenAll(tasks).ConfigureAwait(false);
            };

            foreach (var behavior in behaviors)
            {
                var next = handlerDelegate;
                var method = behavior.GetType().GetMethod("Handle");

                handlerDelegate = async ct =>
                {
                    var task = (Task)method!.Invoke(behavior, new object[] { request, next, ct })!;

                    await task.ConfigureAwait(false);
                };
            }

            await handlerDelegate(cancellationToken).ConfigureAwait(false);
        }

        private async Task InvokeHandlerInstanceWithSemaphoreAsync(
            object handler,
            IEventRequest request,
            CancellationToken cancellationToken)
        {
            await _publishSemaphore.WaitAsync(cancellationToken);

            try
            {
                await InvokeHandlerAsync(handler, request, cancellationToken);
            }
            finally
            {
                _publishSemaphore.Release();
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

            var canHandleMethod = handlerType.GetMethod("CanHandle", new[] { requestType });
            var handleMethod = handlerType.GetMethod("Handle", new[] { requestType });

            if (handleMethod == null)
                throw new InvalidOperationException($"Handle method not found on {handlerType.Name}");

            if (canHandleMethod != null)
            {
                var canHandle = canHandleMethod.Invoke(handler, new object[] { request });

                if (canHandle is bool b && !b)
                    return;
            }

            if (handleMethod.Invoke(handler, new object[] { request }) is Task task)
                await task.ConfigureAwait(false);
            else
                throw new InvalidOperationException($"{handlerType.Name}.Handle must return Task");
        }
    }
}
