using EventFlux.Abstractions;
using EventFlux.Options;
using EventFlux.Services;
using EventFlux.Test.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace EventFlux.Test.Unit
{
    public class EventBusObsoleteConstructorTests
    {
        public static IEnumerable<object[]> ObsoleteConstructorParameterCounts()
        {
            yield return new object[] { 5 };
            yield return new object[] { 6 };
            yield return new object[] { 7 };
            yield return new object[] { 8 };
        }

        [Theory]
        [MemberData(nameof(ObsoleteConstructorParameterCounts))]
        public void IgnoredParameterConstructors_AreMarkedObsolete(int parameterCount)
        {
            // Arrange
            var constructors = typeof(EventBus)
                .GetConstructors()
                .Where(c => c.GetParameters().Length == parameterCount)
                .ToList();

            // Act & Assert
            Assert.NotEmpty(constructors);
            Assert.All(constructors, ctor =>
                Assert.NotNull(ctor.GetCustomAttribute<ObsoleteAttribute>()));
        }

        [Fact]
        public void ThreeParameterAssembliesConstructor_IsMarkedObsolete()
        {
            // Arrange
            var ctor = typeof(EventBus).GetConstructor(new[]
            {
                typeof(IServiceProvider),
                typeof(IEnumerable<Assembly>),
                typeof(ILogger<EventBus>)
            });

            // Act & Assert
            Assert.NotNull(ctor);
            Assert.NotNull(ctor!.GetCustomAttribute<ObsoleteAttribute>());
        }

        [Fact]
        public void CanonicalConstructors_AreNotMarkedObsolete()
        {
            // Arrange
            var canonicalParameterCounts = new[] { 0, 2, 3, 4 };
            var logger = Mock.Of<ILogger<EventBus>>();

            var parameterlessCtor = typeof(EventBus).GetConstructor(Type.EmptyTypes);
            var twoParamCtor = typeof(EventBus).GetConstructor(new[] { typeof(IServiceProvider), typeof(ILogger<EventBus>) });
            var fourParamCtor = typeof(EventBus).GetConstructor(new[] { typeof(IServiceProvider), typeof(ILogger<EventBus>), typeof(EventFluxOptions), typeof(EventStackService) });

            // Act & Assert
            Assert.Null(parameterlessCtor!.GetCustomAttribute<ObsoleteAttribute>());
            Assert.Null(twoParamCtor!.GetCustomAttribute<ObsoleteAttribute>());
            Assert.Null(fourParamCtor!.GetCustomAttribute<ObsoleteAttribute>());
        }

#pragma warning disable CS0618
        [Fact]
        public async Task ObsoleteConstructor_WithAssemblies_StillConstructsWorkingBus()
        {
            // Arrange
            var serviceProvider = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<EventBus>>();

            var eventBus = new EventBus(serviceProvider, new[] { typeof(EventBusObsoleteConstructorTests).Assembly }, logger);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                eventBus.SendAsync<UnhandledEventResponse>(new UnhandledEventRequest()));
        }
#pragma warning restore CS0618
    }
}
