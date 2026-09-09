using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using EventFlux.Abstractions;
using EventFlux.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventFlux.Test
{
    public class DuplicateHandlerRegistrationTests
    {
        [Fact]
        public void AddEventBus_WithDuplicateRequestHandlers_ThrowsInvalidOperationException()
        {
            // Arrange
            var services = new ServiceCollection();
            var dynamicAssembly = CreateAssemblyWithRequestHandlers(duplicate: true);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                services.AddEventBus(dynamicAssembly);
            });

            Assert.Contains("Duplicate handler registration detected", ex.Message);
            Assert.Contains("DynamicTestRequest", ex.Message);
            Assert.Contains("DynamicHandler1", ex.Message);
            Assert.Contains("DynamicHandler2", ex.Message);
        }

        [Fact]
        public void AddEventBus_WithSingleRequestHandler_Succeeds()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            var dynamicAssembly = CreateAssemblyWithRequestHandlers(duplicate: false);

            // Act
            services.AddEventBus(dynamicAssembly);
            using var sp = services.BuildServiceProvider();

            // Assert
            Assert.NotNull(sp.GetService<IEventBus>());
        }

        [Fact]
        public void AddEventBus_WithSameAssemblyProvidedTwice_DoesNotThrow()
        {
            // Arrange
            var services = new ServiceCollection();
            var assembly = typeof(DuplicateHandlerRegistrationTests).Assembly;

            // Act & Assert
            var exception = Record.Exception(() =>
            {
                services.AddEventBus(assembly, assembly);
            });

            Assert.Null(exception);
        }

        [Fact]
        public void AddEventBus_WithMultipleNotificationHandlers_Succeeds()
        {
            // Arrange
            var services = new ServiceCollection();
            var dynamicAssembly = CreateAssemblyWithNotificationHandlers(handlerCount: 2);

            // Act & Assert
            var exception = Record.Exception(() =>
            {
                services.AddEventBus(dynamicAssembly);
            });

            Assert.Null(exception);
        }

        private static Assembly CreateAssemblyWithRequestHandlers(bool duplicate)
        {
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var assemblyName = new AssemblyName("DynamicReqAssembly_" + uniqueSuffix);
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

            var responseTypeBuilder = moduleBuilder.DefineType("DynamicTestResponse", TypeAttributes.Public | TypeAttributes.Class);
            responseTypeBuilder.AddInterfaceImplementation(typeof(IEventResponse));
            var responseType = responseTypeBuilder.CreateType()!;

            var requestTypeBuilder = moduleBuilder.DefineType("DynamicTestRequest", TypeAttributes.Public | TypeAttributes.Class);
            requestTypeBuilder.AddInterfaceImplementation(typeof(IEventRequest<>).MakeGenericType(responseType));
            var requestType = requestTypeBuilder.CreateType()!;

            var handlerInterface = typeof(IEventHandler<,>).MakeGenericType(requestType, responseType);

            var handler1Builder = moduleBuilder.DefineType("DynamicHandler1", TypeAttributes.Public | TypeAttributes.Class);
            handler1Builder.AddInterfaceImplementation(handlerInterface);
            var handleMethod1 = handler1Builder.DefineMethod(
                "Handle",
                MethodAttributes.Public | MethodAttributes.Virtual,
                typeof(Task<>).MakeGenericType(responseType),
                new[] { requestType });
            var il1 = handleMethod1.GetILGenerator();
            il1.Emit(OpCodes.Ldnull);
            il1.Emit(OpCodes.Ret);
            handler1Builder.CreateType();

            if (duplicate)
            {
                var handler2Builder = moduleBuilder.DefineType("DynamicHandler2", TypeAttributes.Public | TypeAttributes.Class);
                handler2Builder.AddInterfaceImplementation(handlerInterface);
                var handleMethod2 = handler2Builder.DefineMethod(
                    "Handle",
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(Task<>).MakeGenericType(responseType),
                    new[] { requestType });
                var il2 = handleMethod2.GetILGenerator();
                il2.Emit(OpCodes.Ldnull);
                il2.Emit(OpCodes.Ret);
                handler2Builder.CreateType();
            }

            return assemblyBuilder;
        }

        private static Assembly CreateAssemblyWithNotificationHandlers(int handlerCount)
        {
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var assemblyName = new AssemblyName("DynamicNotifAssembly_" + uniqueSuffix);
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

            var notificationTypeBuilder = moduleBuilder.DefineType("DynamicNotificationEvent", TypeAttributes.Public | TypeAttributes.Class);
            notificationTypeBuilder.AddInterfaceImplementation(typeof(IEventRequest));
            var notificationType = notificationTypeBuilder.CreateType()!;

            var handlerInterface = typeof(IEventHandler<>).MakeGenericType(notificationType);

            for (var i = 1; i <= handlerCount; i++)
            {
                var handlerBuilder = moduleBuilder.DefineType("DynamicNotificationHandler" + i, TypeAttributes.Public | TypeAttributes.Class);
                handlerBuilder.AddInterfaceImplementation(handlerInterface);
                var handleMethod = handlerBuilder.DefineMethod(
                    "Handle",
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(Task),
                    new[] { notificationType });
                var il = handleMethod.GetILGenerator();
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ret);
                handlerBuilder.CreateType();
            }

            return assemblyBuilder;
        }
    }
}
