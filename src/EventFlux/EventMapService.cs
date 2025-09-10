using System.Reflection;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace EventFlux
{
    public class EventMapService
    {
        private readonly IEnumerable<Assembly> _assemblies;
        private readonly ConcurrentDictionary<Type, Type> _internalEventMaps;

        public EventMapService()
        {
            _assemblies = null;
            _internalEventMaps = new ConcurrentDictionary<Type, Type>();
        }
        public EventMapService(IEnumerable<Assembly> assemblies)
        {
            _assemblies = assemblies;
            _internalEventMaps = new ConcurrentDictionary<Type, Type>();
        }

        public EventMapService(IEnumerable<Assembly> assemblies, Dictionary<Type, Type> internalEventMaps)
        {
            _assemblies = assemblies;
            _internalEventMaps = internalEventMaps != null
                ? new ConcurrentDictionary<Type, Type>(internalEventMaps)
                : new ConcurrentDictionary<Type, Type>();
        }

        public Dictionary<Type, Type> InternalEventMaps => _internalEventMaps.ToDictionary(kv => kv.Key, kv => kv.Value);

        public bool TryGetValue(Type eventRequestType, out Type? responseType)
        {
            if (eventRequestType == null)
            {
                responseType = null;
                return false;
            }

            return _internalEventMaps.TryGetValue(eventRequestType, out responseType);
        }

        public bool IsMap<TEvent>(TEvent? @event)
        {
            var name = @event?.ToString();
            Type? eventType = GetEventValue(name);

            if (eventType == null)
                return false;

            return _internalEventMaps.ContainsKey(eventType);
        }

        public void AddMap<TEvent, TResponse>()
          where TResponse : class
        {
            var eventType = typeof(TEvent);
            var responseType = typeof(TResponse);

            _internalEventMaps.TryAdd(eventType, responseType);
        }

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
                Type eventRequest = entry.Key;  // EventRequest
                Type eventResponse = entry.Value;   // EventResponse

                if (eventRequest.FullName == @event)
                {
                    return (eventRequest, eventResponse);
                }
            }
            return (null, null);
        }
    }
}
