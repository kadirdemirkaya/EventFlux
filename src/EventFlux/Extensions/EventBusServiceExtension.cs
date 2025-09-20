using EventFlux.Abstractions;
using EventFlux.Behaviors;
using EventFlux.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace EventFlux.Extensions
{
    public static class EventBusServiceExtension
    {
        public static IServiceCollection AddEventBus(this IServiceCollection services, params Assembly[] assemblies)
        {
            List<Type> handlers = new();
            Dictionary<Type, List<Type>> internalEventHandlers = new();
            Dictionary<Type, Type> internalEventMaps = new();
            EventService _dictionaryService;
            EventMapService _eventDictionaryMapService;

            var handlerTypesWithResponse = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsInterface && !t.IsAbstract)
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<,>)));

            handlers.AddRange(handlerTypesWithResponse);

            foreach (var handlerType in handlerTypesWithResponse)
            {
                var interfaceTypes = handlerType.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<,>));

                foreach (var interfaceType in interfaceTypes)
                {
                    var requestInjectType = typeof(IEventHandler<,>).MakeGenericType(interfaceType.GenericTypeArguments);

                    services.AddTransient(requestInjectType, handlerType);

                    var genericArgs = interfaceType.GetGenericArguments();
                    var requestType = genericArgs[0];
                    var responseType = genericArgs[1];

                    {
                        if (!internalEventHandlers.ContainsKey(requestType))
                        {
                            internalEventHandlers[requestType] = new List<Type>();
                        }

                        if (!internalEventHandlers[requestType].Contains(handlerType))
                        {
                            internalEventHandlers[requestType].Add(handlerType);
                        }
                    }
                    {
                        if (!internalEventMaps.ContainsKey(requestType))
                        {
                            internalEventMaps[requestType] = responseType;
                        }
                    }
                }
            }

            var handlerTypes = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsInterface && !t.IsAbstract)
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>)));

            handlers.AddRange(handlerTypes);

            foreach (var handlerType in handlerTypes)
            {
                var interfaceTypes = handlerType.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));

                foreach (var interfaceType in interfaceTypes)
                {
                    var requestInjectType = typeof(IEventHandler<>).MakeGenericType(interfaceType.GenericTypeArguments);
                    services.TryAddEnumerable(ServiceDescriptor.Transient(requestInjectType, handlerType));

                    var genericArgs = interfaceType.GetGenericArguments();
                    var requestType = genericArgs[0];

                    {
                        if (!internalEventHandlers.ContainsKey(requestType))
                        {
                            internalEventHandlers[requestType] = new List<Type>();
                        }

                        if (!internalEventHandlers[requestType].Contains(handlerType))
                        {
                            internalEventHandlers[requestType].Add(handlerType);
                        }
                    }
                }
            }

            _dictionaryService = new(assemblies, internalEventHandlers);
            _eventDictionaryMapService = new(assemblies, internalEventMaps);

            //services.AddScoped(sp => new EventBus(sp, assemblies, _dictionaryService, _eventDictionaryMapService, handlers, sp.GetRequiredService<ILogger<EventBus>>()));

            services.AddScoped<IEventBus>(sp =>
                new EventBus(
                    sp,
                    assemblies,
                    _dictionaryService,
                    _eventDictionaryMapService,
                    handlers,
                    sp.GetRequiredService<ILogger<EventBus>>()
                )
            );

            return services;
        }

        public static IServiceCollection AddEventDispatcher(this IServiceCollection services)
        {
            services.AddTransient<IEventDispatcher, EventDispatcher>();

            return services;
        }

        public static IServiceCollection AddEventLogging(this IServiceCollection services)
        {
            services.AddTransient(typeof(IEventCustomPipeline<>), typeof(LoggingBehavior<>));
            
            services.AddTransient(typeof(IEventCustomPipeline<,>), typeof(LoggingBehavior<,>));

            return services;
        }

        public static IServiceCollection AddEventTimeout(this IServiceCollection services)
        {
            services.AddTransient(typeof(IEventCustomPipeline<>), typeof(TimeoutBehavior<>));

            services.AddTransient(typeof(IEventCustomPipeline<,>), typeof(TimeoutBehavior<,>));

            return services;
        }
    }
}
