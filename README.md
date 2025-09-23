## NuGet Package Information

| Package | Downloads | License |
|---------|-----------|---------|
| [![NuGet](https://img.shields.io/nuget/v/EventFlux)](https://www.nuget.org/packages/EventFlux) | [![Downloads](https://img.shields.io/nuget/dt/EventFlux)](https://www.nuget.org/packages/EventFlux) | [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/kadirdemirkaya/EventFlux/blob/main/LICENSE.txt) |

# EventFlux

EventFlux is a lightweight and performance-focused event processing library for .NET. It makes it easy to trigger a request and process it with multiple handlers, add pipeline behaviors, and perform deferred batch triggering.

## Key Features

- Event-driven architecture
- Automatic event handler discovery and logging
- Support for deferred/batch event triggering
- Support for multiple handlers
- Add pipeline behaviors (e.g., validation, logging, cache)
- Dynamic dispatch
- Handler prerequisite query: Pre-evaluation with the `CanHandle` method

## Quick Start

1. Install a package from NuGet (package name provided as an example):

```powershell
dotnet add package EventFlux
```

2. Add services in Program.cs / Startup.cs:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Basic plugins
builder.Services
.AddEventBus(AssemblyReference.Assemblies) // or any assembly list you want
.AddEventLogging() // optional
.AddEventTimeout(); // optional

builder.Services.AddEventDispatcher();

// Example: Adding custom pipeline behavior
builder.Services.AddTransient(typeof(IEventCustomPipeline<,>), typeof(ValidationBehavior<,>));

var app = builder.Build();
app.Run();
```

## Usage examples

- EventBus (usage awaiting an event -> a response):

```csharp
public class ExampleController(IEventBus _eventBus) : ControllerBase
{ 
    [HttpPost("create-user")] 
    public async Task<IActionResult> CreateUser([FromBody] CreateUserCommandRequest command) 
    { 
        CreateUserCommandResponse response = await _eventBus.SendAsync(command); 
        return Ok(response); 
    }
}
```

- EventDispatcher (for more dynamic/multiple redirect scenarios):

```csharp
public class ExampleController(IEventDispatcher _eventDispatcher) : ControllerBase
{ 
    [HttpPost("update-user")] 
    public async Task<IActionResult> UpdateUser([FromBody] UpdateUserCommandRequest command) 
    { 
        UpdateUserCommandResponse response = await _eventDispatcher.SendAsync(command); 
        return Ok(response); 
    }
}
```

## Example custom pipeline

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
        Console.WriteLine($"Validating {typeof(TRequest).Name}");

        return await next();
    }
}
```

## CanHandle example

You can use `CanHandle` to perform a precondition check before calling the handler:

```csharp
public class ExampleEventHandler : IEventHandler<ExampleEventRequest, ExampleEventResponse>
{
    public bool CanHandle(ExampleEventRequest @event)
        => @event.Num > 1000;

    public async Task<ExampleEventResponse> Handle(ExampleEventRequest @event)
    {
        return new() { Res = @event.Num.ToString() };
    }
}
```

## Tests
- The project includes unit tests for EventBus and EventDispatcher. The tests verify the following:
- Correct operation of the SendAsync and PublishAsync methods
- Whether handlers are triggered according to the CanHandle method
- Correct execution of the pipeline (e.g., TimeoutBehavior, TriggerBehavior) and handler chain
- Appropriate handling of cancellation token and timeout conditions
- Correct catching or throwing of exceptions

```csharp
cd .\test\EventFlux.Test\
dotnet test
```

## Configuration and Tips

- **AddEventLogging**: Adds logging to monitor or debug the event flow.
- **AddEventTimeout**: Provides a timeout policy for long-running handlers.
- **AddEventBus / AddEventDispatcher**: Specify which assemblies to scan explicitly for handlers.
  - **EventBus**: Basic event sending and publishing.
  - **EventDispatcher**: Extends EventBus by allowing pipeline behaviors (`IEventCustomPipeline<,>`, `IEventCustomPipeline<>`) to be added between the request and handlers.
- **Pipeline Behaviors**: Inject custom logic such as validation, logging, caching, or other pre/post-processing for handlers.
- **CanHandle**: Implement preconditions in handlers to control whether a handler should execute.
- **Error Handling**: Catch exceptions within handlers to log or generate appropriate responses; unhandled exceptions may propagate depending on your implementation.
- **Unit Tests**: Use mocks or isolated pipeline behaviors to test handler execution, timeouts, and cancellation tokens.

