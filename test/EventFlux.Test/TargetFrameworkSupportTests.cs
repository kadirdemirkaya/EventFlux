using EventFlux.Abstractions;
using System.Runtime.Versioning;
using System.Reflection;

namespace EventFlux.Test
{
    public class TargetFrameworkSupportTests
    {
        private static FrameworkName GetFrameworkName(Assembly assembly)
        {
            var attribute = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
            Assert.NotNull(attribute);
            return new FrameworkName(attribute!.FrameworkName);
        }

        [Fact]
        public void EventFluxAssembly_TargetsSupportedFrameworkVersion()
        {
            // Arrange
            var eventFluxAssembly = typeof(IEventBus).Assembly;

            // Act
            var frameworkName = GetFrameworkName(eventFluxAssembly);

            // Assert
            Assert.Equal(".NETCoreApp", frameworkName.Identifier);
            Assert.True(frameworkName.Version >= new Version(8, 0),
                $"EventFlux must not ship out-of-support target frameworks, but was built for {frameworkName}.");
        }

        [Fact]
        public void EventFluxAssembly_MatchesTestRuntimeTargetFramework()
        {
            // Arrange
            var eventFluxAssembly = typeof(IEventBus).Assembly;
            var testAssembly = typeof(TargetFrameworkSupportTests).Assembly;

            // Act
            var eventFluxFramework = GetFrameworkName(eventFluxAssembly);
            var testFramework = GetFrameworkName(testAssembly);

            // Assert
            Assert.Equal(testFramework.Identifier, eventFluxFramework.Identifier);
            Assert.Equal(testFramework.Version, eventFluxFramework.Version);
        }

        [Fact]
        public void EventFluxAssembly_RunsOnCurrentRuntime()
        {
            // Arrange
            var eventFluxAssembly = typeof(IEventBus).Assembly;

            // Act
            var frameworkName = GetFrameworkName(eventFluxAssembly);

            // Assert
            Assert.True(Environment.Version.Major >= frameworkName.Version.Major,
                $"Runtime {Environment.Version} cannot host an assembly built for {frameworkName}.");
        }
    }
}
