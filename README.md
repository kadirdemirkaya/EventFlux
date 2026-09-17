# EventFlux

[![NuGet](https://img.shields.io/nuget/v/EventFlux.svg)](https://www.nuget.org/packages/EventFlux)
[![Downloads](https://img.shields.io/nuget/dt/EventFlux.svg)](https://www.nuget.org/packages/EventFlux)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/kadirdemirkaya/EventFlux/blob/main/LICENSE.txt)

EventFlux is a lightweight, high-performance in-memory event dispatching and CQRS library for .NET. It supports point-to-point request/response messaging, publish/subscribe event broadcasting, pipeline behaviors (middleware), handler ordering, and deferred batch dispatching.

---

## Key Features

- **Blazing Fast**: Compiled expression tree delegate caching and allocation-lean dispatch — ~113 ns for `SendAsync` and ~225 ns / 424 B for a single-handler `PublishAsync`.
- **Request / Response**: Send a command or query to a single handler and receive a response via `SendAsync` — or send a command that returns nothing with `IEventRequest<Unit>`.
- **Publish / Subscribe**: Broadcast notification events to multiple handlers via `PublishAsync`.
- **Execution Strategies**: Run notification handlers concurrently (`Parallel`) or in guaranteed order (`Sequential`) via `PublishStrategy`.
- **Cancellation Aware**: First-class `CancellationToken` propagation across dispatchers, pipelines, and handlers.
- **Ambient Scope Support**: Seamlessly share the caller's DI scope (EF Core `DbContext`, Unit of Work) via `EventFluxOptions`.
- **Configurable Lifetime**: Register handlers as `Transient`, `Scoped`, or `Singleton` per application needs.
- **Pipeline Behaviors**: Intercept requests with cross-cutting concerns (validation, logging, caching) using `IEventCustomPipeline`.
- **Handler Ordering**: Control execution sequence for multi-handler events with `[HandlerOrder(priority)]`.
- **Precondition Evaluation**: Selectively gate handler execution across both `SendAsync` and `PublishAsync` with `CanHandle`.
- **Deferred Batch Dispatch**: Queue events and dispatch them across scopes via `EventStackService` and `StackEventDispatcherAsync`.
- **Multi-Targeting**: Supports .NET 8.0, 9.0, and 10.0 with SourceLink and symbol debugging (`.snupkg`) enabled.

---

## Quick Start

### 1. Installation

Package Manager Console:
```powershell
dotnet add package EventFlux --version 2.0.0
```

Or via `<PackageReference>` in your `.csproj`:
```xml
<PackageReference Include="EventFlux" Version="2.0.0" />
```

### 2. Register Services

In your `Program.cs` / `Startup.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register EventFlux and scan the assembly that declares Program for handlers
builder.Services.AddEventBus<Program>();

// Optional: register EventDispatcher when using pipeline behaviors or HandlerOrder
builder.Services.AddEventDispatcher();

// Optional built-in behaviors
builder.Services.AddEventLogging();
builder.Services.AddEventTimeout();

// Register any custom pipeline behaviors
builder.Services.AddEventOpenBehavior(typeof(ValidationBehavior<,>));

var app = builder.Build();
app.Run();
```

The registration methods live in the `Microsoft.Extensions.DependencyInjection` namespace, so no extra `using` is needed. Existing `using EventFlux.Extensions;` directives keep working.

---

## End-to-End Example

### 1. Define Request, Response and Handler

```csharp
using EventFlux.Abstractions;

// Define request and response (response must implement IEventResponse)
public record CreateUserCommand(string Username, string Email) : IEventRequest<CreateUserResponse>;
public record CreateUserResponse(Guid UserId, bool Success) : IEventResponse;

// Define handler (CancellationToken is mandatory on IEventHandler in v2.0+)
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
        CreateUserResponse? response = await _eventBus.SendAsync(command, ct);
        return Ok(response);
    }
}
```

### 3. Commands Without a Response (`Unit`)

A command that returns nothing uses the built-in `Unit` response type, so you don't need an empty response class:

```csharp
public record DeleteUserCommand(Guid UserId) : IEventRequest<Unit>;

public class DeleteUserHandler : IEventHandler<DeleteUserCommand, Unit>
{
    public Task<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        // Execute business logic...
        return Unit.Task;
    }
}

await _eventBus.SendAsync(new DeleteUserCommand(userId), ct);   // returns Task, nothing to unwrap
```

`SendAsync(IEventRequest<Unit>)` is available on both `IEventBus` and `IEventDispatcher` (pipeline behaviors still run on the dispatcher).

---

## EventBus vs. EventDispatcher

EventFlux provides two dispatch interfaces to fit your performance and architectural requirements:

| Capability | `IEventBus` | `IEventDispatcher` |
|---|---|---|
| **Primary Focus** | Direct, high-throughput, low-latency dispatch | Extensible pipeline dispatch |
| **Pipeline Behaviors (`IEventCustomPipeline`)** | ❌ Bypassed (direct invocation) | ✅ Supported (wraps handlers in pipeline) |
| **`[HandlerOrder]` Support** | ✅ Supported (order-based invocation) | ✅ Supported (order-based invocation) |
| **Dispatch Overhead** | Minimal (~113 ns send, ~225 ns publish) | Low (~269 ns send, ~376 ns publish — includes pipeline middleware) |
| **Publish / Subscribe (`PublishAsync`)** | ✅ Supported | ✅ Supported |
| **Batch / Stack Dispatch (`AddStackRequestEvent`)** | ✅ Supported | ❌ |

> **When to use which?**
> - Use **`IEventBus`** when you want fast, direct execution without pipeline middleware overhead.
> - Use **`IEventDispatcher`** when you need cross-cutting behaviors (validation, logging, caching, metrics).

### Benchmarks

Measured with [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet) on .NET 8.0 (`ShortRunJob` +
`MemoryDiagnoser`), handlers returning `Task.CompletedTask` so the numbers reflect dispatch overhead
rather than handler work.

| Operation | Mean | Allocated |
|---|---:|---:|
| `IEventBus.SendAsync` | 113 ns | 320 B |
| `IEventBus.PublishAsync` (1 handler) | 225 ns | 424 B |
| `IEventBus.PublishAsync` (3 handlers) | 299 ns | 536 B |
| `IEventDispatcher.SendAsync` | 269 ns | 512 B |
| `IEventDispatcher.PublishAsync` (1 handler) | 376 ns | 616 B |
| `IEventDispatcher.PublishAsync` (3 handlers) | 481 ns | 720 B |

Absolute numbers depend on your hardware and runtime — treat them as relative guidance, not a guarantee.

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
// Open generic behavior: runs for every request
builder.Services.AddEventOpenBehavior(typeof(ValidationBehavior<,>));

// Closed behavior: registered for every IEventCustomPipeline<...> interface it implements
builder.Services.AddEventBehavior<AuditCreateUserBehavior>();
```

Behaviors run in registration order, and registering the same behavior twice has no effect. Both methods accept an optional `ServiceLifetime` (default `Transient`).

`AddEventOpenBehavior` checks the type when you register it. If the type is not an open generic, implements no `IEventCustomPipeline`, or has type parameters that don't map in order onto the interface (for example `Behavior<TRequest> : IEventCustomPipeline<TRequest, MyResponse>`), it throws an `ArgumentException` right away. Otherwise the container would only fail later, when it is built or when an event is dispatched. Registering with `AddTransient(typeof(IEventCustomPipeline<,>), ...)` directly still works too.

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

Handlers can selectively filter events before processing across both `PublishAsync` and `SendAsync`. If `CanHandle` evaluates to `false`, execution is skipped (for `SendAsync`, `null` is returned):

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
    // Default is false in v2.0+ (ambient scope). Set to true for isolated child scope per event.
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

    // Time limit for the built-in AddEventTimeout() behavior
    // Default is null, which keeps the built-in 30 seconds; Timeout.InfiniteTimeSpan disables the limit
    options.Timeout = TimeSpan.FromSeconds(10);
});
```

> `EventFluxOptions` is a single shared instance. A call that passes a configure action replaces options set by an earlier call, so set every option in the last `AddEventBus` / `AddEventDispatcher` call that configures them.

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

> **v2.0 Note:**
> In v2.0+, `CancellationToken` is a mandatory parameter on `IEventHandler.Handle`. Add `CancellationToken cancellationToken = default` to all handler implementations when upgrading from v1.x.

### 7. Error Handling

The three dispatch operations deliberately handle failures differently. Pick the one whose contract matches what the caller needs:

| Operation | Handler throws | Handler's `CanHandle` returns `false` | No handler registered |
|---|---|---|---|
| `SendAsync` | Exception propagates to the caller | Handler is skipped and the result is **`null`** — no exception | `InvalidOperationException` |
| `PublishAsync` — `Parallel` (default) | All handlers run; afterwards a single failure is rethrown as is, several failures are thrown together as an **`AggregateException`** holding every handler's exception | Handler is skipped | Completes silently |
| `PublishAsync` — `Sequential` | Exception is rethrown at once; the **remaining handlers are not invoked** | Handler is skipped | Completes silently |
| `StackEventDispatcherAsync` | Exception is **logged at error level and not rethrown**; the next queued event is still dispatched | Handler is skipped | Completes silently |

- `IEventBus` and `IEventDispatcher` both throw `ArgumentNullException` when `SendAsync` or `PublishAsync` is called with a `null` request.
- Because `SendAsync` returns `null` when a handler declines the request, always null-check the response of a handler that implements `CanHandle`.
- `StackEventDispatcherAsync` checks the `CancellationToken` before each queued event and rethrows the cancellation; queued events not yet dispatched at that point are discarded.
- With `IEventDispatcher`, exceptions travel back through your pipeline behaviors before reaching the caller, so a behavior can log, translate or handle them. The built-in timeout behavior throws `OperationCanceledException` when the timeout expires.

### 8. Registration Details

- **Marker-type overload.** `AddEventBus<TMarker>()` and `AddEventBus<TMarker>(options => ...)` scan the assembly that declares `TMarker`. This is the same as `AddEventBus(typeof(TMarker).Assembly)`, but it can't point at the wrong assembly.
- **Namespace.** `AddEventBus`, `AddEventDispatcher`, `AddEventLogging`, `AddEventTimeout`, `AddEventBehavior` and `AddEventOpenBehavior` are in `Microsoft.Extensions.DependencyInjection` (class `EventFluxServiceCollectionExtensions`). The original `EventFlux.Extensions.EventBusServiceExtension` class still exists and forwards to it, so existing code compiles and runs unchanged, including files that import both namespaces.
- **`AddEventBus` is safe to call more than once.** Modular applications can call it per module: each handler, `IEventBus`, `EventService` and `EventMapService` is registered once, and handlers found by later calls are merged into the existing `EventService` / `EventMapService`.
- **Partially loadable assemblies are tolerated.** If some types in a scanned assembly cannot be loaded (for example because an optional dependency is missing), those types are skipped and the remaining handlers are registered.
- **Open generic handlers are rejected at registration.** A scanned handler such as `class AuditHandler<T> : IEventHandler<OrderPlaced>` cannot be constructed by the container, so `AddEventBus` throws an `InvalidOperationException` that names the handler type. Create a non-generic handler for each event instead.

### 9. NativeAOT and Trimming

EventFlux discovers handlers through assembly scanning and dispatches them through runtime-compiled expression trees, so it is **not compatible with NativeAOT** and handler types may be removed by the trimmer. `AddEventBus`, `AddEventDispatcher`, `EventBus` and `EventDispatcher` are annotated with `[RequiresDynamicCode]` and `[RequiresUnreferencedCode]`: projects that set `PublishAot` or `PublishTrimmed` get a build-time warning at the call site instead of a runtime failure.

---

## Upgrading to v2.0

v2.0 contains the following breaking changes:

| Change | Migration |
|---|---|
| Dropped `net6.0` / `net7.0` targets | Upgrade your project to `net8.0` or later |
| `IEventHandler.Handle` now requires `CancellationToken` | Add `CancellationToken cancellationToken = default` parameter to all handler implementations |
| `EventMapService` moved to `EventFlux.Services` namespace | Update `using` directives from `using EventFlux;` to `using EventFlux.Services;` where referencing `EventMapService` directly |
| `CreateScopePerEvent` defaults to `false` | If you relied on isolated child scopes, explicitly set `options.CreateScopePerEvent = true` |

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
