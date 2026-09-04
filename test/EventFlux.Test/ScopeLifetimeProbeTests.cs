using EventFlux.Abstractions;
using EventFlux.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    public class TrackedScopedService : IDisposable
    {
        public static int Created;
        public static int Disposed;
        public static readonly List<Guid> SeenByHandler = new();

        public Guid Id { get; } = Guid.NewGuid();

        public TrackedScopedService() => Interlocked.Increment(ref Created);

        public void Dispose() => Interlocked.Increment(ref Disposed);

        public static void Reset()
        {
            Created = 0;
            Disposed = 0;
            lock (SeenByHandler) SeenByHandler.Clear();
        }
    }

    public class ScopeProbeEventRequest : IEventRequest
    {
    }

    public class ScopeProbeEventHandler : IEventHandler<ScopeProbeEventRequest>
    {
        private readonly TrackedScopedService _tracked;

        public ScopeProbeEventHandler(TrackedScopedService tracked) => _tracked = tracked;

        public Task Handle(ScopeProbeEventRequest request)
        {
            lock (TrackedScopedService.SeenByHandler)
                TrackedScopedService.SeenByHandler.Add(_tracked.Id);

            return Task.CompletedTask;
        }
    }

    [Collection("SharedHandlerCounters")]
    public class ScopeLifetimeProbeTests
    {
        private static ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped<TrackedScopedService>();
            services.AddEventDispatcher();
            services.AddEventBus(typeof(ScopeLifetimeProbeTests).Assembly);

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task PublishAsync_DisposesTheScopeItCreates()
        {
            using var provider = BuildProvider();
            TrackedScopedService.Reset();

            await provider.GetRequiredService<IEventBus>().PublishAsync(new ScopeProbeEventRequest());

            Assert.Equal(1, TrackedScopedService.Created);
            Assert.Equal(1, TrackedScopedService.Disposed);
        }

        [Fact]
        public async Task PublishAsync_RepeatedCalls_DoNotAccumulateUndisposedScopes()
        {
            using var provider = BuildProvider();
            TrackedScopedService.Reset();

            for (var i = 0; i < 50; i++)
                await provider.GetRequiredService<IEventBus>().PublishAsync(new ScopeProbeEventRequest());

            Assert.Equal(50, TrackedScopedService.Created);
            Assert.Equal(50, TrackedScopedService.Disposed);
        }

        [Fact]
        public async Task PublishAsync_HandlerDoesNotShareTheCallersScope()
        {
            using var provider = BuildProvider();
            TrackedScopedService.Reset();

            using var callerScope = provider.CreateScope();
            var callerInstance = callerScope.ServiceProvider.GetRequiredService<TrackedScopedService>();

            await callerScope.ServiceProvider.GetRequiredService<IEventBus>()
                .PublishAsync(new ScopeProbeEventRequest());

            var handlerInstance = TrackedScopedService.SeenByHandler.Single();

            Assert.NotEqual(callerInstance.Id, handlerInstance);
        }
    }
}
