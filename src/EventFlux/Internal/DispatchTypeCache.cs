using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using EventFlux.Abstractions;

namespace EventFlux.Internal
{
    internal static class DispatchTypeCache
    {
        private static readonly ConcurrentDictionary<Type, Type> NotificationHandlerTypes = new();
        private static readonly ConcurrentDictionary<(Type, Type), Type> RequestHandlerTypes = new();
        private static readonly ConcurrentDictionary<Type, Type> NotificationPipelineTypes = new();
        private static readonly ConcurrentDictionary<(Type, Type), Type> RequestPipelineTypes = new();
        private static readonly ConcurrentDictionary<Type, InterfaceAccessor> InterfaceAccessors = new();
        private static readonly ConcurrentDictionary<Type, Func<object, object, object, CancellationToken, object>> BehaviorInvokers = new();

        public static Type NotificationHandlerType(Type eventType)
            => NotificationHandlerTypes.GetOrAdd(eventType, t => typeof(IEventHandler<>).MakeGenericType(t));

        public static Type RequestHandlerType(Type requestType, Type responseType)
            => RequestHandlerTypes.GetOrAdd((requestType, responseType), key => typeof(IEventHandler<,>).MakeGenericType(key.Item1, key.Item2));

        public static Type NotificationPipelineType(Type eventType)
            => NotificationPipelineTypes.GetOrAdd(eventType, t => typeof(IEventCustomPipeline<>).MakeGenericType(t));

        public static Type RequestPipelineType(Type requestType, Type responseType)
            => RequestPipelineTypes.GetOrAdd((requestType, responseType), key => typeof(IEventCustomPipeline<,>).MakeGenericType(key.Item1, key.Item2));

        public static InterfaceAccessor ForInterface(Type handlerInterfaceType)
            => InterfaceAccessors.GetOrAdd(handlerInterfaceType, BuildInterfaceAccessor);

        public static Func<object, object, object, CancellationToken, object> BehaviorInvoker(Type behaviorType)
            => BehaviorInvokers.GetOrAdd(behaviorType, BuildBehaviorInvoker);

        private static InterfaceAccessor BuildInterfaceAccessor(Type handlerInterfaceType)
        {
            var handleMethod = handlerInterfaceType.GetMethod("Handle")
                ?? throw new InvalidOperationException($"Handler method 'Handle' not found for {handlerInterfaceType.Name}");

            var requestType = handlerInterfaceType.GetGenericArguments()[0];

            var canHandleMethod = handlerInterfaceType.GetMethod("CanHandle");

            return new InterfaceAccessor(
                HandlerAccessor.CompileInvoker(handlerInterfaceType, requestType, handleMethod),
                canHandleMethod == null
                    ? null
                    : HandlerAccessor.CompileInvoker(handlerInterfaceType, requestType, canHandleMethod));
        }

        private static Func<object, object, object, CancellationToken, object> BuildBehaviorInvoker(Type behaviorType)
        {
            var method = behaviorType.GetMethod("Handle")
                ?? throw new InvalidOperationException($"Pipeline Handle method not found for {behaviorType.Name}");

            var parameters = method.GetParameters();

            var behaviorParameter = Expression.Parameter(typeof(object), "behavior");
            var requestParameter = Expression.Parameter(typeof(object), "request");
            var nextParameter = Expression.Parameter(typeof(object), "next");
            var tokenParameter = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            var call = Expression.Call(
                Expression.Convert(behaviorParameter, behaviorType),
                method,
                Expression.Convert(requestParameter, parameters[0].ParameterType),
                Expression.Convert(nextParameter, parameters[1].ParameterType),
                tokenParameter);

            return Expression
                .Lambda<Func<object, object, object, CancellationToken, object>>(
                    Expression.Convert(call, typeof(object)),
                    behaviorParameter,
                    requestParameter,
                    nextParameter,
                    tokenParameter)
                .Compile();
        }
    }

    internal sealed class InterfaceAccessor
    {
        public InterfaceAccessor(Func<object, object, object> handle, Func<object, object, object>? canHandle)
        {
            Handle = handle;
            CanHandle = canHandle;
        }

        public Func<object, object, object> Handle { get; }

        public Func<object, object, object>? CanHandle { get; }
    }
}
