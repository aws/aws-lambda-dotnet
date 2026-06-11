# Child Contexts

`RunInChildContextAsync` runs a sub-workflow inside its own deterministic operation-ID space. The child's return value is checkpointed as a single `CONTEXT` operation, so subsequent invocations replay the cached value without re-executing the contained operations. Use to group related steps under a shared error/observability boundary.

## Signatures

```csharp
Task<T> RunInChildContextAsync<T>(
    Func<IDurableContext, CancellationToken, Task<T>> func,
    string? name = null,
    ChildContextConfig? config = null,
    CancellationToken cancellationToken = default);

Task RunInChildContextAsync(
    Func<IDurableContext, CancellationToken, Task> func,
    string? name = null,
    ChildContextConfig? config = null,
    CancellationToken cancellationToken = default);
```

The `CancellationToken` parameter is a linked token combining the caller-supplied token with the SDK's workflow-shutdown signal — forward it to `StepAsync` and other operations inside the child so cancellation propagates uniformly.

## Example

```csharp
var phaseResult = await ctx.RunInChildContextAsync<string>(
    async (childCtx, ct) =>
    {
        var validated = await childCtx.StepAsync(async (_, c) => Validate(input, c), name: "validate", cancellationToken: ct);
        await childCtx.WaitAsync(TimeSpan.FromSeconds(2), name: "short_wait", cancellationToken: ct);
        var processed = await childCtx.StepAsync(async (_, c) => Process(validated, c), name: "process", cancellationToken: ct);
        return processed;
    },
    name: "phase",
    config: new ChildContextConfig { SubType = "OrderProcessing" });
```

## Configuration

```csharp
public sealed class ChildContextConfig
{
    public string? SubType { get; set; }                         // observability label
    public Func<Exception, Exception>? ErrorMapping { get; set; } // remap thrown exceptions
}
```

`ErrorMapping` lets you translate exceptions thrown inside the child context into a domain-specific exception type before they propagate to the parent.
