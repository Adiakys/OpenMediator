---
description: "Every AnyMediator configuration option for AddAnyMediator: assembly scanning, lifetimes, behaviors, processors, exception handling and notification publishers."
---

# Configuration

All configuration goes through the callback of `AddAnyMediator`:

```csharp
services.AddAnyMediator(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.Lifetime = ServiceLifetime.Scoped;
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher);
});
```

Calling `AddAnyMediator` more than once is safe: services and behaviors are not registered twice.

## Assemblies

| Method | Scans |
|--------|-------|
| `RegisterServicesFromAssemblyContaining<T>()` | the assembly that declares `T` |
| `RegisterServicesFromAssemblyContaining(Type)` | the assembly that declares the type |
| `RegisterServicesFromAssembly(Assembly)` | one assembly |
| `RegisterServicesFromAssemblies(params Assembly[])` | several assemblies |

## Options

| Option | Default | Effect |
|--------|---------|--------|
| `Lifetime` | `Transient` | lifetime of `ISender`, `IPublisher`, `IMediator`, the notification sink, a custom notification publisher, and open behaviors and open processors registered without one; the built-in notification publishers hold no state and are singletons |
| `NotificationPublisherType` | `ForeachAwaitPublisher` | how notification handlers run |
| `NotificationPublisher` | `null` | a ready instance; wins over `NotificationPublisherType` |
| `AutoRegisterRequestProcessors` | `false` | scan for pre- and post-processors |
| `TypeEvaluator` | every type | `Func<Type, bool>` that decides which scanned types are registered |
| `RequestExceptionActionProcessorStrategy` | `ApplyForUnhandledExceptions` | whether exception actions see recovered exceptions |

Request, stream and notification handlers, processors, exception handlers and actions found by scanning are always transient.

To leave a type out of scanning, for example a handler that only tests use:

```csharp
cfg.TypeEvaluator = type => type.Namespace?.EndsWith(".Testing") != true;
```

## Behaviors

| Method | Purpose |
|--------|---------|
| `AddOpenBehavior(Type, ServiceLifetime?)` | open generic request behavior |
| `AddOpenBehaviors(IEnumerable<Type>, ServiceLifetime?)` | several, same lifetime |
| `AddOpenBehaviors(IEnumerable<OpenBehavior>)` | several, each with its own lifetime |
| `AddBehavior<TImplementation>()` | closed behavior, every interface it implements |
| `AddBehavior<TService, TImplementation>()` | closed behavior, one service type |
| `AddOpenStreamBehavior`, `AddOpenStreamBehaviors`, `AddStreamBehavior` | the same for stream behaviors |

Behaviors run in registration order; see [Pipeline behaviors](./pipeline-behaviors#order).

## Processors and exception handling

| Method | Purpose |
|--------|---------|
| `AddRequestPreProcessor<T>()` / `AddRequestPreProcessor<TService, TImplementation>()` | closed pre-processor |
| `AddOpenRequestPreProcessor(Type)` | open generic pre-processor for every request |
| `AddRequestPostProcessor<T>()` / `AddRequestPostProcessor<TService, TImplementation>()` | closed post-processor |
| `AddOpenRequestPostProcessor(Type)` | open generic post-processor for every request |
| `AddRequestExceptionHandler<T>()` | exception handler |
| `AddRequestExceptionAction<T>()` | exception action |

Every method also has an overload that takes `Type` arguments instead of generic ones.

## Services registered

| Service | Implementation |
|---------|----------------|
| `ISender` | the request and stream dispatcher |
| `IPublisher` | the notification fan-out to sinks |
| `IMediator` | both of the above |
| `INotificationPublisher` | `NotificationPublisherType` or `NotificationPublisher` |
| `INotificationSink` | the in-memory sink that runs your handlers |
