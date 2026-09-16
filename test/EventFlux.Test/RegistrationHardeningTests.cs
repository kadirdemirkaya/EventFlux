using System.Reflection;
using System.Reflection.Emit;
using EventFlux.Abstractions;
using EventFlux.Extensions;
using EventFlux.Options;
using EventFlux.Services;
using EventFlux.Test.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    public class RegistrationHardeningTests
    {
        private sealed class StubAssembly : Assembly
        {
            private readonly Type?[] _types;
            private readonly bool _failToLoadSomeTypes;
            private readonly string _name = "StubAssembly_" + Guid.NewGuid().ToString("N");

            public StubAssembly(bool failToLoadSomeTypes, params Type?[] types)
            {
                _types = types;
                _failToLoadSomeTypes = failToLoadSomeTypes;
            }

            public int GetTypesCallCount { get; private set; }

            public override string FullName => _name;

            public override Type[] GetTypes()
            {
                GetTypesCallCount++;

                if (_failToLoadSomeTypes)
                {
                    throw new ReflectionTypeLoadException(
                        _types,
                        new Exception?[] { new TypeLoadException("Simulated unresolvable dependency") });
                }

                return _types.Where(t => t is not null).Select(t => t!).ToArray();
            }
        }

        [Fact]
        public void AddEventBus_CalledTwiceWithSameAssembly_RegistersRequestHandlerOnce()
        {
            // Arrange
            var services = new ServiceCollection();
            var assembly = typeof(RegistrationHardeningTests).Assembly;

            // Act
            services.AddEventBus(assembly);
            services.AddEventBus(assembly);

            // Assert
            Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IEventHandler<ExampleEventRequest, ExampleEventResponse>)));
        }

        [Fact]
        public void AddEventBus_CalledTwice_RegistersInfrastructureOnce()
        {
            // Arrange
            var services = new ServiceCollection();
            var assembly = typeof(RegistrationHardeningTests).Assembly;

            // Act
            services.AddEventBus(assembly);
            services.AddEventBus(assembly);

            // Assert
            Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IEventBus)));
            Assert.Equal(1, services.Count(d => d.ServiceType == typeof(EventService)));
            Assert.Equal(1, services.Count(d => d.ServiceType == typeof(EventMapService)));
            Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IEventHandler<PublishEventRequest>)));
        }

        [Fact]
        public async Task AddEventBus_CalledTwiceWithSingletonLifetime_ResolvesSingleHandlerInstance()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            var assembly = typeof(RegistrationHardeningTests).Assembly;

            // Act
            services.AddEventBus(o => o.HandlerLifetime = ServiceLifetime.Singleton, assembly);
            services.AddEventBus(o => o.HandlerLifetime = ServiceLifetime.Singleton, assembly);
            using var provider = services.BuildServiceProvider();

            // Assert
            var handlers = provider.GetServices<IEventHandler<SendEventRequest, SendEventResponse>>().ToList();
            Assert.Single(handlers);

            var response = await provider.GetRequiredService<IEventBus>()
                .SendAsync(new SendEventRequest { Data = "once" });
            Assert.Equal("once", response!.Data);
        }

        [Fact]
        public async Task AddEventBus_CalledPerModuleWithDifferentAssemblies_KeepsHandlersAndMapsOfEveryModule()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            var moduleA = new StubAssembly(false, typeof(ExampleEventHandler));
            var moduleB = new StubAssembly(false, typeof(SendEventHandler), typeof(PublishEventHandler));

            // Act
            services.AddEventBus(moduleA);
            services.AddEventBus(moduleB);
            using var provider = services.BuildServiceProvider();

            // Assert
            var eventService = provider.GetRequiredService<EventService>();
            var mapService = provider.GetRequiredService<EventMapService>();

            Assert.Contains(typeof(ExampleEventHandler), eventService.GetHandlersForEvent<ExampleEventRequest>());
            Assert.Contains(typeof(SendEventHandler), eventService.GetHandlersForEvent<SendEventRequest>());
            Assert.Contains(typeof(PublishEventHandler), eventService.GetHandlersForEvent<PublishEventRequest>());
            Assert.True(mapService.IsMap<ExampleEventRequest>());
            Assert.True(mapService.IsMap<SendEventRequest>());

            var bus = provider.GetRequiredService<IEventBus>();
            var example = await bus.SendAsync(new ExampleEventRequest { Num = 21 });
            var send = await bus.SendAsync(new SendEventRequest { Data = "b" });
            Assert.Equal(42, example!.Result);
            Assert.Equal("b", send!.Data);
        }

        [Fact]
        public void AddEventBus_WithAssemblyThatFailsToLoadSomeTypes_RegistersTheLoadableHandlers()
        {
            // Arrange
            var services = new ServiceCollection();
            var assembly = new StubAssembly(true, typeof(ExampleEventHandler), null, typeof(PublishEventHandler));

            // Act
            var exception = Record.Exception(() => services.AddEventBus(assembly));

            // Assert
            Assert.Null(exception);
            Assert.Contains(services, d => d.ServiceType == typeof(IEventHandler<ExampleEventRequest, ExampleEventResponse>)
                && d.ImplementationType == typeof(ExampleEventHandler));
            Assert.Contains(services, d => d.ServiceType == typeof(IEventHandler<PublishEventRequest>)
                && d.ImplementationType == typeof(PublishEventHandler));
        }

        [Fact]
        public void AddEventBus_ScansEachAssemblyOnlyOnce()
        {
            // Arrange
            var services = new ServiceCollection();
            var assembly = new StubAssembly(false, typeof(ExampleEventHandler), typeof(PublishEventHandler));

            // Act
            services.AddEventBus(assembly);

            // Assert
            Assert.Equal(1, assembly.GetTypesCallCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AddEventBus_WithOpenGenericHandler_ThrowsNamingTheHandler(bool requestHandler)
        {
            // Arrange
            var services = new ServiceCollection();
            var assembly = CreateAssemblyWithOpenGenericHandler(requestHandler, out var handlerName);

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => services.AddEventBus(assembly));

            // Assert
            Assert.Contains(handlerName, exception.Message);
            Assert.Contains("open generic", exception.Message);
        }

        [Fact]
        public void AddEventBus_WithOpenGenericHandler_RegistersNothing()
        {
            // Arrange
            var services = new ServiceCollection();
            var assembly = CreateAssemblyWithOpenGenericHandler(false, out _);

            // Act
            Assert.Throws<InvalidOperationException>(() => services.AddEventBus(assembly));

            // Assert
            Assert.DoesNotContain(services, d => d.ServiceType == typeof(IEventBus));
        }

        private static Assembly CreateAssemblyWithOpenGenericHandler(bool requestHandler, out string handlerName)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("OpenGenericAssembly_" + suffix), AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

            Type handlerInterface;
            Type returnType;
            Type eventType;

            if (requestHandler)
            {
                var responseBuilder = moduleBuilder.DefineType("OpenGenericResponse", TypeAttributes.Public | TypeAttributes.Class);
                responseBuilder.AddInterfaceImplementation(typeof(IEventResponse));
                var responseType = responseBuilder.CreateType()!;

                var requestBuilder = moduleBuilder.DefineType("OpenGenericRequest", TypeAttributes.Public | TypeAttributes.Class);
                requestBuilder.AddInterfaceImplementation(typeof(IEventRequest<>).MakeGenericType(responseType));
                eventType = requestBuilder.CreateType()!;

                handlerInterface = typeof(IEventHandler<,>).MakeGenericType(eventType, responseType);
                returnType = typeof(Task<>).MakeGenericType(responseType);
            }
            else
            {
                var notificationBuilder = moduleBuilder.DefineType("OpenGenericNotification", TypeAttributes.Public | TypeAttributes.Class);
                notificationBuilder.AddInterfaceImplementation(typeof(IEventRequest));
                eventType = notificationBuilder.CreateType()!;

                handlerInterface = typeof(IEventHandler<>).MakeGenericType(eventType);
                returnType = typeof(Task);
            }

            handlerName = "OpenGenericHandler" + suffix;
            var handlerBuilder = moduleBuilder.DefineType(handlerName, TypeAttributes.Public | TypeAttributes.Class);
            handlerBuilder.DefineGenericParameters("T");
            handlerBuilder.AddInterfaceImplementation(handlerInterface);
            var handleMethod = handlerBuilder.DefineMethod(
                "Handle",
                MethodAttributes.Public | MethodAttributes.Virtual,
                returnType,
                new[] { eventType, typeof(CancellationToken) });
            var il = handleMethod.GetILGenerator();
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ret);
            handlerBuilder.CreateType();

            return assemblyBuilder;
        }
    }
}
