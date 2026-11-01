---
description: "Stream results from a handler as IAsyncEnumerable with IStreamRequest and ISender.CreateStream in AnyMediator."
---

# Stream requests

A stream request returns its results one at a time as an `IAsyncEnumerable<T>`: rows from a paged query, lines of a log, tokens from a language model.

```csharp
public sealed record TailLog(string Source) : IStreamRequest<string>;

public sealed class TailLogHandler(ILogReader reader) : IStreamRequestHandler<TailLog, string>
{
    public async IAsyncEnumerable<string> Handle(
        TailLog request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var line in reader.ReadAsync(request.Source, cancellationToken))
            yield return line;
    }
}
```

Consume it with `CreateStream`:

```csharp
await foreach (var line in sender.CreateStream(new TailLog("api"), cancellationToken))
    Console.WriteLine(line);
```

`CreateStream(object)` accepts a request whose type is only known at runtime and yields the items boxed as `object?`. The object must implement `IStreamRequest<T>`, otherwise an `ArgumentException` is thrown.

## Stream behaviors

Stream requests have their own pipeline, separate from the request pipeline:

```csharp
public sealed class StreamLogging<TRequest, TResponse>(ILogger<StreamLogging<TRequest, TResponse>> logger)
    : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var count = 0;
        await foreach (var item in next(cancellationToken).WithCancellation(cancellationToken))
        {
            count++;
            yield return item;
        }
        logger.LogInformation("{Request} streamed {Count} items", typeof(TRequest).Name, count);
    }
}
```

```csharp
cfg.AddOpenStreamBehavior(typeof(StreamLogging<,>));
```

Like request behaviors, stream behaviors run in registration order: the first registered is the outermost.
