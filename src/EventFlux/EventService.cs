using System.Reflection;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

namespace EventFlux
{
    public class EventService
    {
        private readonly IEnumerable<Assembly> _assemblies;
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<Type, byte>> _internalEventHandlers;

        public EventService()
        {
            _assemblies = null;
            _internalEventHandlers = new ConcurrentDictionary<Type, ConcurrentDictionary<Type, byte>>();
        }
        public EventService(IEnumerable<Assembly> assemblies)
        {
            _assemblies = assemblies;
            _internalEventHandlers = new ConcurrentDictionary<Type, ConcurrentDictionary<Type, byte>>();
        }
        public EventService(IEnumerable<Assembly> assemblies, Dictionary<Type, List<Type>> internalEventHandlers)
        {
            _assemblies = assemblies;
            if (internalEventHandlers != null)
            {
                var map = new ConcurrentDictionary<Type, ConcurrentDictionary<Type, byte>>();
                foreach (var kv in internalEventHandlers)
                {
                    var inner = new ConcurrentDictionary<Type, byte>(kv.Value.ToDictionary(h => h, h => (byte)0));
                    map[kv.Key] = inner;
                }
                _internalEventHandlers = map;
            }
            else
            {
                _internalEventHandlers = new ConcurrentDictionary<Type, ConcurrentDictionary<Type, byte>>();
            }
        }

        public Dictionary<Type, List<Type>> InternalEventHandlers => _internalEventHandlers.ToDictionary(kv => kv.Key, kv => kv.Value.Keys.ToList());

        public bool IsSubscribe<TEvent, THandler>(TEvent @event, THandler @handler)
            where THandler : class
        {
            var eventType = typeof(TEvent);
            var handlerType = typeof(THandler);

            if (!_internalEventHandlers.TryGetValue(eventType, out var inner))
                return false;

            return inner.ContainsKey(handlerType);
        }

        public void Subscribe<TEvent, THandler>()
            where THandler : class
        {
            var eventType = typeof(TEvent);
            var handlerType = typeof(THandler);

            var inner = _internalEventHandlers.GetOrAdd(eventType, _ => new ConcurrentDictionary<Type, byte>());
            inner.TryAdd(handlerType, 0);
        }

        public List<Type> GetHandlersForEvent(Type eventType)
        {
            if (_internalEventHandlers.TryGetValue(eventType, out var inner))
            {
                return inner.Keys.ToList();
            }

            return null;
        }

        public void Subscribe<TEvent, THandler>(TEvent @event, THandler @handler)
            where THandler : class
        {
            var eventType = typeof(TEvent);
            var handlerType = typeof(THandler);

            var inner = _internalEventHandlers.GetOrAdd(eventType, _ => new ConcurrentDictionary<Type, byte>());
            inner.TryAdd(handlerType, 0);
        }

        public void Unsubscribe<TEvent, THandler>()
            where THandler : class
        {
            var eventType = typeof(TEvent);
            var handlerType = typeof(THandler);

            if (_internalEventHandlers.TryGetValue(eventType, out var inner))
            {
                inner.TryRemove(handlerType, out _);

                if (inner.IsEmpty)
                {
                    _internalEventHandlers.TryRemove(eventType, out _);
                }
            }
        }

        public void FindEventHandlers()
        {
            var handlerTypes = _assemblies
               .SelectMany(a => a.GetTypes())
               .Where(t => !t.IsInterface && !t.IsAbstract)
               .Where(t => t.GetInterfaces().Any(i => (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<,>)) || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))));

            foreach (var handlerType in handlerTypes)
            {
                var interfaceTypes = handlerType.GetInterfaces()
                    .Where(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IEventHandler<,>) || i.GetGenericTypeDefinition() == typeof(IEventHandler<>)));

                foreach (var interfaceType in interfaceTypes)
                {
                    var genericArgs = interfaceType.GetGenericArguments();
                    var requestType = genericArgs[0];

                    var inner = _internalEventHandlers.GetOrAdd(requestType, _ => new ConcurrentDictionary<Type, byte>());
                    inner.TryAdd(handlerType, 0);
                }
            }
        }
    }
}
