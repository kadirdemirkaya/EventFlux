# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- `IEventRequest<Unit>` and the built-in `Unit` response type for commands that don't return a value,
  so a `SendAsync` call for a fire-and-forget command no longer needs an empty response class.
- `AddEventOpenBehavior` and `AddEventBehavior` helpers to register pipeline behaviors, validated at
  registration time instead of failing later when the container is built or an event is dispatched.
- `EventFluxOptions.Timeout` to configure the time limit used by the built-in `AddEventTimeout()`
  behavior (previously fixed at 30 seconds).
- `AddEventBus<TMarker>()` / `AddEventBus<TMarker>(options => ...)` overloads that scan the assembly
  declaring `TMarker`, removing the risk of pointing assembly scanning at the wrong assembly.
- `EventFluxOptions.RequeueStackOnCancellation` (opt-in, default `false`): when the caller's token is
  cancelled mid-drain, queued stack events that were not yet dispatched are put back into the shared
  queue in their original order instead of being discarded.
- README sections on dispatch error-handling contracts, registration details, and NativeAOT/trimming
  compatibility.

### Changed

- `AddEventBus` is now safe to call more than once; handlers discovered by later calls are merged into
  the existing `EventService` / `EventMapService` instead of re-registering shared services.
- Assembly scanning now tolerates partially loadable assemblies: types that fail to load are skipped
  instead of aborting handler discovery for the whole assembly.
- Registering a scanned handler with an open generic type (for example `Handler<T> : IEventHandler<Event>`)
  now throws a descriptive `InvalidOperationException` at registration time instead of failing later.
- `PublishAsync` with `PublishStrategy.Parallel` now aggregates every failing handler's exception into a
  single `AggregateException` instead of only surfacing the first failure and dropping the rest.
- A cancellation raised through the caller's own token during `StackEventDispatcherAsync` is no longer
  logged as a dispatch failure.
- The registration extension methods (`AddEventBus`, `AddEventDispatcher`, `AddEventLogging`,
  `AddEventTimeout`, `AddEventBehavior`, `AddEventOpenBehavior`) now live directly in the
  `Microsoft.Extensions.DependencyInjection` namespace; the original `EventFlux.Extensions` namespace
  still works and forwards to them.
- `AddEventBus`, `AddEventDispatcher`, `EventBus` and `EventDispatcher` are annotated with
  `[RequiresDynamicCode]` and `[RequiresUnreferencedCode]`, so NativeAOT/trimmed publishes get a
  build-time warning instead of a runtime failure.
- Refreshed dispatch benchmark numbers and methodology in the README to reflect the current fast paths.

### Deprecated

- The `EventBus` constructors that accept unused `assemblies`, `dictionaryService`,
  `eventDictionaryMapService` and `handlers` parameters are marked `[Obsolete]`; those parameters were
  already ignored. Resolve `IEventBus` from DI after `AddEventBus()` instead.

### Fixed

- `IEventBus.SendAsync` / `PublishAsync` and `IEventDispatcher.SendAsync` / `PublishAsync` now throw
  `ArgumentNullException` for a `null` request instead of failing later inside dispatch.
- Aligned the versions of the framework abstraction dependencies referenced per target framework,
  removing a class of build-time version-mismatch warnings.

### Performance

- Added single-handler and synchronous-completion fast paths to `PublishAsync`.
- `EventDispatcher` skips building its pipeline delegate chain entirely when no behaviors are registered
  for a request.

## [2.0.0] - 2026-09-14

### Changed

- **Breaking:** dropped the `net6.0` and `net7.0` target frameworks; the package now targets `net8.0`,
  `net9.0` and `net10.0`.
- **Breaking:** `IEventHandler.Handle` now requires a `CancellationToken` parameter; the overload
  without it was removed.
- **Breaking:** `EventMapService` moved from the `EventFlux` namespace to `EventFlux.Services`.
- **Breaking:** `CreateScopePerEvent` now defaults to `false` (ambient scope by default) instead of
  `true`.

### Fixed

- `TimeoutBehavior` now distinguishes a caller-triggered cancellation from an actual timeout, so it no
  longer logs a false timeout warning when the caller cancels first.
- Eliminated a `NullReferenceException` in the parameterless `EventService` / `EventMapService`
  constructors and cleared the library's remaining nullable-reference warnings.

### Performance

- Modernized `TimeoutBehavior` to use `WaitAsync` for reliable cancellation on every supported target
  framework, replacing the previous delay-race implementation.
- Reduced allocations on the publish path.

## [1.5.1] - 2026-09-12

### Changed

- Packaging clean-up for release binaries; no public API change.

## [1.5.0] - 2026-09-12

### Added

- `EventFluxOptions.HandlerLifetime` to configure whether handlers are registered as transient, scoped
  or singleton.
- `PublishStrategy` option to run notification handlers sequentially or in parallel.
- Optional `CancellationToken` propagation through event handlers.
- `EventStackService` is registered as a singleton so the deferred/batch queue is resolvable across
  scopes.

### Fixed

- Duplicate request handler registrations are now detected and rejected at registration time.
- Improved type safety and a null-return defect in the event service registries.
- Aligned `CanHandle` behavior in `SendAsync` and fixed a nullable-contract compiler warning (`CS8613`).

## [1.4.1] - 2026-09-06

### Changed

- README documentation overhaul, including measured test coverage; no public API change.

## [1.4.0] - 2026-09-06

### Added

- SourceLink and a symbol package (`.snupkg`) for source-level debugging, plus XML documentation
  comments on public API members.

### Changed

- Dropped an unused configuration dependency.

### Fixed

- Hardened DI registrations and prevented duplicate pipeline behavior registrations.
- Unwrapped handler exceptions thrown through reflection instead of surfacing a wrapping
  `TargetInvocationException`, and guarded against an uninitialized event bus.
- Publish handlers are now resolved from DI instead of a runtime assembly scan on every call.
- Removed a global publish semaphore that could deadlock nested publishes.

### Performance

- Cached compiled handler delegates instead of resolving them by reflection on every dispatch.

## [1.2.1] - 2025-12-13

### Added

- Concurrency test coverage and dispatch benchmarks.

### Changed

- Concurrency-related handler dispatch improvements.

## [1.2.0] - 2025-10-27

### Added

- `[HandlerOrder]` attribute support to control invocation order for multiple handlers of the same
  event.

## [1.1.9] - 2025-09-20

- Early project layout changes. Full contents cannot be established from git history beyond the
  commit message.

## [1.1.0] - 2025-10-27

### Added

- Initial public release: request/response dispatch, publish/subscribe with multiple handlers, and
  priority-ordered multi-handler invocation.

[Unreleased]: https://github.com/kadirdemirkaya/EventFlux/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/kadirdemirkaya/EventFlux/compare/v1.4.0...v2.0.0
[1.5.1]: https://github.com/kadirdemirkaya/EventFlux/compare/8f5b37d...865ba20
[1.5.0]: https://github.com/kadirdemirkaya/EventFlux/compare/f2b6215...8f5b37d
[1.4.1]: https://github.com/kadirdemirkaya/EventFlux/compare/3d192c3...f2b6215
[1.4.0]: https://github.com/kadirdemirkaya/EventFlux/compare/v1.2.1...v1.4.0
[1.2.1]: https://github.com/kadirdemirkaya/EventFlux/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/kadirdemirkaya/EventFlux/compare/v1.1.9...v1.2.0
[1.1.9]: https://github.com/kadirdemirkaya/EventFlux/compare/v1.1.0...v1.1.9
[1.1.0]: https://github.com/kadirdemirkaya/EventFlux/releases/tag/v1.1.0
