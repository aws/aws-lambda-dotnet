# Instrumentation plugins

A plugin observes a durable execution and can run your workflow code inside its own context. Use one to emit traces, metrics or audit records. The SDK knows nothing about what a plugin does with what it sees.

## Register a plugin

A plugin is registered through a factory. The SDK asks the factory for a new plugin at the start of every invocation, so a plugin can keep per-invocation state in ordinary fields, and concurrent invocations on Lambda Managed Instances never share one.

```csharp
public sealed class AuditPlugin : IDurableInstrumentationPlugin
{
    public void OnOperationEnd(OperationEndInfo info) =>
        Console.WriteLine($"{info.Name}: {info.Status}");
}

public sealed class AuditPluginFactory : IDurableInstrumentationPluginFactory
{
    public IDurableInstrumentationPlugin? CreatePlugin(InvocationInfo info) => new AuditPlugin();
}

// Create the options once, for example in a static field, and reuse them.
static readonly DurableExecutionOptions Options = new()
{
    Plugins = { new AuditPluginFactory() },
};

public Task<DurableExecutionInvocationOutput> Handler(DurableExecutionInvocationInput input, ILambdaContext context)
    => DurableFunction.WrapAsync<Order, OrderResult>(Workflow, input, context, Options);
```

The factory holds what outlives an invocation, such as an exporter. `CreatePlugin` may return null to skip an invocation. Always return a new plugin; do not cache one.

A factory can set `ExclusiveGroup`. Two registered factories with the same group throw `PluginLoadException` before any plugin is created, which is how a feature that must run only once (for example one tracing view) is enforced.

## Hooks

Every member of `IDurableInstrumentationPlugin` is optional.

| Hook | When |
|---|---|
| `OnInvocationStart` | Once, before the workflow runs. `InvocationInfo` carries the request id, whether this is the first invocation, `XRayTraceId` (from `ILambdaContext.TraceId`), `TraceParent` (the W3C `traceparent` from `ILambdaContext.W3C`, if the invoke carried one), and the operations known so far and those the service reports as updated. |
| `OnInvocationEndAsync` | Last. Status is `Succeeded`, `Failed`, `Pending` (suspended), or `Retrying` (the invocation threw and Lambda will retry it). This is where to flush. |
| `OnOperationStart` / `OnOperationEnd` | When the workflow reaches an operation, and when it reaches a terminal state. At start `IsReplay` is true when the operation already has a checkpoint (it finished, or an earlier invocation started it, as with a step resumed after a retry). At end it is true when the operation had already finished. An operation that suspends the invocation, or is cancelled, reports no end. A callback ends when its result is read. |
| `OnAttemptStart` / `OnAttemptEnd` | Around each execution of a step's function (and each wait-for-condition poll). The attempt number is 1-based. Cancellation is not an outcome. |
| `OnOperationChange` | A checkpoint response added operations or changed their status, for example a callback completed. |
| `WrapAsync` | Runs the invocation, a child context, or one attempt inside the plugin's own context. Use it to make an ambient value, such as `Activity.Current`, visible to user code. |
| `ProvidePropagationMetadata` | Before a new chained invoke is checkpointed. Return an `X-Amzn-Trace-Id` value for the invoked function, or null. Not called when the invoke is replayed. |
| `GetLogScope` | For every durable log entry. The returned fields are added to the entry's logging scope. |

## Guarantees

- **A plugin cannot fail the workflow.** An exception from a hook is logged and ignored.
- **A plugin cannot slow an operation.** The operation and attempt hooks are synchronous and the SDK never waits for work a plugin starts from them. A plugin that needs to finish something before the response returns does it in `OnInvocationEndAsync`.
- **`OnInvocationEndAsync` has a budget.** It is cancelled and abandoned shortly before the invocation's remaining time runs out. With several plugins they share that budget and run in reverse registration order.
- **`WrapAsync` always runs your code, once.** If a plugin throws before calling `next`, or never calls it, the code runs anyway. The exception from your code always reaches the caller unchanged, even if a plugin swallows it. The first registered plugin is the outermost.
- **Suspension is not unwinding.** When an invocation suspends, user code still running is abandoned and never completes, so code after `await next()` (including `finally` blocks) does not run for it. Do not rely on it to release anything.
- **Order.** Start hooks run in registration order. For nested operations the order is parent start, child start, child end, parent end. The order in which asynchronous work finishes is not defined.

## What the service reports

Verified against real executions:

- `ILambdaContext.TraceId` is the same for every invocation of an execution, and the same as the `_X_AMZN_TRACE_ID` environment variable. It is per invocation, so use it, not the variable, on Lambda Managed Instances.
- `StepDetails.Attempt` counts finished attempts. A finished step already holds its last attempt number; a step that is ready to retry is on the attempt after it. `OperationInfo.Attempt` is the latest attempt number either way.
- Operations that finished in a previous invocation carry the service's start and end timestamps. Operations that start in the current invocation have no start timestamp until the service reports one, so a plugin reads its own clock when a timestamp is missing.
- The header returned from `ProvidePropagationMetadata` is sent to the invoked function as its trace header.

## Testing a plugin

Pass the options to the local test runner:

```csharp
await using var runner = new DurableTestRunner<string, string>(
    Workflow, new TestRunnerOptions { ExecutionOptions = Options });
```

The in-memory service does not report which operations were updated, so `InvocationInfo.UpdatedOperations` is empty there.
