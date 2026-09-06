using System.Reflection;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EventFlux.Abstractions;

namespace EventFlux
{
    /// <summary>
    /// Service managing request-to-response type associations.
    /// </summary>
    public class EventMapService
    {
        private readonly IEnumerable<Assembly> _assemblies;
        private readonly ConcurrentDictionary<Type, Type> _internalEventMaps;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventMapService"/> class.
        /// </summary>
        public EventMapService()
        {
            _assemblies = null;
            _internalEventMaps = new ConcurrentDictionary<Type, Type>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventMapService"/> class with assemblies.
        /// </summary>
        /// <param name="assemblies">Assemblies containing event maps.</param>
        public EventMapService(IEnumerable<Assembly> assemblies)
        {
            _assemblies = assemblies;
            _internalEventMaps = new ConcurrentDictionary<Type, Type>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventMapService"/> class with existing event maps.
        /// </summary>
        /// <param name="assemblies">Assemblies containing event maps.</param>
        /// <param name="internalEventMaps">Initial map of request types to response types.</param>
        public EventMapService(IEnumerable<Assembly> assemblies, Dictionary<Type, Type> internalEventMaps)
        {
            _assemblies = assemblies;
            _internalEventMaps = internalEventMaps != null
                ? new ConcurrentDictionary<Type, Type>(internalEventMaps)
                : new ConcurrentDictionary<Type, Type>();
        }

        /// <summary>
        /// Gets the dictionary of request types to response types.
        /// </summary>
        public Dictionary<Type, Type> InternalEventMaps => _internalEventMaps.ToDictionary(kv => kv.Key, kv => kv.Value);

        /// <summary>
        /// Attempts to get the mapped response type for a given request type.
        /// </summary>
        /// <param name="eventRequestType">The request event type.</param>
        /// <param name="responseType">The resolved response type if found.</param>
        /// <returns><c>true</c> if mapped; otherwise <c>false</c>.</returns>
        public bool TryGetValue(Type eventRequestType, out Type? responseType)
        {
            if (eventRequestType == null)
            {
                responseType = null;
                return false;
            }

            return _internalEventMaps.TryGetValue(eventRequestType, out responseType);
        }

        /// <summary>
        /// Checks whether the event is mapped to a response.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">The event instance.</param>
        /// <returns><c>true</c> if mapped; otherwise <c>false</c>.</returns>
        public bool IsMap<TEvent>(TEvent? @event)
        {
            var name = @event?.ToString();
            Type? eventType = GetEventValue(name);

            if (eventType == null)
                return false;

            return _internalEventMaps.ContainsKey(eventType);
        }

        /// <summary>
        /// Adds a mapping between a request type and response type.
        /// </summary>
        /// <typeparam name="TEvent">The event request type.</typeparam>
        /// <typeparam name="TResponse">The event response type.</typeparam>
        public void AddMap<TEvent, TResponse>()
          where TResponse : class
        {
            var eventType = typeof(TEvent);
            var responseType = typeof(TResponse);

            _internalEventMaps.TryAdd(eventType, responseType);
        }

        /// <summary>
        /// Adds a mapping between an event instance's type and a response type.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="event">The event instance.</param>
        /// <param name="response">The response instance.</param>
        public void AddMap<TEvent, TResponse>(TEvent? @event, TResponse response)
            where TResponse : class
        {
            var name = @event?.ToString();
            Type? eventRequest = GetEventValue(name);

            if (eventRequest != null)
            {
                _internalEventMaps.TryAdd(eventRequest, typeof(TResponse));
            }
        }

        /// <summary>
        /// Removes the mapping for the given event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="event">The event instance.</param>
        /// <param name="response">The response instance.</param>
        public void RemoveMap<TEvent, TResponse>(TEvent? @event, TResponse response)
            where TResponse : class
        {
            var name = @event?.ToString();
            Type? eventType = GetEventValue(name);

            if (eventType != null)
            {
                _internalEventMaps.TryRemove(eventType, out _);
            }
        }

        /// <summary>
        /// Gets the response type by event type name string.
        /// </summary>
        /// <param name="event">The event type full name string.</param>
        /// <param name="responseType">The resolved response type if found.</param>
        /// <returns><c>true</c> if found; otherwise <c>false</c>.</returns>
        public bool GetValue(string? @event, out Type? responseType)
        {
            if (string.IsNullOrEmpty(@event))
            {
                responseType = null;
                return false;
            }

            foreach (var kv in _internalEventMaps)
            {
                if (kv.Key.FullName == @event)
                {
                    responseType = kv.Value;
                    return true;
                }
            }

            responseType = null;
            return false;
        }

        /// <summary>
        /// Scans assemblies for request-response handlers and populates mappings.
        /// </summary>
        public void FindEvents()
        {
            var handlerTypesWithResponse = _assemblies
             .SelectMany(a => a.GetTypes())
             .Where(t => !t.IsInterface && !t.IsAbstract)
             .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<,>)));

            foreach (var handlerType in handlerTypesWithResponse)
            {
                var interfaceTypes = handlerType.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<,>));

                foreach (var interfaceType in interfaceTypes)
                {
                    var genericArgs = interfaceType.GetGenericArguments();
                    var requestType = genericArgs[0];
                    var responseType = genericArgs[1];

                    _internalEventMaps.TryAdd(requestType, responseType);
                }
            }
        }

        private Type? GetEventValue(string? @event)
        {
            if (string.IsNullOrEmpty(@event)) return null;

            foreach (var entry in _internalEventMaps)
            {
                var eventRequest = entry.Key;

                if (eventRequest.FullName == @event)
                    return eventRequest;
            }
            return null;
        }

        private (Type? eventRequest, Type? eventResponse) GetValue(string? @event)
        {
            if (string.IsNullOrEmpty(@event)) return (null, null);

            foreach (var entry in _internalEventMaps)
            {
                Type eventRequest = entry.Key;
                Type eventResponse = entry.Value;

                if (eventRequest.FullName == @event)
                {
                    return (eventRequest, eventResponse);
                }
            }
            return (null, null);
        }
    }
}
