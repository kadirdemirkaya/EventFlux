using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using EventFlux.Abstractions;
using EventFlux.Extensions;

namespace EventFlux.Test
{
    public class AotAnnotationTests
    {
        [Fact]
        public void EventFluxAssembly_IsMarkedTrimmable()
        {
            // Arrange
            var metadata = typeof(IEventBus).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>();

            // Assert
            Assert.Contains(metadata, m => m.Key == "IsTrimmable" && string.Equals(m.Value, "True", StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData(nameof(EventBusServiceExtension.AddEventBus))]
        [InlineData(nameof(EventBusServiceExtension.AddEventDispatcher))]
        public void RegistrationEntryPoints_WarnAotAndTrimmingConsumers(string methodName)
        {
            // Arrange
            var overloads = typeof(EventBusServiceExtension).GetMethods()
                .Where(m => m.Name == methodName)
                .ToList();

            // Assert
            Assert.NotEmpty(overloads);
            Assert.All(overloads, m =>
            {
                Assert.True(m.IsDefined(typeof(RequiresDynamicCodeAttribute), false), $"{m} lacks [RequiresDynamicCode]");
                Assert.True(m.IsDefined(typeof(RequiresUnreferencedCodeAttribute), false), $"{m} lacks [RequiresUnreferencedCode]");
            });
        }

        [Theory]
        [InlineData(typeof(EventBus))]
        [InlineData(typeof(EventDispatcher))]
        public void DispatcherImplementations_WarnAotAndTrimmingConsumers(Type implementation)
        {
            // Assert
            Assert.True(implementation.IsDefined(typeof(RequiresDynamicCodeAttribute), false), $"{implementation} lacks [RequiresDynamicCode]");
            Assert.True(implementation.IsDefined(typeof(RequiresUnreferencedCodeAttribute), false), $"{implementation} lacks [RequiresUnreferencedCode]");
        }
    }
}
