# AWS Lambda Durable Execution Testing for .NET

> **Preview.** `Amazon.Lambda.DurableExecution.Testing` tracks the `Amazon.Lambda.DurableExecution` runtime package (0.x). Public APIs may change before 1.0.

`Amazon.Lambda.DurableExecution.Testing` lets you test [durable workflows](../Amazon.Lambda.DurableExecution/README.md) without deploying to AWS. It drives your workflow handler to a terminal state in-process using the real durable runtime engine backed by an in-memory store, and then exposes the result and every recorded operation for assertions.

You write a test against the `IDurableTestRunner<TInput, TOutput>` interface; the same test runs unchanged against the in-memory `DurableTestRunner` (fast, no AWS) or the `CloudDurableTestRunner` (a deployed function).

## Key Features

- **No deployment** — exercise steps, waits, callbacks, child contexts, parallel branches, and sibling invokes in a unit test.
- **Time-skipping** — day-long `WaitAsync` / `WaitForConditionAsync` waits complete in milliseconds by default, so the whole workflow runs instantly.
- **Step-level inspection** — assert on each operation's status, attempt count, result, error, timing, and parent/child structure.
- **Callback control** — start a workflow, wait for it to suspend on a callback, then send success / failure / heartbeat from the test.
- **Sibling functions** — register plain or durable Lambda handlers so `ctx.InvokeAsync` resolves in-process.
- **Portable tests** — code to `IDurableTestRunner` and switch between the local and cloud backends.

## Installation

```bash
dotnet add package Amazon.Lambda.DurableExecution.Testing
```

Reference it from your test project only — it depends on `Amazon.Lambda.TestUtilities`.

## Quick Start

Construct a `DurableTestRunner<TInput, TOutput>` with your workflow handler, call `RunAsync`, and assert on the `TestResult`:

```csharp
using Amazon.Lambda.DurableExecution.Testing;
using Xunit;

public class OrderWorkflowTests
{
    [Fact]
    public async Task MultiStepWorkflow_Succeeds()
    {
        await using var runner = new DurableTestRunner<int, int>(
            handler: async (input, ctx) =>
            {
                var doubled = await ctx.StepAsync(async (_, _) => input * 2, name: "double");
                var result  = await ctx.StepAsync(async (_, _) => doubled + 10, name: "add_ten");
                return result;
            });

        var result = await runner.RunAsync(5);

        result.EnsureSucceeded();
        Assert.Equal(20, result.Result);

        // Inspect individual steps
        var doubleStep = result.GetStep("double");
        Assert.Equal(OperationKind.Step, doubleStep.Kind);
        Assert.Equal(OperationStatus.Succeeded, doubleStep.Status);
        Assert.Equal(10, doubleStep.GetResult<int>());
    }
}
```

