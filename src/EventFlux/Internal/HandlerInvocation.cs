using System;
using System.Collections.Generic;
using System.Linq;

namespace EventFlux.Internal
{
    internal readonly struct HandlerInvocation
    {
        public HandlerInvocation(object handler, HandlerAccessor accessor)
        {
            Handler = handler;
            Accessor = accessor;
        }

        public object Handler { get; }

        public HandlerAccessor Accessor { get; }
    }

    internal static class HandlerInvocationBuilder
    {
        public static IReadOnlyList<object?> AsReadOnlyList(IEnumerable<object?> services)
        {
            return services as IReadOnlyList<object?> ?? services.ToArray();
        }

        public static HandlerInvocation[] Build(IEnumerable<object?> handlers, Type eventType, out int count)
        {
            var source = AsReadOnlyList(handlers);

            count = 0;

            if (source.Count == 0)
                return Array.Empty<HandlerInvocation>();

            var invocations = new HandlerInvocation[source.Count];

            for (var i = 0; i < source.Count; i++)
            {
                var handler = source[i];

                if (handler is null)
                    continue;

                var accessor = HandlerAccessor.ForConcreteType(handler.GetType(), eventType);

                if (accessor is null)
                    continue;

                invocations[count++] = new HandlerInvocation(handler, accessor);
            }

            SortByPriority(invocations, count);

            return invocations;
        }

        public static void SortByPriority(HandlerInvocation[] invocations, int count)
        {
            for (var i = 1; i < count; i++)
            {
                var current = invocations[i];
                var j = i - 1;

                while (j >= 0 && invocations[j].Accessor.Priority > current.Accessor.Priority)
                {
                    invocations[j + 1] = invocations[j];
                    j--;
                }

                invocations[j + 1] = current;
            }
        }
    }
}
