---
description: "AnyMediator performance compared with MediatR 12.5 and Mediator 3.0: time and memory per Send and Publish, measured with BenchmarkDotNet on .NET 10."
---

# Performance

AnyMediator dispatches without reflection on the hot path: the first `Send` of a request type builds a small generic wrapper that calls the handler and the behaviors through their interfaces, and every later call reuses it.

## Compared with MediatR and Mediator

One request handler or one notification handler, no behaviors, each library with its default registration. Lower is better.

| Library | `Send` | Allocated | `Publish` | Allocated |
|---------|-------:|----------:|----------:|----------:|
| MediatR 12.5.0 | 92 ns | 200 B | 145 ns | 288 B |
| **AnyMediator** | **86 ns** | **96 B** | **90 ns** | **80 B** |
| Mediator 3.0.2 (source generated) | 11 ns | 0 B | 20 ns | 0 B |

`Send` is on par with MediatR and allocates half as much. `Publish` takes about 40% less time and allocates a quarter as much: with the default sequential strategy, handlers are awaited directly, with no delegate per handler, even though every notification goes through the [notification sink](./notification-sinks) layer.

Mediator is in another class: a source generator writes the dispatch code at compile time and its default lifetime is singleton. Choose it when nanoseconds per message matter more than a MediatR-compatible API; for a request that touches a database or the network, the difference between these numbers is not measurable.

## With pipeline behaviors

Each behavior adds 40 to 65 ns and one delegate:

| Scenario | Time | Allocated |
|----------|-----:|----------:|
| `Send`, no behaviors | 95 ns | 96 B |
| `Send`, 1 behavior | 159 ns | 360 B |
| `Send`, 3 behaviors | 241 ns | 632 B |
| `Publish`, 1 handler | 88 ns | 80 B |
| `Publish`, 3 handlers | 151 ns | 144 B |

## How it was measured

BenchmarkDotNet 0.14 on .NET 10.0.14, AMD Ryzen 9 8940HX, Linux. The benchmarks are in [`tests/AnyMediator.PerformanceTests`](https://github.com/Adiakys/AnyMediator/tree/main/tests/AnyMediator.PerformanceTests); run them with:

```bash
dotnet run -c Release --project tests/AnyMediator.PerformanceTests -- --filter '*'
```
