# .NET Lambda Durable Execution SDK Design

## Table of Contents

- [Overview](#overview)
- [Motivation](#motivation)
- [How Durable Execution Works](#how-durable-execution-works)
- [User Experience](#user-experience)
  - [Quick Start](#quick-start)
  - [Steps](#steps)
  - [Wait Operations](#wait-operations)
  - [Callbacks](#callbacks)
  - [Invoke (Chained Functions)](#invoke-chained-functions)
  - [Parallel Execution](#parallel-execution)
  - [Map Operations](#map-operations)
  - [Child Contexts](#child-contexts)
  - [Error Handling & Retry](#error-handling--retry)
  - [Logging](#logging)
- [Internals](#internals)
- [API Reference](#api-reference)
  - [IDurableContext](#idurablecontext)
  - [Configuration Types](#configuration-types)
  - [Result Types](#result-types)
  - [Exception Types](#exception-types)
- [Serialization](#serialization)
- [Integration with Existing Libraries](#integration-with-existing-libraries)
- [Testing](#testing)
- [Local development (Test Tool v2 and Aspire)](#local-development-test-tool-v2-and-aspire)
- [Requirements & Constraints](#requirements--constraints)
- [Package Structure](#package-structure)
- [Implementation plan](#implementation-plan)
- [Cross-SDK API comparison](#cross-sdk-api-comparison)
- [Common Patterns](#common-patterns)

---

## Overview

Lambda Durable Functions let you write multi-step workflows that persist state automatically. They can run for days or months, survive failures, and you only pay for actual compute time.

This doc covers the **.NET Durable Execution SDK** (`Amazon.Lambda.DurableExecution`). SDKs already exist for [Python](https://github.com/aws/aws-durable-execution-sdk-python) and [JavaScript/TypeScript](https://github.com/aws/aws-durable-execution-sdk-js).

Related: [GitHub Issue #2216](https://github.com/aws/aws-lambda-dotnet/issues/2216)

---

## Motivation

### The problem

Today, building multi-step Lambda workflows in .NET requires one of:

1. **Step Functions** -- a separate service with its own state machine language (ASL), adding latency between steps and forcing you to learn a second programming model.
2. **Manual state management** -- rolling your own checkpointing with DynamoDB or S3, plus retry logic, idempotency keys, and resumption code.
3. **Event-driven choreography** -- chaining functions through SQS/SNS/EventBridge, scattering a single workflow's logic across half a dozen Lambda functions.

All three push infrastructure concerns into your business logic. The code gets harder to read and test, and nobody wants to inherit it.

### What durable functions do instead

With this SDK, you write sequential code and the runtime handles persistence:
- Checkpoints each step's result
- Suspends when waiting (no compute charges while idle)
- Resumes from the last checkpoint on the next invocation
- Retries failed steps with configurable backoff
- Waits for callbacks from external systems

Your function reads like a normal async method. The SDK deals with state, replay, and recovery.

### Why build a .NET SDK

.NET has a large Lambda user base, especially in enterprise shops running order processing, document pipelines, and (increasingly) AI agent workflows. Today those teams either use Step Functions or build custom state machines. A native .NET SDK removes that tradeoff.

---

## How Durable Execution Works

### The replay model

Durable functions use a replay-based execution model. Every invocation runs your code from the top, but previously completed steps return their cached result instead of re-executing.

1. Lambda invokes your function with a `DurableExecutionInvocationInput` containing:
   - `DurableExecutionArn` -- unique execution identifier
   - `CheckpointToken` -- for optimistic concurrency
   - `InitialExecutionState` -- previously checkpointed operations

2. Your function code runs **from the beginning** on every invocation.

3. When a **step** is encountered:
   - Previously completed → return cached result (no re-execution)
   - New → execute it, checkpoint the result, continue

4. When a **wait** is encountered:
   - Already elapsed → continue
   - Still pending → return `PENDING`, Lambda terminates, service re-invokes later

5. The function returns one of:
   - `SUCCEEDED` -- workflow completed
   - `FAILED` -- workflow failed
   - `PENDING` -- workflow suspended (waiting for time or callback)

```
┌─────────────────────────────────────────────────────────────────┐
│ First Invocation (t=0s)                                         │
│                                                                 │
│  handler(event, context)                                        │
│    │                                                            │
│    ├─► context.StepAsync(FetchData) → executes, checkpoints     │
│    │                                                            │
│    ├─► context.WaitAsync(30 seconds) → returns PENDING          │
│    │                                                            │
│    └── (Lambda terminates, environment recyclable)              │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ Second Invocation (t=30s)                                       │
│                                                                 │
│  handler(event, context)                                        │
│    │                                                            │
│    ├─► context.StepAsync(FetchData) → returns cached result     │
│    │                                                            │
│    ├─► context.WaitAsync(30 seconds) → already elapsed, skip    │
│    │                                                            │
│    ├─► context.StepAsync(ProcessData) → executes, checkpoints   │
│    │                                                            │
│    └── return result → SUCCEEDED                                │
└─────────────────────────────────────────────────────────────────┘
```

---

## User Experience

### Quick Start

#### Installation

```shell
dotnet add package Amazon.Lambda.DurableExecution
```

#### Minimal Example

```csharp
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MyDurableFunction;

public class Function
{
    [DurableExecution]
    public async Task<OrderResult> Handler(OrderEvent input, IDurableContext context)
    {
        // Step 1: Validate the order (checkpointed automatically)
        var validation = await context.StepAsync(
            async () => await ValidateOrder(input.OrderId),
            name: "validate_order");

        if (!validation.IsValid)
            return new OrderResult { Status = "rejected" };

        // Step 2: Wait for processing (Lambda is NOT running during this time)
        await context.WaitAsync(Duration.FromSeconds(30), name: "processing_delay");

        // Step 3: Process the order
        var result = await context.StepAsync(
            async () => await ProcessOrder(input.OrderId),
            name: "process_order");

        return new OrderResult { Status = "approved", OrderId = result.OrderId };
    }

    private async Task<ValidationResult> ValidateOrder(string orderId) { /* ... */ }
    private async Task<ProcessResult> ProcessOrder(string orderId) { /* ... */ }
}
```

Things to notice:
- `[DurableExecution]` triggers source generation, so you don't wire up the handler yourself
- Each `StepAsync` call checkpoints its result automatically
- `WaitAsync` suspends the function -- Lambda is not running (or billing you) during the wait
- On replay, completed steps return their cached result without re-executing
- The generated wrapper handles checkpoint batching and cleanup

---

### Steps

A step runs your code and checkpoints the result. On replay, the cached result comes back without re-executing.

```csharp
// Basic step with automatic naming
var result = await context.StepAsync(async () => await CallExternalApi());

// Named step (recommended for debugging/testing)
var user = await context.StepAsync(
    async () => await FetchUser(userId),
    name: "fetch_user");

// Step returning a complex type
var order = await context.StepAsync<Order>(
    async () => await orderService.GetOrder(orderId),
    name: "get_order");

// Step with configuration
var payment = await context.StepAsync(
    async () => await chargeCard(amount),
    name: "charge_card",
    config: new StepConfig
    {
        Semantics = StepSemantics.AtMostOncePerRetry, // Prevent duplicate charges
        RetryStrategy = RetryStrategy.Exponential(maxAttempts: 3, initialDelay: TimeSpan.FromSeconds(1))
    });
```

#### Step Semantics

| Semantics | Behavior | Use Case |
|-----------|----------|----------|
| `AtLeastOncePerRetry` (default) | Step re-executes on each retry | Idempotent operations (calculations, reads) |
| `AtMostOncePerRetry` | Step executes at most once per retry | Side effects (payments, emails, writes) |

---

### Wait Operations

Waits suspend the function without consuming compute time. Lambda can recycle the execution environment.

```csharp
// Wait for a specific duration
await context.WaitAsync(Duration.FromSeconds(30));
await context.WaitAsync(Duration.FromMinutes(5), name: "cooldown");
await context.WaitAsync(Duration.FromHours(24), name: "daily_check");
await context.WaitAsync(Duration.FromDays(7), name: "weekly_reminder");
```

**Duration API:**

```csharp
public sealed class Duration
{
    public static Duration FromSeconds(int seconds);
    public static Duration FromMinutes(int minutes);
    public static Duration FromHours(int hours);
    public static Duration FromDays(int days);
    public static Duration FromTimeSpan(TimeSpan timeSpan);
}
```

---

### Callbacks

Callbacks let your workflow pause until an external system responds (human approval, a webhook, a third-party API).

#### Create a Callback (Advanced)

```csharp
// Create a callback and get the callback ID
var callback = await context.CreateCallbackAsync<ApprovalResult>(
    name: "approval_callback",
    config: new CallbackConfig
    {
        Timeout = Duration.FromHours(24),
        HeartbeatTimeout = Duration.FromHours(2)
    });

// Send the callback ID to an external system
await context.StepAsync(
    async () => await SendApprovalEmail(callback.CallbackId, recipientEmail),
    name: "send_approval_email");

// Wait for the external system to respond
var result = await callback.GetResultAsync();
```

#### Wait For Callback (Simple)

```csharp
// Combined pattern: create callback, submit to external system, wait for result
var approval = await context.WaitForCallbackAsync<ApprovalResult>(
    async (callbackId, ctx) =>
    {
        await SendApprovalEmail(callbackId, managerEmail);
    },
    name: "wait_for_approval",
    config: new WaitForCallbackConfig
    {
        Timeout = Duration.FromHours(24),
        RetryStrategy = RetryStrategy.Exponential(maxAttempts: 3)
    });

if (approval.Approved)
{
    await context.StepAsync(async () => await ExecutePlan(), name: "execute");
}
```

**External system completes the callback via AWS API:**
```bash
aws lambda send-durable-execution-callback-success \
  --function-name my-function:1 \
  --callback-id "cb-12345" \
  --payload '{"approved": true, "approver": "jane@example.com"}'
```

---

### Invoke (Chained Functions)

Call another durable function. The invocation is checkpointed, so it survives failures and won't double-fire.

```csharp
// Invoke another durable function
var paymentResult = await context.InvokeAsync<PaymentRequest, PaymentResult>(
    functionName: "arn:aws:lambda:us-east-1:123456789012:function:payment-processor:prod",
    payload: new PaymentRequest { Amount = 100, Currency = "USD" },
    name: "process_payment",
    config: new InvokeConfig
    {
        Timeout = Duration.FromMinutes(5)
    });
```

> **Note:** Durable function invocations require **qualified identifiers** (version number, alias, or `$LATEST`).

---

### Parallel Execution

Run independent operations concurrently. The JS SDK uses a `DurablePromise` pattern where operations are deferred until awaited; in .NET that isn't necessary because `ParallelAsync` and `MapAsync` cover the same use case idiomatically. `Task`-returning methods start immediately and `await` retrieves the result, so there's no gap to fill with a lazy wrapper.

```csharp
// Run multiple operations in parallel
var results = await context.ParallelAsync(
    new Func<IDurableContext, Task<object>>[]
    {
        async (ctx) => await ctx.StepAsync(async () => await FetchUserData(userId), name: "fetch_user"),
        async (ctx) => await ctx.StepAsync(async () => await FetchOrderHistory(userId), name: "fetch_orders"),
        async (ctx) => await ctx.StepAsync(async () => await FetchPreferences(userId), name: "fetch_prefs"),
    },
    name: "parallel_fetch",
    config: new ParallelConfig
    {
        MaxConcurrency = 3,
        CompletionConfig = CompletionConfig.AllSuccessful()
    });

// Access individual results
var userData = results.GetResults()[0];
var orderHistory = results.GetResults()[1];
var preferences = results.GetResults()[2];
```

#### Named Parallel Branches

For better observability, you can name individual branches (matching the JS SDK pattern):

```csharp
// Named branches for easier debugging and testing
var results = await context.ParallelAsync(
    new NamedBranch<object>[]
    {
        new("fetch_user", async (ctx) => await ctx.StepAsync(async () => await FetchUserData(userId))),
        new("fetch_orders", async (ctx) => await ctx.StepAsync(async () => await FetchOrderHistory(userId))),
        new("fetch_prefs", async (ctx) => await ctx.StepAsync(async () => await FetchPreferences(userId))),
    },
    name: "parallel_fetch");

// In tests, you can find specific branches by name
var fetchUserBranch = result.GetOperation("fetch_user");
```

#### Completion Configurations

```csharp
// All must succeed (default)
CompletionConfig.AllSuccessful()

// Complete when any one succeeds
CompletionConfig.FirstSuccessful()

// Complete when all finish (regardless of success/failure)
CompletionConfig.AllCompleted()

// Custom: succeed if at least 3 succeed, tolerate up to 2 failures
new CompletionConfig
{
    MinSuccessful = 3,
    ToleratedFailureCount = 2
}
```

---

### Map Operations

Process a collection in parallel with configurable concurrency.

```csharp
var orders = new[] { "order-1", "order-2", "order-3", "order-4", "order-5" };

var results = await context.MapAsync(
    items: orders,
    func: async (ctx, orderId, index, allItems) =>
    {
        return await ctx.StepAsync(
            async () => await ProcessOrder(orderId),
            name: $"process_order_{index}");
    },
    name: "process_all_orders",
    config: new MapConfig
    {
        MaxConcurrency = 3,
        CompletionConfig = CompletionConfig.AllSuccessful(),
        ItemNamer = (orderId, index) => $"Order-{orderId}"  // Readable names for observability
    });

// Check results
results.ThrowIfError(); // Throws if any item failed
var processedOrders = results.GetResults();
```

---

### Child Contexts

Child contexts group related durable operations into a sub-workflow. Use them when you need waits or multiple steps inside a logical unit (you cannot nest durable calls inside a step directly).

```csharp
// Group operations into a child context
var enrichedData = await context.RunInChildContextAsync(
    async (childCtx) =>
    {
        var validated = await childCtx.StepAsync(
            async () => await Validate(data),
            name: "validate");

        await childCtx.WaitAsync(Duration.FromSeconds(1), name: "rate_limit");

        var enriched = await childCtx.StepAsync(
            async () => await Enrich(validated),
            name: "enrich");

        return enriched;
    },
    name: "validation_phase");
```

> **Why child contexts?** You cannot nest durable operations inside a step. Steps are leaf operations. If you need multiple durable operations grouped together, use a child context.

---

### Error Handling & Retry

#### Retry Strategies

```csharp
// Exponential backoff
var result = await context.StepAsync(
    async () => await CallUnreliableApi(),
    name: "api_call",
    config: new StepConfig
    {
        RetryStrategy = RetryStrategy.Exponential(
            maxAttempts: 5,
            initialDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromSeconds(60),
            backoffRate: 2.0)
    });

// Custom retry strategy
var result = await context.StepAsync(
    async () => await CallApi(),
    name: "api_call",
    config: new StepConfig
    {
        RetryStrategy = new CustomRetryStrategy((exception, attemptCount) =>
        {
            // Only retry transient errors
            if (exception is HttpRequestException httpEx && httpEx.StatusCode >= 500)
                return RetryDecision.RetryAfter(TimeSpan.FromSeconds(Math.Pow(2, attemptCount)));

            return RetryDecision.DoNotRetry();
        })
    });

// Retry with specific exception types
var result = await context.StepAsync(
    async () => await CallApi(),
    name: "api_call",
    config: new StepConfig
    {
        RetryStrategy = RetryStrategy.Exponential(
            maxAttempts: 3,
            retryableExceptions: new[] { typeof(TimeoutException), typeof(HttpRequestException) })
    });
```

#### Saga Pattern (Compensating Transactions)

```csharp
[DurableExecution]
public async Task<BookingResult> Handler(BookingRequest input, IDurableContext context)
{
    var compensations = new List<(string Name, Func<Task> Action)>();

    try
    {
        var flight = await context.StepAsync(
            async () => await BookFlight(input),
            name: "book_flight");
        compensations.Add(("cancel_flight", async () => await CancelFlight(flight.Id)));

        var hotel = await context.StepAsync(
            async () => await BookHotel(input),
            name: "book_hotel");
        compensations.Add(("cancel_hotel", async () => await CancelHotel(hotel.Id)));

        var car = await context.StepAsync(
            async () => await BookCar(input),
            name: "book_car");
        compensations.Add(("cancel_car", async () => await CancelCar(car.Id)));

        return new BookingResult { Status = "confirmed" };
    }
    catch (Exception ex)
    {
        // Execute compensations in reverse order
        foreach (var (name, action) in compensations.AsEnumerable().Reverse())
        {
            await context.StepAsync(action, name: name);
        }
        return new BookingResult { Status = "cancelled", Error = ex.Message };
    }
}
```

---

### Logging

`context.Logger` is replay-aware: it suppresses duplicate messages that would otherwise repeat on every invocation. Use it instead of `Console.WriteLine`.

```csharp
[DurableExecution]
public async Task<string> Handler(MyEvent input, IDurableContext context)
{
    // ✅ Replay-safe: only logs once even during replay
    context.Logger.LogInformation("Starting workflow for {OrderId}", input.OrderId);

    var result = await context.StepAsync(
        async () => await ProcessData(input.Data),
        name: "process_data");

    // ✅ Replay-safe
    context.Logger.LogInformation("Processing complete: {Result}", result);

    // ❌ NOT replay-safe: will log on every replay
    Console.WriteLine("This will repeat!");

    return result;
}
```

The logger integrates with `Microsoft.Extensions.Logging`:

```csharp
// context.Logger implements ILogger
context.Logger.LogDebug("Debug info");
context.Logger.LogInformation("Info message");
context.Logger.LogWarning("Warning: {Detail}", detail);
context.Logger.LogError(exception, "Error occurred");
```

#### Custom Logger Configuration

You can swap the logger or disable replay-aware filtering (e.g., to see logs during replay for debugging):

```csharp
// Use a custom logger (e.g., Serilog, AWS Lambda Powertools)
context.ConfigureLogger(new LoggerConfig
{
    CustomLogger = myCustomLogger,
    ModeAware = true  // true = suppress during replay (default), false = always log
});

// Disable replay-aware filtering to see ALL logs (useful for debugging)
context.ConfigureLogger(new LoggerConfig { ModeAware = false });
```

---

## Internals

### AWS APIs used

| API | Purpose |
|-----|---------|
| `CheckpointDurableExecution` | Persist operation state (step results, waits, etc.) |
| `GetDurableExecutionState` | Retrieve previously checkpointed state on replay |
| `SendDurableExecutionCallbackSuccess` | External systems signal callback completion |
| `SendDurableExecutionCallbackFailure` | External systems signal callback failure |
| `SendDurableExecutionCallbackHeartbeat` | External systems send heartbeat signals |

### How suspension works internally

This follows the same pattern as the JavaScript SDK's `Promise.race`. The .NET equivalent is `Task.WhenAny`.

When `RunAsync` starts, it kicks off two tasks in parallel: user code and a termination signal (a `TaskCompletionSource` that starts unresolved). Whoever finishes first wins:

```
┌─────────────────────────────────────────────────────────────────────┐
│  DurableExecutionHandler.RunAsync                                    │
│                                                                      │
│  var userTask = userHandler(context);                                │
│  var terminationTask = terminationManager.TerminationTask;           │
│                                                                      │
│  var winner = await Task.WhenAny(userTask, terminationTask);         │
│                                                                      │
│  ┌─── userTask ───────────────────┐  ┌─── terminationTask ────────┐ │
│  │ StepAsync("fetch") → execute   │  │ (unresolved TCS - waiting) │ │
│  │ WaitAsync("delay") → ...       │  │                            │ │
│  │   calls Terminate() ──────────────► SetResult() → resolves!    │ │
│  │   awaits forever (blocked)     │  │                            │ │
│  └────────────────────────────────┘  └────────────────────────────┘ │
│                                                                      │
│  winner == terminationTask → return PENDING                          │
│  (userTask is abandoned, GC collects it)                             │
└─────────────────────────────────────────────────────────────────────┘
```

The `TerminationManager` is a thin wrapper around `TaskCompletionSource<TerminationResult>`:
- `TerminationTask` -- a Task that hangs forever until `Terminate()` is called
- `Terminate(reason)` -- resolves the TCS, causing the race to pick termination

When user code hits a pending wait or callback:
1. It checkpoints the operation state
2. Calls `terminationManager.Terminate(WaitScheduled)`
3. Awaits a new never-completing `TaskCompletionSource` (blocks itself permanently)
4. `Task.WhenAny` sees the termination task resolved and picks it as the winner
5. `RunAsync` returns PENDING; Lambda terminates; the abandoned user task is GC'd

### Lifecycle and cleanup

`RunAsync` manages the full lifecycle internally. When the handler completes (SUCCEEDED/FAILED) or suspends (PENDING), `RunAsync` stops the background checkpoint batcher, flushes any pending checkpoint operations, and disposes internal state. Users never call `Dispose` or wrap anything in `await using`.

---

## API Reference

### IDurableContext

The primary interface developers interact with:

```csharp
public interface IDurableContext
{
    /// <summary>
    /// Replay-safe logger. Messages are de-duplicated during replay.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Metadata about the current durable execution.
    /// </summary>
    IExecutionContext ExecutionContext { get; }

    /// <summary>
    /// The underlying Lambda context.
    /// </summary>
    ILambdaContext LambdaContext { get; }

    /// <summary>
    /// Execute a step with automatic checkpointing.
    /// </summary>
    Task<T> StepAsync<T>(
        Func<Task<T>> func,
        string? name = null,
        StepConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a step that returns no value.
    /// </summary>
    Task StepAsync(
        Func<Task> func,
        string? name = null,
        StepConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspend execution for the specified duration.
    /// </summary>
    Task WaitAsync(
        Duration duration,
        string? name = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a callback for external system integration.
    /// </summary>
    Task<ICallback<T>> CreateCallbackAsync<T>(
        string? name = null,
        CallbackConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Wait for an external system to respond via callback.
    /// </summary>
    Task<T> WaitForCallbackAsync<T>(
        Func<string, ICallbackContext, Task> submitter,
        string? name = null,
        WaitForCallbackConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoke another durable function.
    /// </summary>
    Task<TResult> InvokeAsync<TPayload, TResult>(
        string functionName,
        TPayload payload,
        string? name = null,
        InvokeConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute multiple operations in parallel.
    /// </summary>
    Task<IBatchResult<T>> ParallelAsync<T>(
        IReadOnlyList<Func<IDurableContext, Task<T>>> functions,
        string? name = null,
        ParallelConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a collection of items in parallel.
    /// </summary>
    Task<IBatchResult<TResult>> MapAsync<TItem, TResult>(
        IReadOnlyList<TItem> items,
        Func<IDurableContext, TItem, int, IReadOnlyList<TItem>, Task<TResult>> func,
        string? name = null,
        MapConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run operations in an isolated child context.
    /// </summary>
    Task<T> RunInChildContextAsync<T>(
        Func<IDurableContext, Task<T>> func,
        string? name = null,
        ChildContextConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Poll until a condition is met.
    /// </summary>
    Task<TState> WaitForConditionAsync<TState>(
        Func<TState, IConditionCheckContext, Task<TState>> check,
        WaitForConditionConfig<TState> config,
        string? name = null,
        CancellationToken cancellationToken = default);
}
```

#### CancellationToken behavior

All methods accept a `CancellationToken` that follows standard .NET semantics: cancellation throws `OperationCanceledException` and the execution fails. Cancellation does **not** trigger suspension — those are separate concepts. The durable execution service handles timeout scenarios automatically: if Lambda terminates mid-execution, the next invocation simply replays from the last checkpoint. For advanced users who want to suspend gracefully before timeout, check `context.LambdaContext.RemainingTime` and return early.

### Configuration Types

```csharp
/// <summary>
/// Configuration for step execution.
/// </summary>
public class StepConfig
{
    /// <summary>
    /// Retry strategy for failed steps. Default is no retry.
    /// </summary>
    public IRetryStrategy? RetryStrategy { get; set; }

    /// <summary>
    /// Execution semantics. Default is AtLeastOncePerRetry.
    /// </summary>
    public StepSemantics Semantics { get; set; } = StepSemantics.AtLeastOncePerRetry;

    /// <summary>
    /// Custom serializer for the step result. Default is System.Text.Json.
    /// </summary>
    public ICheckpointSerializer? Serializer { get; set; }
}

public enum StepSemantics
{
    /// <summary>
    /// Step re-executes on each retry attempt. Safe for idempotent operations.
    /// </summary>
    AtLeastOncePerRetry,

    /// <summary>
    /// Step executes at most once per retry attempt. Use for side effects.
    /// </summary>
    AtMostOncePerRetry
}

/// <summary>
/// Configuration for callback operations.
/// </summary>
public class CallbackConfig
{
    /// <summary>
    /// Maximum time to wait for callback response.
    /// </summary>
    public Duration Timeout { get; set; } = Duration.None;

    /// <summary>
    /// Maximum time between heartbeat signals before timeout.
    /// </summary>
    public Duration HeartbeatTimeout { get; set; } = Duration.None;

    /// <summary>
    /// Custom serializer for callback result.
    /// </summary>
    public ICheckpointSerializer? Serializer { get; set; }
}

/// <summary>
/// Configuration for wait-for-callback operations.
/// </summary>
public class WaitForCallbackConfig : CallbackConfig
{
    /// <summary>
    /// Retry strategy for the submitter function.
    /// </summary>
    public IRetryStrategy? RetryStrategy { get; set; }
}

/// <summary>
/// Configuration for invoke operations.
/// </summary>
public class InvokeConfig
{
    /// <summary>
    /// Maximum time to wait for the invoked function.
    /// </summary>
    public Duration Timeout { get; set; } = Duration.None;

    /// <summary>
    /// Custom serializer for the payload.
    /// </summary>
    public ICheckpointSerializer? PayloadSerializer { get; set; }

    /// <summary>
    /// Custom serializer for the result.
    /// </summary>
    public ICheckpointSerializer? ResultSerializer { get; set; }
}

/// <summary>
/// Configuration for parallel execution.
/// </summary>
public class ParallelConfig
{
    /// <summary>
    /// Maximum concurrent branches. Null = unlimited.
    /// </summary>
    public int? MaxConcurrency { get; set; }

    /// <summary>
    /// When to consider the operation complete.
    /// </summary>
    public CompletionConfig CompletionConfig { get; set; } = CompletionConfig.AllSuccessful();
}

/// <summary>
/// Configuration for map operations.
/// </summary>
public class MapConfig
{
    /// <summary>
    /// Maximum concurrent items. Null = unlimited.
    /// </summary>
    public int? MaxConcurrency { get; set; }

    /// <summary>
    /// When to consider the operation complete.
    /// </summary>
    public CompletionConfig CompletionConfig { get; set; } = CompletionConfig.AllSuccessful();
}

/// <summary>
/// Defines completion criteria for parallel/map operations.
/// </summary>
public class CompletionConfig
{
    public int? MinSuccessful { get; set; }
    public int? ToleratedFailureCount { get; set; }
    public double? ToleratedFailurePercentage { get; set; }

    public static CompletionConfig AllSuccessful() => new() { ToleratedFailureCount = 0 };
    public static CompletionConfig FirstSuccessful() => new() { MinSuccessful = 1 };
    public static CompletionConfig AllCompleted() => new();
}

/// <summary>
/// Configuration for child context operations.
/// </summary>
public class ChildContextConfig
{
    /// <summary>
    /// Custom serializer for the child context's return value.
    /// </summary>
    public ICheckpointSerializer? Serializer { get; set; }

    /// <summary>
    /// Operation sub-type label for observability (e.g., in test runner output).
    /// </summary>
    public string? SubType { get; set; }
}

/// <summary>
/// Configuration for wait-for-condition (polling).
/// </summary>
public class WaitForConditionConfig<TState>
{
    /// <summary>
    /// Initial state passed to the first check invocation.
    /// </summary>
    public required TState InitialState { get; set; }

    /// <summary>
    /// Strategy controlling how long to wait between checks.
    /// </summary>
    public required IWaitStrategy<TState> WaitStrategy { get; set; }
}
```

### Result Types

```csharp
/// <summary>
/// Result of a parallel or map operation.
/// </summary>
public interface IBatchResult<T>
{
    /// <summary>
    /// All items (succeeded and failed).
    /// </summary>
    IReadOnlyList<IBatchItem<T>> All { get; }

    /// <summary>
    /// Only successful items.
    /// </summary>
    IReadOnlyList<IBatchItem<T>> Succeeded { get; }

    /// <summary>
    /// Only failed items.
    /// </summary>
    IReadOnlyList<IBatchItem<T>> Failed { get; }

    /// <summary>
    /// Get all successful results. Throws if any failed.
    /// </summary>
    IReadOnlyList<T> GetResults();

    /// <summary>
    /// Throw an exception if any item failed.
    /// </summary>
    void ThrowIfError();

    /// <summary>
    /// Why the operation completed.
    /// </summary>
    CompletionReason CompletionReason { get; }
}

public interface IBatchItem<T>
{
    int Index { get; }
    BatchItemStatus Status { get; }
    T? Result { get; }
    DurableExecutionException? Error { get; }
}

public enum BatchItemStatus { Succeeded, Failed, Cancelled }
public enum CompletionReason { AllCompleted, MinSuccessfulReached, FailureToleranceExceeded }

/// <summary>
/// Represents a pending callback.
/// </summary>
public interface ICallback<T>
{
    /// <summary>
    /// The callback ID to send to external systems.
    /// </summary>
    string CallbackId { get; }

    /// <summary>
    /// Wait for and return the callback result.
    /// Suspends execution until the result is available.
    /// </summary>
    Task<T?> GetResultAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata about the current execution.
/// </summary>
public interface IExecutionContext
{
    /// <summary>
    /// The ARN of the current durable execution.
    /// </summary>
    string DurableExecutionArn { get; }
}
```

### Exception Types

```csharp
/// <summary>
/// Base exception for all durable execution errors.
/// </summary>
public class DurableExecutionException : Exception { }

/// <summary>
/// Thrown when user code inside a step fails (after retries exhausted).
/// Contains the original error details from the checkpoint.
/// </summary>
public class CallableRuntimeException : DurableExecutionException
{
    public string? ErrorType { get; }
    public string? ErrorData { get; }
    public IReadOnlyList<string>? StackTrace { get; }
}

/// <summary>
/// Thrown when a callback fails or times out.
/// </summary>
public class CallbackException : DurableExecutionException
{
    public string? CallbackId { get; }
}

/// <summary>
/// Thrown when input validation fails.
/// </summary>
public class DurableValidationException : DurableExecutionException { }

/// <summary>
/// Thrown when the checkpoint API call fails.
/// </summary>
public class CheckpointException : DurableExecutionException
{
    public bool IsRetriable { get; }
}
```

---

## Serialization

### Default behavior

Step results are serialized to JSON (via `System.Text.Json`) before checkpointing. Your return types need to be JSON-serializable.

```csharp
// ✅ GOOD: JSON-serializable types
public record OrderResult(string OrderId, decimal Total, bool IsCompleted);

// ❌ BAD: Non-serializable types
public class BadResult
{
    public Stream DataStream { get; set; }      // Not serializable
    public HttpClient Client { get; set; }      // Not serializable
}
```

### Custom Serialization

Implement `ICheckpointSerializer<T>` for custom serialization:

```csharp
public interface ICheckpointSerializer<T>
{
    string Serialize(T value, SerializationContext context);
    T Deserialize(string data, SerializationContext context);
}

public record SerializationContext(string OperationId, string DurableExecutionArn);
```

Usage:

```csharp
var result = await context.StepAsync(
    async () => await GetLargeData(),
    name: "get_data",
    config: new StepConfig
    {
        Serializer = new CompressedJsonSerializer<LargeData>()
    });
```

### NativeAOT compatibility

The SDK is AOT-friendly but does not require AOT. The default JSON serialization uses reflection (standard `System.Text.Json` behavior), which works in JIT mode. For NativeAOT deployments, provide a `JsonSerializerContext` via the `ICheckpointSerializer<T>` interface — this avoids all runtime reflection and is fully trim-safe. The SDK itself avoids `Activator.CreateInstance`, `Type.GetType()`, and other reflection patterns, and uses `[DynamicallyAccessedMembers]` trimming annotations where needed.

```csharp
// Default: works with reflection (JIT mode)
var result = await context.StepAsync<Order>(async () => await GetOrder());

// AOT mode: user provides serialization context
var result = await context.StepAsync(
    async () => await GetOrder(),
    config: new StepConfig { Serializer = new JsonCheckpointSerializer<Order>(MyJsonContext.Default.Order) });
```

---

## Integration with Existing Libraries

### Amazon.Lambda.Core

The SDK uses existing Lambda core interfaces:
- `ILambdaContext` -- available via `context.LambdaContext`
- `ILambdaSerializer` -- used for event deserialization

### Amazon.Lambda.RuntimeSupport

The durable execution handler integrates with the existing runtime support bootstrap:

```csharp
// The [DurableExecution] attribute signals that the handler
// receives DurableExecutionInvocationInput and returns DurableExecutionInvocationOutput
// The SDK handles the translation to/from the user's handler signature
```

### Amazon.Lambda.Annotations

`[DurableExecution]` hooks into the existing annotations source generator. At compile time it:

1. Generates a handler wrapper that translates `DurableExecutionInvocationInput` to/from your types
2. Manages context lifecycle (creation, checkpoint batching, cleanup)
3. Adds `DurableConfig` to the CloudFormation template
4. Adds the `AWSLambdaBasicDurableExecutionRolePolicy` managed policy

```csharp
public class Functions
{
    [LambdaFunction]
    [DurableExecution(ExecutionTimeout = 3600, RetentionPeriodInDays = 7)]
    public async Task<OrderResult> ProcessOrder(
        [FromBody] OrderRequest request,
        IDurableContext context)
    {
        var validated = await context.StepAsync(
            async () => await Validate(request),
            name: "validate");
        // ...
    }
}
```

### AWSSDK.Lambda

The SDK depends on the AWS SDK for .NET Lambda client to make checkpoint API calls:
- `CheckpointDurableExecutionAsync`
- `GetDurableExecutionStateAsync`

---

## Testing (customer-facing package)

We ship a separate NuGet package (`Amazon.Lambda.DurableExecution.Testing`) that lets developers test their durable functions locally without deploying to AWS.

**Why this needs to exist:** A durable function requires multiple Lambda invocations to complete (invoke → PENDING → wait → re-invoke → SUCCEEDED). You can't test that with a normal unit test because there's no Lambda service orchestrating the re-invocations. The test runner simulates this loop in-process: it calls your handler, gets PENDING, marks waits as elapsed, calls your handler again with the prior checkpoint state, and repeats until the workflow completes.

```csharp
var runner = new DurableTestRunner<OrderEvent, OrderResult>(
    handler: new Function().Handler,
    options: new TestRunnerOptions
    {
        SkipTime = true,      // Waits complete instantly (no real delays)
        MaxInvocations = 10   // Safety limit to prevent infinite loops
    });

var result = await runner.RunAsync(
    input: new OrderEvent { OrderId = "order-123" },
    timeout: TimeSpan.FromSeconds(30));

Assert.Equal(InvocationStatus.Succeeded, result.Status);
Assert.Equal("approved", result.Result.Status);

// Inspect individual steps
var validateStep = result.GetStep("validate_order");
Assert.True(validateStep.GetResult<ValidationResult>().IsValid);
```

The Python and JS SDKs both ship equivalent test runner packages.

---

## Local development (Test Tool v2 and Aspire)

The Lambda Test Tool v2 and the Aspire Lambda integration currently emulate single-invocation Lambda functions. Durable functions require a multi-invocation loop that neither tool supports today. To add support, the local emulator needs three things:

### Checkpoint API endpoints

The SDK calls these during execution. The emulator would serve them locally with in-memory storage:

- `POST /checkpoint-durable-execution` -- store step results, wait records
- `GET /durable-execution-state` -- return accumulated state for replay

### An orchestration loop

When the function returns `PENDING`, the emulator needs to:
- Parse the checkpoint to determine what's pending (timer, callback, retry)
- Wait for that condition (or skip it in fast mode)
- Re-invoke the function with the accumulated `DurableExecutionInvocationInput`
- Repeat until `SUCCEEDED` or `FAILED`

### Callback delivery

An endpoint that external tools (or the developer via the UI) can call to deliver callback results:

- `POST /send-durable-execution-callback-success`
- This triggers a re-invocation of the waiting execution

### How this relates to the testing SDK

The `DurableTestRunner` in the testing package implements the same orchestration loop programmatically. The test tool / Aspire enhancement would reuse this engine and wrap it in a web UI or Aspire dashboard, giving developers a visual way to see execution state, deliver callbacks manually, skip timers, and inspect checkpoint history.

### Priority

This is post-v1 work. For the initial release, developers test durable functions using the programmatic `DurableTestRunner` or by deploying to AWS. Test tool and Aspire support are a fast-follow once the core SDK is stable.

---

## Requirements & Constraints

- **Target framework:** `net8.0` only. .NET 6 is EOL and not supported. Durable functions are a new feature — adopters will be on the latest managed runtime. Targeting .NET 8 gives access to `required` properties, improved `System.Text.Json` source generation, and better NativeAOT support.
- **Lambda runtime:** Requires the managed .NET 8 runtime or a custom runtime (`provided.al2023`) for NativeAOT deployments.
- **Durable execution service:** The function must be configured with `DurableConfig` (handled automatically by the `[DurableExecution]` source generator).
- **Qualified function identifiers:** `InvokeAsync` requires a version number, alias, or `$LATEST` — unqualified ARNs are not supported for durable invocations.
- **Serializable results:** All step return types must be JSON-serializable (or use a custom `ICheckpointSerializer<T>`).

---

## Package Structure

### Amazon.Lambda.DurableExecution (Runtime)

The core SDK that runs in Lambda. Minimal dependencies.

**Dependencies:**
- `Amazon.Lambda.Core` (existing)
- `AWSSDK.Lambda` (for checkpoint/state APIs)
- `Microsoft.Extensions.Logging.Abstractions` (for ILogger)

### Amazon.Lambda.DurableExecution.Testing (Dev-only)

Test runner and helpers for local/cloud testing.

**Dependencies:**
- `Amazon.Lambda.DurableExecution`
- `Amazon.Lambda.TestUtilities` (existing)

---

## Implementation plan

| Workstream | Scope | Estimate |
|------------|-------|----------|
| **Durable execution runtime** | Core SDK: replay engine, all context operations (step, wait, callback, invoke, parallel, map), checkpoint batching, retry, logging | ~5-6 weeks |
| **Annotations / source generator** | `[DurableExecution]` attribute, handler wrapper codegen, CloudFormation DurableConfig + IAM policy generation | ~2 weeks |
| **Testing SDK** | Local test runner (in-memory, time-skipping), cloud test runner, step inspection API | ~1.5 weeks |
| **Blueprints, docs, examples** | `dotnet new` project templates, developer guide, API reference, sample projects | ~2 weeks |

**Total: ~10-11 weeks (1 engineer familiar with the Python/JS SDKs)**

---

## Cross-SDK API comparison

All three SDKs expose the same core operations. The differences are naming conventions, parameter ordering, and concurrency model.

| Operation | .NET | Python | JavaScript |
|-----------|------|--------|------------|
| Step | `context.StepAsync(func, name?, config?)` | `context.step(func, name?, config?)` | `context.step(name?, fn, config?)` → `DurablePromise<T>` |
| Wait | `context.WaitAsync(duration, name?)` | `context.wait(duration, name?)` | `context.wait(name?, duration)` → `DurablePromise<void>` |
| Create callback | `context.CreateCallbackAsync<T>(name?, config?)` | `context.create_callback(name?, config?)` | `context.createCallback(name?, config?)` |
| Wait for callback | `context.WaitForCallbackAsync<T>(submitter, name?, config?)` | `context.wait_for_callback(submitter, name?, config?)` | `context.waitForCallback(name?, submitter, config?)` |
| Invoke | `context.InvokeAsync<P,R>(funcName, payload, name?, config?)` | `context.invoke(func_name, payload, name?, config?)` | `context.invoke(name?, funcId, input, config?)` → `DurablePromise<T>` |
| Parallel | `context.ParallelAsync<T>(functions, name?, config?)` | `context.parallel(functions, name?, config?)` | `context.parallel(name?, branches, config?)` |
| Map | `context.MapAsync<TItem,TResult>(items, func, name?, config?)` | `context.map(inputs, func, name?, config?)` | `context.map(name?, items, mapFunc, config?)` |
| Child context | `context.RunInChildContextAsync<T>(func, name?, config?)` | `context.run_in_child_context(func, name?, config?)` | `context.runInChildContext(name?, fn, config?)` |
| Wait for condition | `context.WaitForConditionAsync<T>(check, config, name?)` | `context.wait_for_condition(check, config, name?)` | `context.waitForCondition(name?, checkFunc, config?)` |
| Logger | `context.Logger` (ILogger) | `context.logger` (Logger) | `context.logger` (DurableContextLogger) |
| Lambda context | `context.LambdaContext` | `context.lambda_context` | `context.lambdaContext` |
| Execution context | `context.ExecutionContext` | `context.execution_context` | *(via logger metadata)* |
| Promise combinators | `CompletionConfig` on `ParallelAsync` | `CompletionConfig` on `parallel`/`map` | `context.promise.all/allSettled/any/race` |
| Configure logger | `context.ConfigureLogger(config)` | `context.set_logger(logger)` | `context.configureLogger(config)` |
| Cancellation | `CancellationToken` on all methods | *(N/A)* | *(N/A)* |

**Key differences:**

- **Concurrency model:** JS returns `DurablePromise<T>` (lazy, deferred until awaited). Python is synchronous (blocks the thread). .NET returns `Task<T>` (standard async/await).
- **Name parameter position:** JS puts `name` first; Python and .NET put it after the function/duration.
- **Parallel semantics in JS:** JS uses `context.promise.all/any/race/allSettled` to combine DurablePromises. .NET and Python use `CompletionConfig` on the `Parallel`/`Map` operations instead.
- **.NET-only:** `CancellationToken` on every method (standard .NET pattern).

---

## Common Patterns

### GenAI Agentic Loop

```csharp
[DurableExecution]
public async Task<string> AgentHandler(AgentRequest input, IDurableContext context)
{
    var messages = new List<Message>
    {
        new Message { Role = "user", Content = input.Prompt }
    };

    while (true)
    {
        var response = await context.StepAsync(
            async () => await InvokeModel(messages),
            name: "invoke_model");

        if (response.ToolCall == null)
            return response.Content;

        var toolResult = await context.StepAsync(
            async () => await ExecuteTool(response.ToolCall),
            name: $"tool_{response.ToolCall.Name}");

        messages.Add(new Message { Role = "assistant", Content = toolResult });
    }
}
```

### Human-in-the-Loop

```csharp
[DurableExecution]
public async Task<ReviewResult> ReviewHandler(ReviewRequest input, IDurableContext context)
{
    var analysis = await context.StepAsync(
        async () => await AnalyzeDocument(input.DocumentUrl),
        name: "analyze_document");

    context.Logger.LogInformation("Analysis complete, requesting human review");

    var review = await context.WaitForCallbackAsync<HumanReview>(
        async (callbackId, ctx) =>
        {
            await NotifyReviewer(input.ReviewerEmail, callbackId, analysis);
        },
        name: "human_review",
        config: new WaitForCallbackConfig
        {
            Timeout = Duration.FromDays(7),
            HeartbeatTimeout = Duration.FromHours(24)
        });

    if (review.Approved)
    {
        await context.StepAsync(
            async () => await PublishDocument(input.DocumentUrl),
            name: "publish");
    }

    return new ReviewResult { Status = review.Approved ? "published" : "rejected" };
}
```

### Scheduled Pipeline with Retries

```csharp
[DurableExecution]
public async Task<PipelineResult> DataPipeline(PipelineInput input, IDurableContext context)
{
    // Extract
    var rawData = await context.StepAsync(
        async () => await ExtractFromSource(input.SourceId),
        name: "extract",
        config: new StepConfig
        {
            RetryStrategy = RetryStrategy.Exponential(maxAttempts: 5, initialDelay: TimeSpan.FromSeconds(2))
        });

    // Transform (fan-out)
    var transformed = await context.MapAsync(
        items: rawData.Chunks,
        func: async (ctx, chunk, index, _) =>
        {
            return await ctx.StepAsync(
                async () => await TransformChunk(chunk),
                name: $"transform_{index}");
        },
        name: "transform_all",
        config: new MapConfig { MaxConcurrency = 10 });

    transformed.ThrowIfError();

    // Load
    var loadResult = await context.StepAsync(
        async () => await LoadToDestination(transformed.GetResults()),
        name: "load",
        config: new StepConfig
        {
            Semantics = StepSemantics.AtMostOncePerRetry  // Don't double-load
        });

    // Wait before next run
    await context.WaitAsync(Duration.FromHours(1), name: "schedule_delay");

    return new PipelineResult { RecordsProcessed = loadResult.Count };
}
```

---

## References

- [AWS Blog: Build multi-step applications and AI workflows with AWS Lambda durable functions](https://aws.amazon.com/blogs/aws/build-multi-step-applications-and-ai-workflows-with-aws-lambda-durable-functions/)
- [AWS Documentation: Lambda Durable Functions](https://docs.aws.amazon.com/lambda/latest/dg/durable-functions.html)
- [Python SDK Repository](https://github.com/aws/aws-durable-execution-sdk-python)
- [JavaScript/TypeScript SDK Repository](https://github.com/aws/aws-durable-execution-sdk-js)
- [GitHub Issue #2216: .NET Durable Functions Support](https://github.com/aws/aws-lambda-dotnet/issues/2216)
- [Existing .NET Annotations Design Doc](lambda-annotations-design.md)
