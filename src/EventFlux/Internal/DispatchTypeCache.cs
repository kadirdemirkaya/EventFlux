using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using EventFlux.Abstractions;

namespace EventFlux.Internal
{
    [RequiresDynamicCode(AotMessages.DynamicCode)]
    [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
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

        public static Func<object, object, object, CancellationToken, object> BehaviorInvoker(Type pipelineInterfaceType)
            => BehaviorInvokers.GetOrAdd(pipelineInterfaceType, BuildBehaviorInvoker);

        private static InterfaceAccessor BuildInterfaceAccessor(Type handlerInterfaceType)
        {
            var requestType = handlerInterfaceType.GetGenericArguments()[0];

            var handleMethod = handlerInterfaceType.GetMethod(
                "Handle",
                new[] { requestType, typeof(CancellationToken) })
                ?? throw new InvalidOperationException($"Handler method 'Handle' not found for {handlerInterfaceType.Name}");

            var canHandleMethod = handlerInterfaceType.GetMethod("CanHandle", new[] { requestType });

            return new InterfaceAccessor(
                HandlerAccessor.CompileInvoker(handlerInterfaceType, requestType, handleMethod),
                canHandleMethod == null
                    ? null
                    : HandlerAccessor.CompilePredicate(handlerInterfaceType, requestType, canHandleMethod));
        }

        private static Func<object, object, object, CancellationToken, object> BuildBehaviorInvoker(Type pipelineInterfaceType)
        {
            var method = pipelineInterfaceType.GetMethod("Handle")
                ?? throw new InvalidOperationException($"Pipeline Handle method not found for {pipelineInterfaceType.Name}");

            var parameters = method.GetParameters();

            var behaviorParameter = Expression.Parameter(typeof(object), "behavior");
            var requestParameter = Expression.Parameter(typeof(object), "request");
            var nextParameter = Expression.Parameter(typeof(object), "next");
            var tokenParameter = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            var call = Expression.Call(
                Expression.Convert(behaviorParameter, pipelineInterfaceType),
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
        public InterfaceAccessor(Func<object, object, CancellationToken, object> handle, Func<object, object, bool>? canHandle)
        {
            Handle = handle;
            CanHandle = canHandle;
        }

        public Func<object, object, CancellationToken, object> Handle { get; }

        public Func<object, object, bool>? CanHandle { get; }
    }
}
