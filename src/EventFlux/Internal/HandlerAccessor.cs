using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using EventFlux.Attributes;

namespace EventFlux.Internal
{
    internal sealed class HandlerAccessor
    {
        private HandlerAccessor(
            Type handlerType,
            int priority,
            Func<object, object, object> handle,
            Func<object, object, bool>? canHandle)
        {
            HandlerType = handlerType;
            Priority = priority;
            Handle = handle;
            CanHandle = canHandle;
        }

        public Type HandlerType { get; }

        public int Priority { get; }

        public Func<object, object, object> Handle { get; }

        public Func<object, object, bool>? CanHandle { get; }

        private static readonly ConcurrentDictionary<(Type HandlerType, Type EventType), HandlerAccessor?> Cache = new();

        public static HandlerAccessor? ForConcreteType(Type handlerType, Type eventType)
        {
            return Cache.GetOrAdd((handlerType, eventType), key => Build(key.HandlerType, key.EventType));
        }

        private static HandlerAccessor? Build(Type handlerType, Type eventType)
        {
            var handleMethod = handlerType.GetMethod(
                "Handle",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                new[] { eventType },
                modifiers: null);

            if (handleMethod == null)
                return null;

            var canHandleMethod = handlerType.GetMethod(
                "CanHandle",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                new[] { eventType },
                modifiers: null);

            var priority = handlerType.GetCustomAttribute<HandlerOrderAttribute>()?.Priority ?? 0;

            return new HandlerAccessor(
                handlerType,
                priority,
                CompileInvoker(handlerType, eventType, handleMethod),
                canHandleMethod == null ? null : CompilePredicate(handlerType, eventType, canHandleMethod));
        }

        public static Func<object, object, object> CompileInvoker(Type targetType, Type argumentType, MethodInfo method)
        {
            var targetParameter = Expression.Parameter(typeof(object), "target");
            var argumentParameter = Expression.Parameter(typeof(object), "argument");

            var call = Expression.Call(
                Expression.Convert(targetParameter, targetType),
                method,
                Expression.Convert(argumentParameter, argumentType));

            return Expression
                .Lambda<Func<object, object, object>>(
                    Expression.Convert(call, typeof(object)),
                    targetParameter,
                    argumentParameter)
                .Compile();
        }

        private static Func<object, object, bool> CompilePredicate(Type targetType, Type argumentType, MethodInfo method)
        {
            var targetParameter = Expression.Parameter(typeof(object), "target");
            var argumentParameter = Expression.Parameter(typeof(object), "argument");

            var call = Expression.Call(
                Expression.Convert(targetParameter, targetType),
                method,
                Expression.Convert(argumentParameter, argumentType));

            return Expression
                .Lambda<Func<object, object, bool>>(call, targetParameter, argumentParameter)
                .Compile();
        }
    }
}
