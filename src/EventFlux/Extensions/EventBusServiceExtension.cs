using EventFlux.Abstractions;
using EventFlux.Behaviors;
using EventFlux.Internal;
using EventFlux.Options;
using EventFlux.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
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
        /// <remarks>
        /// Calling this method more than once is safe: handlers, <see cref="EventService"/>, <see cref="EventMapService"/>
        /// and <see cref="IEventBus"/> are registered only once, and handlers discovered by later calls are merged into
        /// the existing <see cref="EventService"/> and <see cref="EventMapService"/>. Types that fail to load from an
        /// assembly are skipped instead of aborting the scan.
        /// </remarks>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="assemblies">Assemblies to scan for event handlers.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="assemblies"/> is null, empty, or contains only null items.</exception>
        /// <exception cref="InvalidOperationException">Thrown when duplicate handlers for the same request type are detected, or when a scanned handler is an open generic type definition.</exception>
        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
        public static IServiceCollection AddEventBus(this IServiceCollection services, params Assembly[] assemblies)
        {
            return AddEventBus(services, (Action<EventFluxOptions>?)null, assemblies);
        }

        /// <summary>
        /// Registers EventFlux event bus infrastructure with custom options, scanning provided assemblies for handlers.
        /// </summary>
        /// <remarks>
        /// Calling this method more than once is safe: handlers, <see cref="EventService"/>, <see cref="EventMapService"/>
        /// and <see cref="IEventBus"/> are registered only once, and handlers discovered by later calls are merged into
        /// the existing <see cref="EventService"/> and <see cref="EventMapService"/>. Types that fail to load from an
        /// assembly are skipped instead of aborting the scan.
        /// </remarks>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="configureOptions">Action to configure <see cref="EventFluxOptions"/>.</param>
        /// <param name="assemblies">Assemblies to scan for event handlers.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="assemblies"/> is null, empty, or contains only null items.</exception>
        /// <exception cref="InvalidOperationException">Thrown when duplicate handlers for the same request type are detected, or when a scanned handler is an open generic type definition.</exception>
        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
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

            var validAssemblies = assemblies.Where(a => a is not null).Distinct().ToArray();
            if (validAssemblies.Length == 0)
            {
                throw new ArgumentException("At least one valid assembly must be provided.", nameof(assemblies));
            }

            var scan = ScanHandlerTypes(validAssemblies);

            EventFluxOptions options;
            if (configureOptions is not null)
            {
                options = new EventFluxOptions();
                configureOptions(options);
                services.RemoveAll<EventFluxOptions>();
                services.AddSingleton(options);
            }
            else
            {
                var existingDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(EventFluxOptions));
                options = existingDescriptor?.ImplementationInstance as EventFluxOptions ?? new EventFluxOptions();
                services.TryAddSingleton(options);
            }

            var eventService = ExistingInstance<EventService>(services);
            if (eventService is null)
            {
                eventService = new EventService(validAssemblies);
                services.AddSingleton(eventService);
            }

            var eventMapService = ExistingInstance<EventMapService>(services);
            if (eventMapService is null)
            {
                eventMapService = new EventMapService(validAssemblies);
                services.AddSingleton(eventMapService);
            }

            foreach (var registration in scan.RequestHandlers)
            {
                services.TryAddEnumerable(new ServiceDescriptor(registration.ServiceType, registration.HandlerType, options.HandlerLifetime));
                eventService.Subscribe(registration.RequestType, registration.HandlerType);
                eventMapService.AddMap(registration.RequestType, registration.ResponseType!);
            }

            foreach (var registration in scan.NotificationHandlers)
            {
                services.TryAddEnumerable(new ServiceDescriptor(registration.ServiceType, registration.HandlerType, options.HandlerLifetime));
                eventService.Subscribe(registration.RequestType, registration.HandlerType);
            }

            services.TryAddSingleton<EventStackService>();

            services.TryAddScoped<IEventBus>(sp =>
                new EventBus(
                    sp,
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
        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
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
        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
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

        private readonly struct HandlerRegistration
        {
            public HandlerRegistration(Type handlerType, Type serviceType, Type requestType, Type? responseType)
            {
                HandlerType = handlerType;
                ServiceType = serviceType;
                RequestType = requestType;
                ResponseType = responseType;
            }

            public Type HandlerType { get; }

            public Type ServiceType { get; }

            public Type RequestType { get; }

            public Type? ResponseType { get; }
        }

        private sealed class HandlerScanResult
        {
            public List<HandlerRegistration> RequestHandlers { get; } = new();

            public List<HandlerRegistration> NotificationHandlers { get; } = new();
        }

        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
        private static HandlerScanResult ScanHandlerTypes(IEnumerable<Assembly> assemblies)
        {
            var result = new HandlerScanResult();
            var requestToHandlerMap = new Dictionary<Type, Type>();

            var candidateTypes = assemblies
                .SelectMany(a => a.GetLoadableTypes())
                .Where(t => !t.IsInterface && !t.IsAbstract)
                .Distinct();

            foreach (var handlerType in candidateTypes)
            {
                var interfaces = handlerType.GetInterfaces();

                var requestInterfaces = interfaces
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<,>))
                    .ToArray();

                var notificationInterfaces = interfaces
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                    .ToArray();

                if (requestInterfaces.Length == 0 && notificationInterfaces.Length == 0)
                {
                    continue;
                }

                if (handlerType.IsGenericTypeDefinition)
                {
                    throw new InvalidOperationException(
                        $"Handler type '{handlerType.FullName ?? handlerType.Name}' is an open generic type definition and cannot be registered by AddEventBus. " +
                        "Assembly scanning only registers closed handler types: derive a non-generic handler for each event, " +
                        "or move the open generic type out of the scanned assemblies.");
                }

                foreach (var interfaceType in requestInterfaces)
                {
                    var genericArgs = interfaceType.GetGenericArguments();
                    var requestType = genericArgs[0];

                    if (requestToHandlerMap.TryGetValue(requestType, out var existingHandlerType))
                    {
                        if (existingHandlerType != handlerType)
                        {
                            throw new InvalidOperationException(
                                $"Duplicate handler registration detected for request type '{requestType.FullName}'. " +
                                $"Conflicting handlers: '{existingHandlerType.FullName}' and '{handlerType.FullName}'. " +
                                "A request can only have one handler.");
                        }
                        continue;
                    }

                    requestToHandlerMap[requestType] = handlerType;

                    result.RequestHandlers.Add(new HandlerRegistration(
                        handlerType,
                        typeof(IEventHandler<,>).MakeGenericType(interfaceType.GenericTypeArguments),
                        requestType,
                        genericArgs[1]));
                }

                foreach (var interfaceType in notificationInterfaces)
                {
                    result.NotificationHandlers.Add(new HandlerRegistration(
                        handlerType,
                        typeof(IEventHandler<>).MakeGenericType(interfaceType.GenericTypeArguments),
                        interfaceType.GetGenericArguments()[0],
                        null));
                }
            }

            return result;
        }

        private static T? ExistingInstance<T>(IServiceCollection services) where T : class
        {
            return services.LastOrDefault(d => d.ServiceType == typeof(T) && !d.IsKeyedService)?.ImplementationInstance as T;
        }
    }
}
