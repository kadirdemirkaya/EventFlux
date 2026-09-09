# EventFlux

[![NuGet](https://img.shields.io/nuget/v/EventFlux.svg)](https://www.nuget.org/packages/EventFlux)
[![Downloads](https://img.shields.io/nuget/dt/EventFlux.svg)](https://www.nuget.org/packages/EventFlux)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/kadirdemirkaya/EventFlux/blob/main/LICENSE.txt)

EventFlux is a lightweight, high-performance in-memory event dispatching and CQRS library for .NET. It supports point-to-point request/response messaging, publish/subscribe event broadcasting, pipeline behaviors (middleware), handler ordering, and deferred batch dispatching.

---

## Key Features

- **Blazing Fast**: Compiled expression tree delegate caching with minimal dispatch overhead (~81 ns).
- **Request / Response**: Send a command or query to a single handler and receive a response via `SendAsync`.
- **Publish / Subscribe**: Broadcast notification events to multiple handlers via `PublishAsync`.
- **Pipeline Behaviors**: Intercept requests with cross-cutting concerns (validation, logging, caching) using `IEventCustomPipeline`.
- **Handler Ordering**: Control execution sequence for multi-handler events with `[HandlerOrder(priority)]`.
- **Precondition Evaluation**: Selectively gate handler execution with the `CanHandle` method.
- **Deferred Batch Dispatch**: Queue events and dispatch them together via `AddStackRequestEvent` and `StackEventDispatcherAsync`.
- **Multi-Targeting**: Supports .NET 6.0, 7.0, 8.0, and 9.0 with SourceLink and symbol debugging (`.snupkg`) enabled.

---

## Quick Start

### 1. Installation

Package Manager Console:
```powershell
dotnet add package EventFlux --version 1.4.1
```

Or via `<PackageReference>` in your `.csproj`:
```xml
<PackageReference Include="EventFlux" Version="1.4.1" />
```

### 2. Register Services

In your `Program.cs` / `Startup.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register EventFlux and scan assembly for handlers
builder.Services.AddEventBus(typeof(Program).Assembly);

// Optional: register EventDispatcher when using pipeline behaviors or HandlerOrder
builder.Services.AddEventDispatcher();

// Optional built-in behaviors
builder.Services.AddEventLogging();
builder.Services.AddEventTimeout();

// Register any custom pipeline behaviors
builder.Services.AddTransient(typeof(IEventCustomPipeline<,>), typeof(ValidationBehavior<,>));

var app = builder.Build();
app.Run();
```

---

## End-to-End Example

### 1. Define Request, Response and Handler

```csharp
using EventFlux.Abstractions;

// Define request and response (response must implement IEventResponse)
public record CreateUserCommand(string Username, string Email) : IEventRequest<CreateUserResponse>;
public record CreateUserResponse(Guid UserId, bool Success) : IEventResponse;

// Define handler (CancellationToken is optional via default interface method)
public class CreateUserHandler : IEventHandler<CreateUserCommand, CreateUserResponse>
{
    public Task<CreateUserResponse> Handle(CreateUserCommand request, CancellationToken cancellationToken = default)
    {
        var userId = Guid.NewGuid();
        // Execute business logic...
        return Task.FromResult(new CreateUserResponse(userId, true));
    }
}
```

### 2. Dispatching in a Controller or Service

```csharp
[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly IEventBus _eventBus;

    public UserController(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserCommand command, CancellationToken ct)
    {
        CreateUserResponse response = await _eventBus.SendAsync(command, ct);
        return Ok(response);
    }
}
```

---

## EventBus vs. EventDispatcher

EventFlux provides two dispatch interfaces to fit your performance and architectural requirements:

| Capability | `IEventBus` | `IEventDispatcher` |
|---|---|---|
| **Primary Focus** | Direct, high-throughput, low-latency dispatch | Extensible pipeline dispatch |
| **Pipeline Behaviors (`IEventCustomPipeline`)** | ❌ Bypassed (direct invocation) | ✅ Supported (wraps handlers in pipeline) |
| **`[HandlerOrder]` Support** | ✅ Supported (order-based invocation) | ✅ Supported (order-based invocation) |
| **Dispatch Overhead** | Minimal (~81 ns) | Low (includes pipeline middleware) |
| **Publish / Subscribe (`PublishAsync`)** | ✅ Supported | ✅ Supported |
| **Batch / Stack Dispatch (`AddStackRequestEvent`)** | ✅ Supported | ❌ |

> **When to use which?**
> - Use **`IEventBus`** when you want fast, direct execution without pipeline middleware overhead.
> - Use **`IEventDispatcher`** when you need cross-cutting behaviors (validation, logging, caching, metrics).

---

## Features in Detail

### 1. Custom Pipeline Behaviors

Pipeline behaviors wrap request execution, similar to ASP.NET Core middleware:

```csharp
public class ValidationBehavior<TRequest, TResponse> : IEventCustomPipeline<TRequest, TResponse>
    where TRequest : IEventRequest<TResponse>
    where TResponse : IEventResponse
{
    public async Task<TResponse> Handle(
        TRequest request,
        EventHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Validating {typeof(TRequest).Name}...");
        
        // Always forward cancellationToken to next()
        return await next(cancellationToken);
    }
}
```

Register your custom pipeline in `Program.cs`:

```csharp
builder.Services.AddTransient(typeof(IEventCustomPipeline<,>), typeof(ValidationBehavior<,>));
```

### 2. Multi-Handler Ordering with `[HandlerOrder]`
 
When broadcasting notifications via `PublishAsync`, control the invocation sequence of multiple handlers using `[HandlerOrder(priority)]`:

```csharp
public record UserCreatedNotification(Guid UserId) : IEventRequest;

[HandlerOrder(1)]
public class AuditLogHandler : IEventHandler<UserCreatedNotification>
{
    public Task Handle(UserCreatedNotification notification)
    {
        // Invoked first
        return Task.CompletedTask;
    }
}

[HandlerOrder(2)]
public class SendWelcomeEmailHandler : IEventHandler<UserCreatedNotification>
{
    public Task Handle(UserCreatedNotification notification)
    {
        // Invoked second
        return Task.CompletedTask;
    }
}
```

> **Parallel vs. Sequential:**
> By default, multiple handlers start in order but execute concurrently (`PublishStrategy.Parallel`). If you need each handler to fully complete before the next handler starts, configure `options.PublishStrategy = PublishStrategy.Sequential` (see below).

### 3. Conditional Handling (`CanHandle`)

Notification handlers can selectively filter events before processing:

```csharp
public record OrderNotification(decimal TotalAmount) : IEventRequest;

public class HighValueOrderHandler : IEventHandler<OrderNotification>
{
    public bool CanHandle(OrderNotification @event) => @event.TotalAmount > 10000;

    public Task Handle(OrderNotification @event)
    {
        Console.WriteLine($"Processing high-value order: {@event.TotalAmount}");
        return Task.CompletedTask;
    }
}
```

### 4. Deferred Batch Dispatching

Queue multiple events and dispatch them all together:

```csharp
public class OrderService
{
    private readonly IEventBus _eventBus;

    public OrderService(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task ProcessOrdersAsync(CancellationToken ct = default)
    {
        _eventBus.AddStackRequestEvent(new OrderShippedEvent(101));
        _eventBus.AddStackRequestEvent(new OrderShippedEvent(102));

        // Dispatches all queued events in sequence and clears the stack
        await _eventBus.StackEventDispatcherAsync(ct);
    }
}
```

> **Thread-Safe Singleton Queue (`EventStackService`):**
> Deferred events are held by a thread-safe `EventStackService` registered as a DI singleton. Events queued across different HTTP requests or DI scopes are preserved in the shared queue and can be drained and dispatched from any scope or background service. You can also inject `EventStackService` directly if your architecture requires queueing events without referencing `IEventBus`.


### 5. Advanced Configuration (`EventFluxOptions`)

Customize execution behavior globally during registration:

```csharp
builder.Services.AddEventBus(options =>
{
    // Ambient Scope: Handlers share the caller's DI scope (e.g. DbContext / Unit of Work)
    // Default is true (each event runs in an isolated child scope)
    options.CreateScopePerEvent = false;

    // Execution Strategy: Sequential (await each handler in order) or Parallel (Task.WhenAll)
    // Default is PublishStrategy.Parallel
    options.PublishStrategy = PublishStrategy.Sequential;

    // Handler Lifetime: Transient, Scoped, or Singleton
    // Default is ServiceLifetime.Transient
    options.HandlerLifetime = ServiceLifetime.Scoped;
}, typeof(Program).Assembly);

builder.Services.AddEventDispatcher(options =>
{
    options.CreateScopePerEvent = false;
    options.PublishStrategy = PublishStrategy.Sequential;
});
```

### 6. Cancellation Support (`CancellationToken`)

Both `IEventHandler<TRequest, TResponse>` and `IEventHandler<TRequest>` provide `Handle` overloads accepting a `CancellationToken`. Handlers can inspect the token or forward it to asynchronous operations (e.g. database calls, HTTP requests, delays):

```csharp
public class ProcessPaymentHandler : IEventHandler<ProcessPaymentCommand, PaymentResponse>
{
    public async Task<PaymentResponse> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        // Cancels in-flight work when client disconnects or timeout expires
        var result = await _paymentGateway.ChargeAsync(request.Amount, cancellationToken);
        return new PaymentResponse(result.IsSuccess);
    }
}
```

> **Backward Compatibility:**
> Existing handlers implementing `Handle(request)` continue to work without any changes. Implementing the `CancellationToken` parameter is completely opt-in via C# default interface methods.

---

## Ecosystem & Extensions

- [EventFlux.RabbitFlow](https://www.nuget.org/packages/EventFlux.RabbitFlow) — Distributed messaging integration with RabbitMQ.
- [EventFlux.RedisFlow](https://www.nuget.org/packages/EventFlux.RedisFlow) — Redis pub/sub and distributed caching integration.

---

## Running Tests

Run the test suite using the .NET CLI:

```powershell
dotnet test
```

---

## License

This project is licensed under the [MIT License](https://github.com/kadirdemirkaya/EventFlux/blob/main/LICENSE.txt).
