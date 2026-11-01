---
description: "Move a .NET project from MediatR to AnyMediator, the free Apache-2.0 alternative. Change the package, the namespace and AddMediatR, then check the few behavioral differences."
---

# Migrating from MediatR

::: tip Coming from OpenMediatR?
AnyMediator was published as OpenMediatR up to 1.5.0. See [Upgrading from OpenMediatR](#upgrading-from-openmediatr) at the end of this page.
:::

AnyMediator keeps MediatR's type names, method names and configuration API. Most projects migrate with three mechanical changes and a review of the differences below. You can try the changes on your own files with the converter on the [home page](/#migrate).

## Why migrate

From version 13, MediatR is distributed under a commercial license and needs a license key. AnyMediator is Apache-2.0: free for any use, commercial included, with no key and no revenue threshold.

## The three changes

### 1. Swap the package

```bash
dotnet remove package MediatR
dotnet add package AnyMediator
```

If a project referenced `MediatR.Contracts`, reference `AnyMediator.Abstractions` instead.

### 2. Change the namespace

```diff
- using MediatR;
- using MediatR.Pipeline;
+ using AnyMediator;
```

Everything lives in the single `AnyMediator` namespace, including the pre/post-processor and exception-handler interfaces that MediatR keeps in `MediatR.Pipeline`. Global usings work the same way.

### 3. Change the registration call

```diff
- services.AddMediatR(cfg =>
+ services.AddAnyMediator(cfg =>
  {
-     cfg.LicenseKey = "...";
      cfg.RegisterServicesFromAssemblyContaining<Program>();
      cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
  });
```

`RegisterServicesFromAssembly`, `RegisterServicesFromAssemblies`, `RegisterServicesFromAssemblyContaining`, `AddOpenBehavior`, `AddBehavior`, `AddOpenStreamBehavior`, `AddRequestPreProcessor`, `AddRequestPostProcessor`, `AddRequestExceptionHandler`, `AddRequestExceptionAction`, `Lifetime`, `NotificationPublisher`, `NotificationPublisherType`, `AutoRegisterRequestProcessors` and `RequestExceptionActionProcessorStrategy` keep their names.

## Differences to check

| Area | MediatR | AnyMediator |
|------|---------|-------------|
| License | Commercial license from v13 | Apache-2.0, free for any use |
| `LicenseKey` | Required for commercial use | Does not exist: remove it |
| Behavior constraint | `where TRequest : notnull` | `where TRequest : IRequest<TResponse>` |
| Stream behavior constraint | `where TRequest : notnull` | `where TRequest : IStreamRequest<TResponse>` |
| `Publish` when a handler throws | The exception reaches the caller | The exception is logged and not re-thrown |
| `Send(object)` and `Publish(object)` | Available | Not available: use the generic overloads |
| Notification delivery | In-process handlers | In-process handlers, plus any [notification sink](./notification-sinks) you add |

### Behavior constraints

A generic behavior written for MediatR declares `where TRequest : notnull`. In AnyMediator, `IPipelineBehavior<TRequest, TResponse>` requires `TRequest : IRequest<TResponse>`, so the compiler reports CS0314 until you change the constraint:

```diff
  public sealed class LoggingBehavior<TRequest, TResponse>
      : IPipelineBehavior<TRequest, TResponse>
-     where TRequest : notnull
+     where TRequest : IRequest<TResponse>
```

### Publish no longer throws

`IPublisher.Publish` sends a notification to every registered [notification sink](./notification-sinks). If a sink fails, which includes a failing handler in the default in-memory sink, the exception is logged through `ILogger` and the remaining sinks still run. The call to `Publish` completes normally.

Review code that catches exceptions around `Publish`. If the caller must know that a step failed, make that step a request and use `Send`, which propagates exceptions.

## Checklist

1. Replace the package and the namespace.
2. Replace `AddMediatR` with `AddAnyMediator` and remove `LicenseKey`.
3. Fix behavior constraints until the project builds.
4. Search for `Publish(` and review error handling around it.
5. Run your tests.

## Upgrading from OpenMediatR

AnyMediator 2.0.0 is OpenMediatR 1.5.0 under a new name. Behavior is unchanged; only names change.

```bash
dotnet remove package OpenMediatR
dotnet add package AnyMediator
```

| OpenMediatR | AnyMediator |
|-------------|-------------|
| `OpenMediatR` / `OpenMediatR.Abstractions` packages | `AnyMediator` / `AnyMediator.Abstractions` |
| `using OpenMediatR;` | `using AnyMediator;` |
| `using OpenMediatR.NotificationPublishers;` | `using AnyMediator.NotificationPublishers;` |
| `services.AddOpenMediatR(...)` | `services.AddAnyMediator(...)` |
| `OpenMediatRConfiguration` | `AnyMediatorConfiguration` |

The converter on the [home page](/#migrate) applies these changes too.

---

AnyMediator is an independent project. It is not affiliated with or endorsed by MediatR, Jimmy Bogard or Lucky Penny Software; MediatR is named only to describe compatibility.
