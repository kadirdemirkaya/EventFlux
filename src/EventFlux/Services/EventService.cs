using System.Reflection;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;
using EventFlux.Abstractions;

namespace EventFlux.Services
{
    /// <summary>
    /// Service managing event-to-handler type mappings and registrations.
    /// </summary>
    public class EventService
    {
        private readonly IEnumerable<Assembly> _assemblies;
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<Type, byte>> _internalEventHandlers;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventService"/> class.
        /// </summary>
        public EventService()
        {
            _assemblies = null;
            _internalEventHandlers = new ConcurrentDictionary<Type, ConcurrentDictionary<Type, byte>>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventService"/> class with assemblies.
        /// </summary>
        /// <param name="assemblies">Assemblies containing event handlers.</param>
        public EventService(IEnumerable<Assembly> assemblies)
        {
            _assemblies = assemblies;
            _internalEventHandlers = new ConcurrentDictionary<Type, ConcurrentDictionary<Type, byte>>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventService"/> class with initial event handlers.
        /// </summary>
        /// <param name="assemblies">Assemblies containing event handlers.</param>
        /// <param name="internalEventHandlers">Initial map of event types to handler types.</param>
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

        /// <summary>
        /// Gets the current dictionary of event types and their subscribed handler types.
        /// </summary>
        public Dictionary<Type, List<Type>> InternalEventHandlers => _internalEventHandlers.ToDictionary(kv => kv.Key, kv => kv.Value.Keys.ToList());

        /// <summary>
        /// Checks whether a handler is subscribed to an event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <typeparam name="THandler">The handler type.</typeparam>
        /// <param name="event">The event instance.</param>
        /// <param name="handler">The handler instance.</param>
        /// <returns><c>true</c> if subscribed; otherwise <c>false</c>.</returns>
        public bool IsSubscribe<TEvent, THandler>(TEvent @event, THandler @handler)
            where THandler : class
        {
            var eventType = typeof(TEvent);
            var handlerType = typeof(THandler);

            if (!_internalEventHandlers.TryGetValue(eventType, out var inner))
                return false;

            return inner.ContainsKey(handlerType);
        }

        /// <summary>
        /// Subscribes a handler type to an event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <typeparam name="THandler">The handler type.</typeparam>
        public void Subscribe<TEvent, THandler>()
            where THandler : class
        {
            var eventType = typeof(TEvent);
            var handlerType = typeof(THandler);

            var inner = _internalEventHandlers.GetOrAdd(eventType, _ => new ConcurrentDictionary<Type, byte>());
            inner.TryAdd(handlerType, 0);
        }

        /// <summary>
        /// Gets the list of handler types registered for the specified event type.
        /// </summary>
        /// <param name="eventType">The event type.</param>
        /// <returns>A list of handler types, or null if none registered.</returns>
        public List<Type> GetHandlersForEvent(Type eventType)
        {
            if (_internalEventHandlers.TryGetValue(eventType, out var inner))
            {
                return inner.Keys.ToList();
            }

            return null;
        }

        /// <summary>
        /// Subscribes a handler instance to an event.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <typeparam name="THandler">The handler type.</typeparam>
        /// <param name="event">The event instance.</param>
        /// <param name="handler">The handler instance.</param>
        public void Subscribe<TEvent, THandler>(TEvent @event, THandler @handler)
            where THandler : class
        {
            var eventType = typeof(TEvent);
            var handlerType = typeof(THandler);

            var inner = _internalEventHandlers.GetOrAdd(eventType, _ => new ConcurrentDictionary<Type, byte>());
            inner.TryAdd(handlerType, 0);
        }

        /// <summary>
        /// Unsubscribes a handler type from an event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <typeparam name="THandler">The handler type.</typeparam>
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

        /// <summary>
        /// Scans configured assemblies to discover and register event handler types.
        /// </summary>
        public void FindEventHandlers()
        {
            var handlerTypes = _assemblies
               .SelectMany(a => a.GetTypes())
               .Where(t => !t.IsInterface && !t.IsAbstract)
               .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<,>) || i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>)));

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
