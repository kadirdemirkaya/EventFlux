using EventFlux.Abstractions;
using EventFlux.Attributes;
using EventFlux.Extensions;
using EventFlux.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace EventFlux.Test
{
    public class HandlerOrderStabilityTests
    {
        public class StabilityTracker
        {
            private readonly object _gate = new();
            private readonly List<string> _entries = new();

            public void Record(string name)
            {
                lock (_gate)
                {
                    _entries.Add(name);
                }
            }

            public IReadOnlyList<string> Entries
            {
                get
                {
                    lock (_gate)
                    {
                        return _entries.ToArray();
                    }
                }
            }
        }

        public class StabilityEvent : IEventRequest
        {
        }

        public abstract class StabilityHandlerBase
        {
            private readonly StabilityTracker _tracker;

            protected StabilityHandlerBase(StabilityTracker tracker) => _tracker = tracker;

            protected Task RecordAsync()
            {
                _tracker.Record(GetType().Name);
                return Task.CompletedTask;
            }
        }

        [HandlerOrder(0)]
        public class StabilityHandler00 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler00(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(1)]
        public class StabilityHandler01 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler01(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(0)]
        public class StabilityHandler02 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler02(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(1)]
        public class StabilityHandler03 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler03(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(0)]
        public class StabilityHandler04 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler04(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(1)]
        public class StabilityHandler05 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler05(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(0)]
        public class StabilityHandler06 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler06(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(1)]
        public class StabilityHandler07 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler07(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(0)]
        public class StabilityHandler08 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler08(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(1)]
        public class StabilityHandler09 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler09(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(0)]
        public class StabilityHandler10 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler10(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(1)]
        public class StabilityHandler11 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler11(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(0)]
        public class StabilityHandler12 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler12(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(1)]
        public class StabilityHandler13 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler13(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(0)]
        public class StabilityHandler14 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler14(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(1)]
        public class StabilityHandler15 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler15(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(0)]
        public class StabilityHandler16 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler16(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(1)]
        public class StabilityHandler17 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler17(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(0)]
        public class StabilityHandler18 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler18(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        [HandlerOrder(1)]
        public class StabilityHandler19 : StabilityHandlerBase, IEventHandler<StabilityEvent>
        {
            public StabilityHandler19(StabilityTracker tracker) : base(tracker) { }
            public Task Handle(StabilityEvent @event, CancellationToken cancellationToken = default) => RecordAsync();
        }

        private static ServiceProvider CreateProvider(StabilityTracker tracker)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(tracker);
            services.AddEventBus(
                options => options.PublishStrategy = PublishStrategy.Sequential,
                typeof(HandlerOrderStabilityTests).Assembly);
            services.AddEventDispatcher(options => options.PublishStrategy = PublishStrategy.Sequential);

            return services.BuildServiceProvider();
        }

        private static IReadOnlyList<string> ExpectedOrder(IServiceProvider provider)
        {
            return provider
                .GetServices(typeof(IEventHandler<StabilityEvent>))
                .Select(handler => handler!.GetType())
                .Select(type => new
                {
                    type.Name,
                    Priority = type.GetCustomAttribute<HandlerOrderAttribute>()?.Priority ?? 0
                })
                .OrderBy(entry => entry.Priority)
                .Select(entry => entry.Name)
                .ToArray();
        }

        [Fact]
        public async Task EventBus_PublishAsync_TiedPriorities_PreserveResolutionOrder()
        {
            // Arrange
            var tracker = new StabilityTracker();
            using var provider = CreateProvider(tracker);
            var expected = ExpectedOrder(provider);

            // Act
            await provider.GetRequiredService<IEventBus>().PublishAsync(new StabilityEvent());

            // Assert
            Assert.Equal(expected, tracker.Entries);
        }

        [Fact]
        public async Task EventDispatcher_PublishAsync_TiedPriorities_PreserveResolutionOrder()
        {
            // Arrange
            var tracker = new StabilityTracker();
            using var provider = CreateProvider(tracker);
            var expected = ExpectedOrder(provider);

            // Act
            await provider.GetRequiredService<IEventDispatcher>().PublishAsync(new StabilityEvent());

            // Assert
            Assert.Equal(expected, tracker.Entries);
        }

        [Fact]
        public async Task EventBus_PublishAsync_InvokesEveryRegisteredHandlerExactlyOnce()
        {
            // Arrange
            var tracker = new StabilityTracker();
            using var provider = CreateProvider(tracker);
            var expected = ExpectedOrder(provider);

            // Act
            await provider.GetRequiredService<IEventBus>().PublishAsync(new StabilityEvent());

            // Assert
            Assert.Equal(20, tracker.Entries.Count);
            Assert.Equal(expected.OrderBy(name => name), tracker.Entries.OrderBy(name => name));
        }
    }
}
