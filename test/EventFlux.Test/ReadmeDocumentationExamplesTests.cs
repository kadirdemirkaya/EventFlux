using EventFlux.Abstractions;
using EventFlux.Attributes;
using EventFlux.Delegates;
using EventFlux.Extensions;
using EventFlux.Options;
using EventFlux.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventFlux.Test
{
    public record CreateUserCommand(string Username, string Email) : IEventRequest<CreateUserResponse>;
    public record CreateUserResponse(Guid UserId, bool Success) : IEventResponse;

    public class CreateUserHandler : IEventHandler<CreateUserCommand, CreateUserResponse>
    {
        public Task<CreateUserResponse> Handle(CreateUserCommand request)
        {
            var userId = Guid.NewGuid();
            return Task.FromResult(new CreateUserResponse(userId, true));
        }
    }

    public class ValidationBehavior<TRequest, TResponse> : IEventCustomPipeline<TRequest, TResponse>
        where TRequest : IEventRequest<TResponse>
        where TResponse : IEventResponse
    {
        public static bool WasCalled { get; set; }

        public async Task<TResponse> Handle(
            TRequest request,
            EventHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return await next(cancellationToken);
        }
    }

    public record UserCreatedNotification(Guid UserId) : IEventRequest;

    [HandlerOrder(1)]
    public class AuditLogHandler : IEventHandler<UserCreatedNotification>
    {
        public static List<string> ExecutionLog = new();

        public Task Handle(UserCreatedNotification notification)
        {
            ExecutionLog.Add("AuditLog");
            return Task.CompletedTask;
        }
    }

    [HandlerOrder(2)]
    public class SendWelcomeEmailHandler : IEventHandler<UserCreatedNotification>
    {
        public Task Handle(UserCreatedNotification notification)
        {
            AuditLogHandler.ExecutionLog.Add("SendWelcomeEmail");
            return Task.CompletedTask;
        }
    }

    public record OrderNotification(decimal TotalAmount) : IEventRequest;

    public class HighValueOrderHandler : IEventHandler<OrderNotification>
    {
        public static bool WasHandled = false;

        public bool CanHandle(OrderNotification @event) => @event.TotalAmount > 10000;

        public Task Handle(OrderNotification @event)
        {
            WasHandled = true;
            return Task.CompletedTask;
        }
    }

    public record OrderShippedEvent(int OrderId) : IEventRequest;

    public class OrderShippedHandler : IEventHandler<OrderShippedEvent>
    {
        public static int ShippedCount = 0;

        public Task Handle(OrderShippedEvent @event)
        {
            Interlocked.Increment(ref ShippedCount);
            return Task.CompletedTask;
        }
    }

    public class ReadmeDocumentationExamplesTests
    {
        [Fact]
        public async Task Readme_EndToEndExample_WorksAsExpected()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(ReadmeDocumentationExamplesTests).Assembly);
            services.AddEventDispatcher();

            using var sp = services.BuildServiceProvider();
            var eventBus = sp.GetRequiredService<IEventBus>();

            var command = new CreateUserCommand("johndoe", "john@example.com");
            var response = await eventBus.SendAsync(command);

            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.NotEqual(Guid.Empty, response.UserId);
        }

        [Fact]
        public async Task Readme_PipelineBehavior_ExecutesWithCancellationToken()
        {
            ValidationBehavior<CreateUserCommand, CreateUserResponse>.WasCalled = false;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(ReadmeDocumentationExamplesTests).Assembly);
            services.AddEventDispatcher();
            services.AddTransient(typeof(IEventCustomPipeline<,>), typeof(ValidationBehavior<,>));

            using var sp = services.BuildServiceProvider();
            var dispatcher = sp.GetRequiredService<IEventDispatcher>();

            var command = new CreateUserCommand("johndoe", "john@example.com");
            var response = await dispatcher.SendAsync(command, CancellationToken.None);

            Assert.True(ValidationBehavior<CreateUserCommand, CreateUserResponse>.WasCalled);
            Assert.NotNull(response);
        }

        [Fact]
        public async Task Readme_HandlerOrder_StartsInExpectedOrder()
        {
            AuditLogHandler.ExecutionLog.Clear();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(ReadmeDocumentationExamplesTests).Assembly);
            services.AddEventDispatcher();

            using var sp = services.BuildServiceProvider();
            var dispatcher = sp.GetRequiredService<IEventDispatcher>();

            await dispatcher.PublishAsync(new UserCreatedNotification(Guid.NewGuid()));

            Assert.Equal(2, AuditLogHandler.ExecutionLog.Count);
            Assert.Equal("AuditLog", AuditLogHandler.ExecutionLog[0]);
            Assert.Equal("SendWelcomeEmail", AuditLogHandler.ExecutionLog[1]);
        }

        [Fact]
        public async Task Readme_CanHandle_FiltersCorrectly()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(ReadmeDocumentationExamplesTests).Assembly);
            services.AddEventDispatcher();

            using var sp = services.BuildServiceProvider();
            var eventBus = sp.GetRequiredService<IEventBus>();

            HighValueOrderHandler.WasHandled = false;
            await eventBus.PublishAsync(new OrderNotification(500));
            Assert.False(HighValueOrderHandler.WasHandled);

            await eventBus.PublishAsync(new OrderNotification(15000));
            Assert.True(HighValueOrderHandler.WasHandled);
        }

        [Fact]
        public async Task Readme_BatchDispatch_ExecutesQueuedEvents()
        {
            OrderShippedHandler.ShippedCount = 0;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(ReadmeDocumentationExamplesTests).Assembly);

            using var sp = services.BuildServiceProvider();
            var eventBus = sp.GetRequiredService<IEventBus>();

            eventBus.AddStackRequestEvent(new OrderShippedEvent(101));
            eventBus.AddStackRequestEvent(new OrderShippedEvent(102));

            await eventBus.StackEventDispatcherAsync();

            Assert.Equal(2, OrderShippedHandler.ShippedCount);
        }

        [Fact]
        public async Task Readme_BatchDispatch_WithDirectEventStackService_ExecutesQueuedEvents()
        {
            OrderShippedHandler.ShippedCount = 0;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(ReadmeDocumentationExamplesTests).Assembly);

            using var sp = services.BuildServiceProvider();
            var stackService = sp.GetRequiredService<EventStackService>();
            stackService.AddEventRequest(new OrderShippedEvent(201));
            stackService.AddEventRequest(new OrderShippedEvent(202));

            using var scope = sp.CreateScope();
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await eventBus.StackEventDispatcherAsync();

            Assert.Equal(2, OrderShippedHandler.ShippedCount);
        }

        [Fact]
        public void Readme_ScopeConfiguration_AllowsAmbientScope()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(options =>
            {
                options.CreateScopePerEvent = false;
            }, typeof(ReadmeDocumentationExamplesTests).Assembly);
            services.AddEventDispatcher(options =>
            {
                options.CreateScopePerEvent = false;
            });

            using var sp = services.BuildServiceProvider();
            var options = sp.GetRequiredService<EventFluxOptions>();
            Assert.False(options.CreateScopePerEvent);
        }

        [Fact]
        public void Readme_PublishStrategyConfiguration_AllowsSequential()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(options =>
            {
                options.PublishStrategy = PublishStrategy.Sequential;
            }, typeof(ReadmeDocumentationExamplesTests).Assembly);
            services.AddEventDispatcher(options =>
            {
                options.PublishStrategy = PublishStrategy.Sequential;
            });

            using var sp = services.BuildServiceProvider();
            var options2 = sp.GetRequiredService<EventFluxOptions>();
            Assert.Equal(PublishStrategy.Sequential, options2.PublishStrategy);
        }

        [Fact]
        public void Readme_HandlerLifetimeConfiguration_AllowsScoped()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped;
            }, typeof(ReadmeDocumentationExamplesTests).Assembly);

            using var sp = services.BuildServiceProvider();
            var options = sp.GetRequiredService<EventFluxOptions>();
            Assert.Equal(ServiceLifetime.Scoped, options.HandlerLifetime);
        }

        public record UpdateUserEmailCommand(string Email) : IEventRequest<UpdateUserEmailResponse>;
        public record UpdateUserEmailResponse(bool Success) : IEventResponse;

        public class UpdateUserEmailHandler : IEventHandler<UpdateUserEmailCommand, UpdateUserEmailResponse>
        {
            public Task<UpdateUserEmailResponse> Handle(UpdateUserEmailCommand request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new UpdateUserEmailResponse(true));
            }
        }

        [Fact]
        public async Task Readme_CancellationTokenHandler_Works()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(typeof(ReadmeDocumentationExamplesTests).Assembly);

            using var sp = services.BuildServiceProvider();
            var eventBus = sp.GetRequiredService<IEventBus>();

            var response = await eventBus.SendAsync(new UpdateUserEmailCommand("test@example.com"), CancellationToken.None);
            Assert.NotNull(response);
            Assert.True(response.Success);
        }
    }
}
