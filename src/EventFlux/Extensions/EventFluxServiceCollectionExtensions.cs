using EventFlux;
using EventFlux.Abstractions;
using EventFlux.Behaviors;
using EventFlux.Internal;
using EventFlux.Options;
using EventFlux.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for setting up EventFlux services in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class EventFluxServiceCollectionExtensions
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
        /// Registers EventFlux event bus infrastructure, scanning the assembly that declares <typeparamref name="TMarker"/> for handlers.
        /// </summary>
        /// <remarks>
        /// Equivalent to <c>AddEventBus(typeof(TMarker).Assembly)</c>.
        /// </remarks>
        /// <typeparam name="TMarker">Any type declared in the assembly to scan, typically <c>Program</c>.</typeparam>
        /// <param name="services">The service collection to register into.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when duplicate handlers for the same request type are detected, or when a scanned handler is an open generic type definition.</exception>
        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
        public static IServiceCollection AddEventBus<TMarker>(this IServiceCollection services)
        {
            return AddEventBus(services, (Action<EventFluxOptions>?)null, typeof(TMarker).Assembly);
        }

        /// <summary>
        /// Registers EventFlux event bus infrastructure with custom options, scanning the assembly that declares <typeparamref name="TMarker"/> for handlers.
        /// </summary>
        /// <remarks>
        /// Equivalent to <c>AddEventBus(configureOptions, typeof(TMarker).Assembly)</c>.
        /// </remarks>
        /// <typeparam name="TMarker">Any type declared in the assembly to scan, typically <c>Program</c>.</typeparam>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="configureOptions">Action to configure <see cref="EventFluxOptions"/>.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when duplicate handlers for the same request type are detected, or when a scanned handler is an open generic type definition.</exception>
        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
        public static IServiceCollection AddEventBus<TMarker>(this IServiceCollection services, Action<EventFluxOptions>? configureOptions)
        {
            return AddEventBus(services, configureOptions, typeof(TMarker).Assembly);
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
        /// <remarks>
        /// The time limit is <see cref="EventFluxOptions.Timeout"/> when it is set, otherwise 30 seconds.
        /// </remarks>
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

        /// <summary>
        /// Registers a closed pipeline behavior for every <see cref="IEventCustomPipeline{TRequest}"/> and
        /// <see cref="IEventCustomPipeline{TRequest, TResponse}"/> interface it implements.
        /// </summary>
        /// <remarks>
        /// Registering the same behavior more than once has no effect. Behaviors run in registration order.
        /// Use <see cref="AddEventOpenBehavior"/> for open generic behaviors such as <c>ValidationBehavior&lt;,&gt;</c>.
        /// </remarks>
        /// <typeparam name="TBehavior">The concrete, closed behavior type.</typeparam>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="lifetime">The lifetime of the behavior (default: <see cref="ServiceLifetime.Transient"/>).</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <typeparamref name="TBehavior"/> is abstract or implements no pipeline interface.</exception>
        public static IServiceCollection AddEventBehavior<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] TBehavior>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Transient)
            where TBehavior : class
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var behaviorType = typeof(TBehavior);
            EnsureConcrete(behaviorType, nameof(TBehavior));

            var pipelineInterfaces = PipelineInterfaces(behaviorType);
            if (pipelineInterfaces.Length == 0)
            {
                throw new ArgumentException(
                    $"Behavior type '{behaviorType.FullName}' does not implement IEventCustomPipeline<TRequest> or IEventCustomPipeline<TRequest, TResponse>.",
                    nameof(TBehavior));
            }

            foreach (var pipelineInterface in pipelineInterfaces)
            {
                services.TryAddEnumerable(new ServiceDescriptor(pipelineInterface, behaviorType, lifetime));
            }

            return services;
        }

        /// <summary>
        /// Registers an open generic pipeline behavior, such as <c>typeof(ValidationBehavior&lt;,&gt;)</c>, for every event type.
        /// </summary>
        /// <remarks>
        /// The behavior's type parameters must map one-to-one, in order, onto the pipeline interface it implements:
        /// <c>Behavior&lt;TRequest&gt; : IEventCustomPipeline&lt;TRequest&gt;</c> or
        /// <c>Behavior&lt;TRequest, TResponse&gt; : IEventCustomPipeline&lt;TRequest, TResponse&gt;</c>. Any other shape cannot be
        /// constructed by the container and is rejected here instead of failing when the container is built or an event is
        /// dispatched. Registering the same behavior more than once has no effect. Behaviors run in registration order.
        /// </remarks>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="openBehaviorType">The open generic behavior type definition.</param>
        /// <param name="lifetime">The lifetime of the behavior (default: <see cref="ServiceLifetime.Transient"/>).</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="openBehaviorType"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="openBehaviorType"/> is not an open generic type definition,
        /// is abstract, implements no pipeline interface, or its type parameters do not map onto the pipeline interface.</exception>
        public static IServiceCollection AddEventOpenBehavior(
            this IServiceCollection services,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] Type openBehaviorType,
            ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (openBehaviorType is null)
            {
                throw new ArgumentNullException(nameof(openBehaviorType));
            }

            if (!openBehaviorType.IsGenericTypeDefinition)
            {
                throw new ArgumentException(
                    $"Behavior type '{openBehaviorType.FullName ?? openBehaviorType.Name}' is not an open generic type definition. " +
                    "Pass a definition such as typeof(MyBehavior<,>), or register a closed behavior with AddEventBehavior<TBehavior>().",
                    nameof(openBehaviorType));
            }

            EnsureConcrete(openBehaviorType, nameof(openBehaviorType));

            var pipelineInterfaces = PipelineInterfaces(openBehaviorType);
            if (pipelineInterfaces.Length == 0)
            {
                throw new ArgumentException(
                    $"Behavior type '{openBehaviorType.FullName ?? openBehaviorType.Name}' does not implement IEventCustomPipeline<TRequest> or IEventCustomPipeline<TRequest, TResponse>.",
                    nameof(openBehaviorType));
            }

            var typeParameters = openBehaviorType.GetGenericArguments();
            var serviceTypes = new Type[pipelineInterfaces.Length];

            for (var i = 0; i < pipelineInterfaces.Length; i++)
            {
                var pipelineInterface = pipelineInterfaces[i];
                var interfaceArguments = pipelineInterface.GetGenericArguments();

                if (!interfaceArguments.SequenceEqual(typeParameters))
                {
                    throw new ArgumentException(
                        $"Open generic behavior '{openBehaviorType.FullName ?? openBehaviorType.Name}' declares {typeParameters.Length} type parameter(s) " +
                        $"that do not map in order onto the {interfaceArguments.Length} type argument(s) of IEventCustomPipeline, so the container cannot construct it. " +
                        "Declare it as Behavior<TRequest> : IEventCustomPipeline<TRequest> or Behavior<TRequest, TResponse> : IEventCustomPipeline<TRequest, TResponse>, " +
                        "or register a closed behavior with AddEventBehavior<TBehavior>().",
                        nameof(openBehaviorType));
                }

                serviceTypes[i] = pipelineInterface.GetGenericTypeDefinition();
            }

            foreach (var serviceType in serviceTypes)
            {
                services.TryAddEnumerable(new ServiceDescriptor(serviceType, openBehaviorType, lifetime));
            }

            return services;
        }

        private static void EnsureConcrete(Type behaviorType, string parameterName)
        {
            if (behaviorType.IsAbstract || behaviorType.IsInterface)
            {
                throw new ArgumentException(
                    $"Behavior type '{behaviorType.FullName ?? behaviorType.Name}' must be a concrete class.",
                    parameterName);
            }
        }

        private static Type[] PipelineInterfaces([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type behaviorType)
        {
            return behaviorType.GetInterfaces()
                .Where(i => i.IsGenericType
                    && (i.GetGenericTypeDefinition() == typeof(IEventCustomPipeline<>)
                        || i.GetGenericTypeDefinition() == typeof(IEventCustomPipeline<,>)))
                .ToArray();
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
