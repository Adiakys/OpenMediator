---
description: "Move a .NET project from MediatR to AnyMediator, the free Apache-2.0 alternative. Change the package, the namespace and AddMediatR, then check the few behavioral differences."
---

# Migrating from MediatR

::: tip Coming from OpenMediatR?
AnyMediator was published as OpenMediatR up to 1.5.0. See [Upgrading from OpenMediatR](#upgrading-from-openmediatr) at the end of this page.
:::

AnyMediator keeps MediatR's type names, method names and configuration API. Most projects migrate with three mechanical changes and a review of the differences below. You can try the changes on your own files with the converter on the [home page](/#migrate).

## Why migrate

From version 13, MediatR is dual licensed: the copyleft Reciprocal Public License 1.5, or a commercial license with a license key. The commercial Community edition is free only below $5 million in annual revenue and $10 million in outside capital. AnyMediator is Apache-2.0: free for any use, commercial included, with no key and no threshold. See [MediatR alternatives compared](./mediatr-alternatives) for the details and the other options.

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
- using MediatR.NotificationPublishers;
+ using AnyMediator.NotificationPublishers;
```

Everything lives in the `AnyMediator` namespace, including the pre/post-processor and exception-handler interfaces that MediatR keeps in `MediatR.Pipeline`. The only exception is the notification publishers (`TaskWhenAllPublisher`, `ForeachAwaitPublisher`), in `AnyMediator.NotificationPublishers` as in MediatR. Global usings work the same way.

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
| License | RPL-1.5 or commercial from v13 | Apache-2.0, free for any use |
| `LicenseKey` | Needed under the commercial license; without it, a warning is logged | Does not exist: remove it |
| Two handlers for one request | The first one found is used | `AddAnyMediator` throws and names both classes |
| `Publish` with several sinks failing | — | Every sink runs, then an `AggregateException` with every failure |
| Notification delivery | In-process handlers | In-process handlers, plus any [notification sink](./notification-sinks) you add |
| Custom `INotificationPublisher` | `Publish(IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken)` | `Publish(IEnumerable<Func<Task>>, CancellationToken)`: each delegate runs one handler |

Interfaces, method shapes, `Send(object)`, `Publish(object)`, behavior constraints, handlers for base notification types, generic handlers and exceptions from `Publish` work as in MediatR.

### Two handlers for one request

If scanning finds two classes that handle the same request, `AddAnyMediator` stops with an `InvalidOperationException` naming both. A test double in a scanned assembly is the usual cause: give it a request type of its own, or leave it out with `cfg.TypeEvaluator`.

## Checklist

1. Replace the package and the namespaces.
2. Replace `AddMediatR` with `AddAnyMediator` and remove `LicenseKey`.
3. Adapt any custom `INotificationPublisher` to the delegate signature.
4. Start the application: fix any request reported as having two handlers.
5. Run your tests.

## Upgrading from AnyMediator 2.0

AnyMediator 2.1 brings AnyMediator in line with MediatR. Review the first four changes before upgrading:

- **`Publish` re-throws handler failures.** In 2.0 a failing handler was logged and `Publish` completed normally. AnyMediator no longer depends on `Microsoft.Extensions.Logging.Abstractions`.
- **Two handlers for one request is a startup error.** In 2.0 the first one found was used silently.
- **`ISender` and `IPublisher` gain `Send(object)` and `Publish(object)`.** A class that implements them by hand, such as a decorator or a test fake, must add the two methods; mocks created by a mocking library are unaffected.
- **Handlers for base notification types run.** A handler for `INotificationHandler<IDomainEvent>` now also receives `OrderPlaced : IDomainEvent`.
- New: generic request and notification handlers, `cfg.TypeEvaluator`, and the `notnull` behavior constraint, so behaviors written for MediatR compile unchanged.

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
