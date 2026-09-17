using EventFlux.Abstractions;
using EventFlux.Test.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    public class DependencyInjectionNamespaceOnlyTests
    {
        [Fact]
        public async Task OnlyDependencyInjectionNamespaceImported_FullRegistration_DispatchesThroughPipeline()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            // Act
            services
                .AddEventBus<DependencyInjectionNamespaceOnlyTests>(options => options.Timeout = TimeSpan.FromSeconds(10))
                .AddEventDispatcher()
                .AddEventLogging()
                .AddEventTimeout()
                .AddEventOpenBehavior(typeof(TracingRequestBehavior<,>))
                .AddEventBehavior<RecordUnitCommandBehavior>();
            using var provider = services.BuildServiceProvider();
            var dispatcher = provider.GetRequiredService<IEventDispatcher>();
            var command = new RecordUnitCommand();
            await dispatcher.SendAsync(command);

            // Assert
            Assert.Equal(new[] { "open-behavior", "closed-behavior", "handler" }, command.Trace);
        }
    }
}
