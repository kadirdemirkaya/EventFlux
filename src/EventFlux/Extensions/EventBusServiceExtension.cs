using EventFlux.Abstractions;
using EventFlux.Behaviors;
using EventFlux.Options;
using EventFlux.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace EventFlux.Extensions
{
    /// <summary>
    /// Extension methods for setting up EventFlux services in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class EventBusServiceExtension
    {
        /// <summary>
        /// Registers EventFlux event bus infrastructure, scanning provided assemblies for handlers.
        /// </summary>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="assemblies">Assemblies to scan for event handlers.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="assemblies"/> is null, empty, or contains only null items.</exception>
        public static IServiceCollection AddEventBus(this IServiceCollection services, params Assembly[] assemblies)
        {
            return AddEventBus(services, (Action<EventFluxOptions>?)null, assemblies);
        }

        /// <summary>
        /// Registers EventFlux event bus infrastructure with custom options, scanning provided assemblies for handlers.
        /// </summary>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="configureOptions">Action to configure <see cref="EventFluxOptions"/>.</param>
        /// <param name="assemblies">Assemblies to scan for event handlers.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="assemblies"/> is null, empty, or contains only null items.</exception>
        public static IServiceCollection AddEventBus(this IServiceCollection services, Action<EventFluxOptions>? configureOptions, params Assembly[] assemblies)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (assemblies is null || assemblies.Length == 0)
            {
                throw new ArgumentException("At least one assembly must be provided.", nameof(assemblies));
            }

            var validAssemblies = assemblies.Where(a => a is not null).ToArray();
            if (validAssemblies.Length == 0)
            {
                throw new ArgumentException("At least one valid assembly must be provided.", nameof(assemblies));
            }

            if (configureOptions is not null)
            {
                var options = new EventFluxOptions();
                configureOptions(options);
                services.RemoveAll<EventFluxOptions>();
                services.AddSingleton(options);
            }
            else
            {
                services.TryAddSingleton<EventFluxOptions>();
            }

            List<Type> handlers = new();
            Dictionary<Type, List<Type>> internalEventHandlers = new();
            Dictionary<Type, Type> internalEventMaps = new();
            EventService _dictionaryService;
            EventMapService _eventDictionaryMapService;

            var handlerTypesWithResponse = validAssemblies
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
                    services.AddTransient(handlerType);

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

            var handlerTypes = validAssemblies
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

            _dictionaryService = new(validAssemblies, internalEventHandlers);
            _eventDictionaryMapService = new(validAssemblies, internalEventMaps);

            services.AddSingleton<EventService>(_dictionaryService);
            services.AddSingleton<EventMapService>(_eventDictionaryMapService);
            services.TryAddSingleton<EventStackService>();

            services.AddScoped<IEventBus>(sp =>
                new EventBus(
                    sp,
                    validAssemblies,
                    _dictionaryService,
                    _eventDictionaryMapService,
                    handlers,
                    sp.GetRequiredService<ILogger<EventBus>>(),
                    sp.GetService<EventFluxOptions>(),
                    sp.GetService<EventStackService>()
                )
            );

            return services;
        }

        /// <summary>
        /// Registers <see cref="IEventDispatcher"/> for pipeline behavior-aware event dispatching.
        /// </summary>
        /// <param name="services">The service collection to register into.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        public static IServiceCollection AddEventDispatcher(this IServiceCollection services)
        {
            return AddEventDispatcher(services, (Action<EventFluxOptions>?)null);
        }

        /// <summary>
        /// Registers <see cref="IEventDispatcher"/> for pipeline behavior-aware event dispatching with custom options.
        /// </summary>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="configureOptions">Action to configure <see cref="EventFluxOptions"/>.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        public static IServiceCollection AddEventDispatcher(this IServiceCollection services, Action<EventFluxOptions>? configureOptions)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configureOptions is not null)
            {
                var options = new EventFluxOptions();
                configureOptions(options);
                services.RemoveAll<EventFluxOptions>();
                services.AddSingleton(options);
            }
            else
            {
                services.TryAddSingleton<EventFluxOptions>();
            }

            services.TryAddTransient<IEventDispatcher, EventDispatcher>();

            return services;
        }

        /// <summary>
        /// Registers built-in logging pipeline behaviors for request and notification dispatching.
        /// </summary>
        /// <param name="services">The service collection to register into.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        public static IServiceCollection AddEventLogging(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IEventCustomPipeline<>), typeof(LoggingBehavior<>)));
            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IEventCustomPipeline<,>), typeof(LoggingBehavior<,>)));

            return services;
        }

        /// <summary>
        /// Registers built-in timeout pipeline behaviors for request and notification dispatching.
        /// </summary>
        /// <param name="services">The service collection to register into.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        public static IServiceCollection AddEventTimeout(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IEventCustomPipeline<>), typeof(TimeoutBehavior<>)));
            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IEventCustomPipeline<,>), typeof(TimeoutBehavior<,>)));

            return services;
        }
    }
}
