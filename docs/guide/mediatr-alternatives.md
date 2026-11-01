---
description: "MediatR alternatives for .NET compared: AnyMediator, Mediator, LiteBus, Wolverine and FreeMediator, by license, API compatibility, features and Native AOT, with MediatR's current licensing."
---

# MediatR alternatives compared

Since version 13, MediatR is no longer under an open-source-only license, and many teams are looking at the alternatives. This page compares the ones most often suggested, from their own documentation and NuGet packages. It is written by the AnyMediator project, so check the sources at the end for anything that matters to your decision.

Facts as of November 2026.

## MediatR's license today

- **Versions 13.0.0 and later** are dual licensed: the Reciprocal Public License 1.5, a copyleft license that requires releasing your own source under the same terms, or Lucky Penny Software's commercial license.
- **The Community edition is free** for organizations with gross annual revenue under $5 million that never received more than $10 million in outside capital. Government agencies are not eligible. It requires registering for a license key.
- **Paid tiers** are priced per number of developers.
- **Without a valid key** MediatR keeps working and logs a warning; there is no license server.
- **Versions up to 12.5.0** remain Apache-2.0 and can be used indefinitely, but receive no updates.

## At a glance

| | AnyMediator | MediatR | Mediator | LiteBus | Wolverine | FreeMediator |
|---|---|---|---|---|---|---|
| License | Apache-2.0 | RPL-1.5 or commercial | MIT | MIT | MIT | MIT |
| Same API as MediatR | Yes, namespace `AnyMediator` | — | Similar names; `ValueTask`, different delegates, singleton by default | No (`ICommand`, `IQuery`, `IEvent`) | No (convention-based handlers) | Yes, namespace `FreeMediator` |
| Dispatch | Generic wrappers built once per message type | Runtime reflection | Source generator | Reflection at registration | Code generated at startup or ahead of time | Reflection and `Reflection.Emit` |
| Stream requests | Yes | Yes | Yes | Yes | Yes | No |
| Pipeline behaviors | Yes | Yes | Yes | Pre/post/error stages instead | Middleware | Yes, also for notifications |
| Pre/post-processors | Yes | Yes | Yes | Yes | Through middleware | No |
| Exception handlers and actions | Yes | Yes | Yes | Yes | Error policies | No |
| Notification strategies | Sequential, parallel, continue on exception, plus [sinks](./notification-sinks) | Sequential, parallel | Sequential, parallel | Sequential or parallel, by priority | Local queues | Sequential |
| Tracing | [OpenTelemetry spans and metric](./tracing) | Not built in | In 3.1 (pre-release) | Not documented | Yes | Not documented |
| Native AOT | Not supported | Not advertised | Yes | Not documented | In progress | Not documented (uses `Reflection.Emit`) |
| Targets | .NET 8, .NET 10 | .NET Framework 4.6.2, .NET Standard 2.0, .NET 8–10 | .NET Standard 2.0, .NET 8 | .NET 10 | .NET 9, .NET 10 | .NET 10 |

## Which one to choose

- **Keep MediatR** if your organization qualifies for the Community edition or is happy to pay for a license and support from its author.
- **AnyMediator** if you want to leave MediatR without touching handlers: same type names and configuration API, all of MediatR's features including streams, processors and exception handlers, Apache-2.0 with no key. Migration is [a package, a namespace and the registration call](./migrating-from-mediatr).
- **Mediator** if raw speed or Native AOT matters most, thanks to its source generator. Expect to adapt behaviors to `ValueTask` and its delegate types.
- **Wolverine** if you also want messaging, queues and durable outboxes: it is a framework rather than a mediator library, with its own handler conventions.
- **LiteBus** if you prefer separate command, query and event mediators and are on .NET 10.
- **FreeMediator** for a close MediatR clone on .NET 10 when you do not need streams, processors or exception handlers.

For speed numbers measured on the same machine, see [Performance](./performance): AnyMediator is on par with MediatR for `Send` and faster for `Publish`, while Mediator's source generator is an order of magnitude faster than both.

## Sources

- MediatR: [license](https://github.com/LuckyPennySoftware/MediatR/blob/main/LICENSE.md), [license agreement](https://luckypennysoftware.com/license), [pricing and FAQ](https://mediatr.io/), [NuGet](https://www.nuget.org/packages/MediatR), [NuGet 12.5.0](https://www.nuget.org/packages/MediatR/12.5.0)
- Mediator: [repository and differences from MediatR](https://github.com/martinothamar/Mediator), [NuGet](https://www.nuget.org/packages/Mediator.SourceGenerator)
- LiteBus: [repository](https://github.com/litenova/LiteBus), [differences from MediatR](https://litebus.io/docs/migration/mediatr-differences), [NuGet](https://www.nuget.org/packages/LiteBus)
- Wolverine: [Wolverine as mediator](https://wolverinefx.net/tutorials/mediator.html), [from MediatR](https://wolverinefx.net/introduction/from-mediatr.html), [AOT](https://wolverinefx.io/guide/aot), [NuGet](https://www.nuget.org/packages/WolverineFx)
- FreeMediator: [repository](https://github.com/steffenskov/FreeMediator), [NuGet](https://www.nuget.org/packages/FreeMediator)

---

AnyMediator is an independent project. It is not affiliated with or endorsed by MediatR, Jimmy Bogard, Lucky Penny Software or the other projects on this page.
