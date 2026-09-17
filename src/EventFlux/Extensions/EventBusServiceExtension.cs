using EventFlux.Internal;
using EventFlux.Options;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace EventFlux.Extensions
{
    /// <summary>
    /// Registration methods kept in the <c>EventFlux.Extensions</c> namespace for compatibility.
    /// </summary>
    /// <remarks>
    /// The registration API lives in <see cref="EventFluxServiceCollectionExtensions"/> in the
    /// <c>Microsoft.Extensions.DependencyInjection</c> namespace. Every member here forwards to it, so existing
    /// <c>using EventFlux.Extensions;</c> directives and compiled callers keep working, and a file that imports both
    /// namespaces binds without ambiguity.
    /// </remarks>
    public static class EventBusServiceExtension
    {
        /// <inheritdoc cref="EventFluxServiceCollectionExtensions.AddEventBus(IServiceCollection, Assembly[])"/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
        public static IServiceCollection AddEventBus(IServiceCollection services, params Assembly[] assemblies)
        {
            return EventFluxServiceCollectionExtensions.AddEventBus(services, (Action<EventFluxOptions>?)null, assemblies);
        }

        /// <inheritdoc cref="EventFluxServiceCollectionExtensions.AddEventBus(IServiceCollection, Assembly[])"/>
        /// <typeparam name="TServices">The service collection type.</typeparam>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
        public static IServiceCollection AddEventBus<TServices>(this TServices services, params Assembly[] assemblies)
            where TServices : IServiceCollection
        {
            return EventFluxServiceCollectionExtensions.AddEventBus(services, (Action<EventFluxOptions>?)null, assemblies);
        }

        /// <inheritdoc cref="EventFluxServiceCollectionExtensions.AddEventBus(IServiceCollection, Action{EventFluxOptions}, Assembly[])"/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
        public static IServiceCollection AddEventBus(IServiceCollection services, Action<EventFluxOptions>? configureOptions, params Assembly[] assemblies)
        {
            return EventFluxServiceCollectionExtensions.AddEventBus(services, configureOptions, assemblies);
        }

        /// <inheritdoc cref="EventFluxServiceCollectionExtensions.AddEventBus(IServiceCollection, Action{EventFluxOptions}, Assembly[])"/>
        /// <typeparam name="TServices">The service collection type.</typeparam>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
        public static IServiceCollection AddEventBus<TServices>(this TServices services, Action<EventFluxOptions>? configureOptions, params Assembly[] assemblies)
            where TServices : IServiceCollection
        {
            return EventFluxServiceCollectionExtensions.AddEventBus(services, configureOptions, assemblies);
        }

        /// <inheritdoc cref="EventFluxServiceCollectionExtensions.AddEventDispatcher(IServiceCollection)"/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
        public static IServiceCollection AddEventDispatcher(IServiceCollection services)
        {
            return EventFluxServiceCollectionExtensions.AddEventDispatcher(services, (Action<EventFluxOptions>?)null);
        }

        /// <inheritdoc cref="EventFluxServiceCollectionExtensions.AddEventDispatcher(IServiceCollection)"/>
        /// <typeparam name="TServices">The service collection type.</typeparam>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
        public static IServiceCollection AddEventDispatcher<TServices>(this TServices services)
            where TServices : IServiceCollection
        {
            return EventFluxServiceCollectionExtensions.AddEventDispatcher(services, (Action<EventFluxOptions>?)null);
        }

        /// <inheritdoc cref="EventFluxServiceCollectionExtensions.AddEventDispatcher(IServiceCollection, Action{EventFluxOptions})"/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
        public static IServiceCollection AddEventDispatcher(IServiceCollection services, Action<EventFluxOptions>? configureOptions)
        {
            return EventFluxServiceCollectionExtensions.AddEventDispatcher(services, configureOptions);
        }

        /// <inheritdoc cref="EventFluxServiceCollectionExtensions.AddEventDispatcher(IServiceCollection, Action{EventFluxOptions})"/>
        /// <typeparam name="TServices">The service collection type.</typeparam>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [RequiresDynamicCode(AotMessages.DynamicCode)]
        [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
        public static IServiceCollection AddEventDispatcher<TServices>(this TServices services, Action<EventFluxOptions>? configureOptions)
            where TServices : IServiceCollection
        {
            return EventFluxServiceCollectionExtensions.AddEventDispatcher(services, configureOptions);
        }

        /// <inheritdoc cref="EventFluxServiceCollectionExtensions.AddEventLogging(IServiceCollection)"/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IServiceCollection AddEventLogging(IServiceCollection services)
        {
            return EventFluxServiceCollectionExtensions.AddEventLogging(services);
        }

        /// <inheritdoc cref="EventFluxServiceCollectionExtensions.AddEventLogging(IServiceCollection)"/>
        /// <typeparam name="TServices">The service collection type.</typeparam>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IServiceCollection AddEventLogging<TServices>(this TServices services)
            where TServices : IServiceCollection
        {
            return EventFluxServiceCollectionExtensions.AddEventLogging(services);
        }

        /// <inheritdoc cref="EventFluxServiceCollectionExtensions.AddEventTimeout(IServiceCollection)"/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IServiceCollection AddEventTimeout(IServiceCollection services)
        {
            return EventFluxServiceCollectionExtensions.AddEventTimeout(services);
        }

        /// <inheritdoc cref="EventFluxServiceCollectionExtensions.AddEventTimeout(IServiceCollection)"/>
        /// <typeparam name="TServices">The service collection type.</typeparam>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IServiceCollection AddEventTimeout<TServices>(this TServices services)
            where TServices : IServiceCollection
        {
            return EventFluxServiceCollectionExtensions.AddEventTimeout(services);
        }
    }
}
