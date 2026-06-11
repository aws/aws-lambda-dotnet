# Wait

`WaitAsync` suspends the workflow for a duration. The Lambda terminates and is re-invoked when the timer fires — you pay for compute time only on the resume side.

## Signature

```csharp
Task WaitAsync(
    TimeSpan duration,
    string? name = null,
    CancellationToken cancellationToken = default);
```

`duration` must be at least 1 second and at most 31,622,400 seconds (~1 year).

## Example

```csharp
await ctx.WaitAsync(TimeSpan.FromHours(2), name: "warehouse-processing");
```

## Step + Wait + Step

```csharp
var validated = await ctx.StepAsync(async _ => Validate(input), name: "validate");
await ctx.WaitAsync(TimeSpan.FromSeconds(3), name: "short_wait");
var processed = await ctx.StepAsync(async _ => Process(validated), name: "process");
```