`RunAsync` drives the workflow to a terminal state and resolves any registered sibling functions automatically. It throws `InvalidOperationException` if the workflow suspends on a callback — use the [callback pattern](#testing-callbacks) for those workflows.

## The `TestResult`

`RunAsync` (and `WaitForResultAsync`) return a `TestResult<TOutput>`:

| Member | Description |
|---|---|
| `Status` | `InvocationStatus.Succeeded`, `Failed`, or `Pending`. |
| `IsSucceeded` / `IsFailed` | Status shortcuts. |
| `Result` | The typed workflow output when succeeded. |
| `Error` | The `ErrorObject` when failed. |
| `DurableExecutionArn` | The execution ARN for the run. |
| `InvocationCount` | Handler invocations used to drive the workflow (local only; `null` on the cloud runner). |
| `Steps` | Every recorded operation except the top-level `EXECUTION` op. |
| `EnsureSucceeded()` | Throws `TestExecutionFailedException` if not succeeded. |

Query steps with `GetStep(name)` / `FindStep(name)` (returns null if absent), `GetSteps(name)` (all matches, e.g. parallel branches or map items), `GetStepById(id)`, `GetStepsByStatus(status)`, and `GetChildren(step)`.

Each `TestStep` exposes `Id`, `Name`, `ParentId`, `Kind` (`Step`, `Wait`, `Callback`, `ChainedInvoke`, `Context`, `Execution`), `SubKind`, `Status`, `Attempt`, `StartedAt` / `EndedAt` / `Duration`, and `Children`. Use `GetResult<T>()` and `GetError()` to read an operation's typed result or error.

Asserting on a failed workflow:

```csharp
var result = await runner.RunAsync("test");

Assert.Equal(InvocationStatus.Failed, result.Status);
Assert.Equal("System.InvalidOperationException", result.Error!.ErrorType);
```

## Waits and Time-Skipping

By default `TestRunnerOptions.SkipTime` is `true`, so `WaitAsync` timers and `WaitForConditionAsync` backoffs complete immediately — a workflow with a 30-day wait runs in milliseconds. The wait is still recorded as a step you can assert on:

```csharp
await using var runner = new DurableTestRunner<string, string>(
    handler: async (input, ctx) =>
    {
        await ctx.StepAsync(async (_, _) => "done", name: "before");
        await ctx.WaitAsync(TimeSpan.FromDays(30), name: "long_wait");
        return await ctx.StepAsync(async (_, _) => "completed", name: "after");
    });

var result = await runner.RunAsync("go");
result.EnsureSucceeded();

var wait = result.GetStep("long_wait");
Assert.Equal(OperationKind.Wait, wait.Kind);
```

Set `SkipTime = false` to assert on real wait durations.

## Testing Callbacks

For workflows that suspend on a callback (approvals, webhooks, external signals), use the two-call pattern: `StartAsync` runs the workflow until it suspends, `WaitForCallbackAsync` returns the pending callback id, you send a result, then `WaitForResultAsync` drives it to completion.

```csharp
await using var runner = new DurableTestRunner<string, string>(
    handler: async (input, ctx) =>
    {
        var approval = await ctx.WaitForCallbackAsync<string>(
            async (callbackId, cbCtx, ct) => { /* submit to external system */ },
            name: "approval");
        return $"approved: {approval}";
    });

var arn        = await runner.StartAsync("request-1");
var callbackId = await runner.WaitForCallbackAsync(arn, name: "approval");

await runner.SendCallbackSuccessAsync(callbackId, "yes");
var result = await runner.WaitForResultAsync(arn);

result.EnsureSucceeded();
Assert.Equal("approved: yes", result.Result);
```

`SendCallbackFailureAsync(callbackId, error)` delivers a failure (surfaced in the workflow as `CallbackException`), and `SendCallbackHeartbeatAsync(callbackId)` keeps a callback alive (a no-op locally).

## Sibling Functions

When your workflow calls `ctx.InvokeAsync` to invoke another Lambda function, register that function on the runner so it resolves in-process. Functions can be registered by short name or full ARN (resolution matches on the short name).

```csharp
await using var runner = new DurableTestRunner<int, string>(
    handler: async (input, ctx) =>
    {
        var payment = await ctx.InvokeAsync<PaymentRequest, PaymentResult>(
            "process-payment", new PaymentRequest { Amount = input }, name: "charge");
        return $"charged: {payment.Status}";
    });

// A plain (non-durable) Lambda handler
runner.RegisterFunction<PaymentRequest, PaymentResult>(
    "process-payment",
    (req, _) => Task.FromResult(new PaymentResult { Status = $"approved-{req.Amount}" }));

// A durable sibling runs in its own nested runner with its own steps
runner.RegisterDurableFunction<PaymentRequest, PaymentResult>(
    "audit",
    async (req, childCtx) => await childCtx.StepAsync(/* ... */));

var result = await runner.RunAsync(100);
result.EnsureSucceeded();

var invoke = result.GetStep("charge");
Assert.Equal(OperationKind.ChainedInvoke, invoke.Kind);
```

Invoking an unregistered function throws `UnregisteredSiblingFunctionException`. A sibling that throws is recorded as a failed `ChainedInvoke` step and surfaces in the workflow as `InvokeException`.

## Configuration

`TestRunnerOptions` (local runner):

| Option | Default | Description |
|---|---|---|
| `SkipTime` | `true` | Complete waits and retry backoffs immediately. |
| `MaxInvocations` | `100` | Handler invocation cap before `TestExecutionLimitException`. |
| `DefaultTimeout` | `30s` | Wall-clock timeout for a single `RunAsync` / `WaitForResultAsync`. |
| `Serializer` | `DefaultLambdaJsonSerializer` | `ILambdaSerializer` for payloads and results. |
| `LoggerFactory` | none | Optional logging during execution. |
| `DurableExecutionArn` | synthetic test ARN | Override for tests that assert on the ARN. |

## Testing Against a Deployed Function

`CloudDurableTestRunner<TInput, TOutput>` implements the same `IDurableTestRunner` interface against a real deployed durable function: it invokes the function, reads the durable execution ARN from the response, polls `GetDurableExecution` until terminal, and reconstructs the recorded operations from `GetDurableExecutionHistory` (the token-free, externally-pollable history API). Tests written against the interface run unchanged on either backend.

```csharp
await using var runner = new CloudDurableTestRunner<Order, OrderResult>(
    functionArn: "arn:aws:lambda:us-east-1:123456789012:function:order-processor:live");

var result = await runner.RunAsync(new Order(/* ... */));
result.EnsureSucceeded();
```

`CloudTestRunnerOptions` configures `InitialPollInterval` (default 200ms), `PollInterval` (steady-state cap, default 2s), `DefaultTimeout` (default 5m), and `Serializer`. Polling uses exponential backoff from `InitialPollInterval` up to `PollInterval` so the eventual-consistency window right after an Event invoke is probed quickly without hammering the service. The cloud runner maps the service's `FAILED`, `TIMED_OUT`, and `STOPPED` terminal states onto `InvocationStatus.Failed` (inspect `Error` for detail) and does not track `InvocationCount` (it returns `null`) — do not assert on `InvocationCount` in tests intended to run against both backends.

## Related

- [Amazon.Lambda.DurableExecution](../Amazon.Lambda.DurableExecution/README.md) — the durable execution runtime SDK this package tests.
