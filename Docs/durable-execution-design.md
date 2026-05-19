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

1. Lambda invokes your function with a service-envelope payload containing:
   - `DurableExecutionArn` -- unique execution identifier
   - `CheckpointToken` -- for optimistic concurrency
   - `InitialExecutionState` -- previously checkpointed operations

   The SDK reads this envelope, hands your workflow only the user payload, and writes the response envelope on the way out — your code never sees the wire format.

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
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MyDurableFunction;

public class Function
{
    [LambdaFunction]
    [DurableExecution]
    public async Task<OrderResult> Handler(OrderEvent input, IDurableContext context)
    {
        // Step 1: Validate the order (checkpointed automatically)
        var validation = await context.StepAsync(
            async (step) => await ValidateOrder(input.OrderId),
            name: "validate_order");

        if (!validation.IsValid)
            return new OrderResult { Status = "rejected" };

        // Step 2: Wait for processing (Lambda is NOT running during this time)
        await context.WaitAsync(TimeSpan.FromSeconds(30), name: "processing_delay");

        // Step 3: Process the order
        var result = await context.StepAsync(
            async (step) => await ProcessOrder(input.OrderId),
            name: "process_order");

        return new OrderResult { Status = "approved", OrderId = result.OrderId };
    }

    private async Task<ValidationResult> ValidateOrder(string orderId) { /* ... */ }
    private async Task<ProcessResult> ProcessOrder(string orderId) { /* ... */ }
}
```

Things to notice:
- `[LambdaFunction]` + `[DurableExecution]` triggers source generation, so you don't wire up the handler yourself
- Each step function receives an `IStepContext` with a step-scoped logger, attempt number, and operation ID
- Each `StepAsync` call checkpoints its result automatically
- `WaitAsync` suspends the function -- Lambda is not running (or billing you) during the wait
- On replay, completed steps return their cached result without re-executing
- The generated wrapper handles checkpoint batching and cleanup

#### Manual Handler (Without Annotations)

If you don't use `Amazon.Lambda.Annotations`, register `DurableEntryPoint<TInput, TOutput>` as your Lambda handler. The entry point owns all wire-envelope (de)serialization — your workflow only deals with `TInput`/`TOutput`:

```csharp
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace MyDurableFunction;

public class Function
{
    private static readonly DurableEntryPoint<OrderEvent, OrderResult> _entry = new(MyWorkflow);

    public static async Task Main()
    {
        await LambdaBootstrapBuilder
            .Create(_entry.InvokeAsync, new DefaultLambdaJsonSerializer())
            .Build()
            .RunAsync();
    }

    private static async Task<OrderResult> MyWorkflow(OrderEvent input, IDurableContext context)
    {
        var validation = await context.StepAsync(
            async (step) => await ValidateOrder(input.OrderId),
            name: "validate_order");

        if (!validation.IsValid)
            return new OrderResult { Status = "rejected" };

        await context.WaitAsync(TimeSpan.FromSeconds(30), name: "processing_delay");

        var result = await context.StepAsync(
            async (step) => await ProcessOrder(input.OrderId),
            name: "process_order");

        return new OrderResult { Status = "approved", OrderId = result.OrderId };
    }

    private static async Task<ValidationResult> ValidateOrder(string orderId) { /* ... */ }
    private static async Task<ProcessResult> ProcessOrder(string orderId) { /* ... */ }
}
```

`DurableEntryPoint.InvokeAsync` is a `Stream → Stream` Lambda handler. It:
- Deserializes the service envelope using a library-internal `JsonSerializerContext`
- Hydrates `ExecutionState` from `InitialExecutionState`
- Extracts the user payload via the registered `ILambdaSerializer` and runs your workflow through `DurableExecutionHandler.RunAsync`
- Serializes the result using the registered `ILambdaSerializer`, wraps it in the response envelope, and writes the envelope back to the stream

For workflows that return no value, use the single-type-parameter form:

```csharp
private static readonly DurableEntryPoint<OrderEvent> _entry = new(MyWorkflow);

private static async Task MyWorkflow(OrderEvent input, IDurableContext context)
{
    await context.StepAsync(async (step) => await SendNotification(input.UserId), name: "notify");
    await context.WaitAsync(TimeSpan.FromHours(1), name: "cooldown");
    await context.StepAsync(async (step) => await Cleanup(input.UserId), name: "cleanup");
}
```

For **NativeAOT** deployments, register a `SourceGeneratorLambdaJsonSerializer<TContext>` whose `JsonSerializerContext` lists only your own types. The library's internal envelope context handles the wire format — users never register envelope types, so no source-gen warnings or accessibility errors:

```csharp
[JsonSerializable(typeof(OrderEvent))]
[JsonSerializable(typeof(OrderResult))]
public partial class MyJsonContext : JsonSerializerContext { }

public class Function
{
    private static readonly DurableEntryPoint<OrderEvent, OrderResult> _entry = new(MyWorkflow);

    public static async Task Main()
    {
        await LambdaBootstrapBuilder
            .Create(_entry.InvokeAsync, new SourceGeneratorLambdaJsonSerializer<MyJsonContext>())
            .Build()
            .RunAsync();
    }

    private static async Task<OrderResult> MyWorkflow(OrderEvent input, IDurableContext context)
    {
        // ...
    }
}
```

To inject a custom `IAmazonLambda` client (e.g., for VPC endpoints or unit testing), pass it to the `DurableEntryPoint` constructor:

```csharp
private static readonly DurableEntryPoint<OrderEvent, OrderResult> _entry =
    new(MyWorkflow, new AmazonLambdaClient(/* custom config */));
```

You'd also need to manually configure the CloudFormation template with `DurableConfig` and managed policies:

```json
{
  "Resources": {
    "MyFunction": {
      "Type": "AWS::Serverless::Function",
      "Properties": {
        "Handler": "MyDurableFunction",
        "Policies": [
          "AWSLambdaBasicExecutionRole",
          "AWSLambdaBasicDurableExecutionRolePolicy"
        ],
        "DurableConfig": {
          "Enabled": true
        }
      }
    }
  }
}
```

##### Two-stage (de)serialization

The reason `DurableEntryPoint` exists — instead of a typed `(EnvelopeIn, ILambdaContext) → EnvelopeOut` handler — is to keep the wire envelope and its internal types out of the user's `JsonSerializerContext`. Under AOT/source-gen JSON, the user's context lives in a different assembly than the library, so it can't see internal envelope types or attribute-referenced internal converters. Splitting (de)serialization into two stages avoids the leak entirely:

| Stage | Owner | Reads/writes | Context used |
|---|---|---|---|
| 1. Envelope | Library | `Stream` ↔ `DurableExecutionInvocationInput`/`Output` | Internal `DurableEnvelopeJsonContext` (sees all internal types) |
| 2. User payload | User | `string` ↔ `TInput`/`TOutput` | The `ILambdaSerializer` you register with `LambdaBootstrapBuilder` |

The user's serializer is read from `ILambdaContext.Serializer` at invocation time (the new `LambdaBootstrapBuilder.Create(Func<Stream, ILambdaContext, Task<Stream>>, ILambdaSerializer)` overload propagates it). With AOT this means the user only registers their own POCOs; the library's envelope types stay internal.

Differences vs the Annotations approach:
- You define `Main` and call `LambdaBootstrapBuilder` yourself
- You configure `DurableConfig` + managed policies in your CloudFormation template manually (not generated)
- No `[LambdaFunction]` or `[DurableExecution]` attributes needed

With `[LambdaFunction] + [DurableExecution]`, even the `Main` entry point and CloudFormation config are generated at compile time — you just write the workflow method.

---

### Steps

> **Implementations:** [Python](https://github.com/aws/aws-durable-execution-sdk-python/blob/main/src/aws_durable_execution_sdk_python/operation/step.py) | [JavaScript](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js/src/handlers/step-handler/step-handler.ts)

A step runs your code and checkpoints the result. On replay, the cached result comes back without re-executing. Each step function receives an `IStepContext` with a step-scoped logger and attempt metadata.

```csharp
// Basic step
var result = await context.StepAsync(async (step) => await CallExternalApi());

// Named step (recommended for debugging/testing)
var user = await context.StepAsync(
    async (step) => await FetchUser(userId),
    name: "fetch_user");

// Using the step-scoped logger (includes step name, attempt number, operation ID)
var order = await context.StepAsync(
    async (step) =>
    {
        step.Logger.LogInformation("Fetching order {OrderId}", orderId);
        return await orderService.GetOrder(orderId);
    },
    name: "get_order");

// Step with configuration
var payment = await context.StepAsync(
    async (step) => await chargeCard(amount),
    name: "charge_card",
    config: new StepConfig
    {
        Semantics = StepSemantics.AtMostOncePerRetry,
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

> **Implementations:** [Python](https://github.com/aws/aws-durable-execution-sdk-python/blob/main/src/aws_durable_execution_sdk_python/operation/wait.py) | [JavaScript](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js/src/handlers/wait-handler/wait-handler.ts)

Waits suspend the function without consuming compute time. Lambda can recycle the execution environment.

```csharp
// Wait for a specific duration
await context.WaitAsync(TimeSpan.FromSeconds(30));
await context.WaitAsync(TimeSpan.FromMinutes(5), name: "cooldown");
await context.WaitAsync(TimeSpan.FromHours(24), name: "daily_check");
await context.WaitAsync(TimeSpan.FromDays(7), name: "weekly_reminder");
```

> **Validation:** The duration must be at least 1 second. Values less than 1 second throw `ArgumentOutOfRangeException`. Sub-second precision is truncated to whole seconds (the underlying service operates at second granularity).

---

### Callbacks

> **Implementations:** [Python](https://github.com/aws/aws-durable-execution-sdk-python/blob/main/src/aws_durable_execution_sdk_python/operation/callback.py) | [JavaScript](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js/src/handlers/callback-handler/callback.ts)

Callbacks let your workflow pause until an external system responds (human approval, a webhook, a third-party API).

#### Create a Callback (Advanced)

```csharp
// Create a callback and get the callback ID
var callback = await context.CreateCallbackAsync<ApprovalResult>(
    name: "approval_callback",
    config: new CallbackConfig
    {
        Timeout = TimeSpan.FromHours(24),
        HeartbeatTimeout = TimeSpan.FromHours(2)
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
        Timeout = TimeSpan.FromHours(24),
        RetryStrategy = RetryStrategy.Exponential(maxAttempts: 3)
    });

if (approval.Approved)
{
    await context.StepAsync(async (step) => await ExecutePlan(), name: "execute");
}
```

**Example `SendApprovalEmail` stub:**
```csharp
private async Task SendApprovalEmail(string callbackId, string recipientEmail)
{
    // Include the callbackId in the approval link so the external system
    // can complete the callback via the AWS API
    var approvalLink = $"https://my-app.example.com/approve?callbackId={callbackId}";
    await emailService.SendAsync(recipientEmail, "Approval Required", $"Please approve: {approvalLink}");
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

> **Implementations:** [Python](https://github.com/aws/aws-durable-execution-sdk-python/blob/main/src/aws_durable_execution_sdk_python/operation/invoke.py) | [JavaScript](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js/src/handlers/invoke-handler/invoke-handler.ts)

Call another durable function. The invocation is checkpointed, so it survives failures and won't double-fire.

```csharp
// Invoke another durable function
var paymentResult = await context.InvokeAsync<PaymentRequest, PaymentResult>(
    functionName: "arn:aws:lambda:us-east-1:123456789012:function:payment-processor:prod",
    payload: new PaymentRequest { Amount = 100, Currency = "USD" },
    name: "process_payment",
    config: new InvokeConfig
    {
        Timeout = TimeSpan.FromMinutes(5)
    });
```

> **Note:** Durable function invocations require **qualified identifiers** — include a version number, alias, or `$LATEST`:
> - ✅ `arn:aws:lambda:us-east-1:123456789012:function:payment-processor:prod` (alias)
> - ✅ `arn:aws:lambda:us-east-1:123456789012:function:payment-processor:42` (version)  
> - ✅ `arn:aws:lambda:us-east-1:123456789012:function:payment-processor:$LATEST`
> - ❌ `arn:aws:lambda:us-east-1:123456789012:function:payment-processor` (unqualified — not supported)

---

### Parallel Execution

> **Implementations:** [Python](https://github.com/aws/aws-durable-execution-sdk-python/blob/main/src/aws_durable_execution_sdk_python/operation/parallel.py) | [JavaScript](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js/src/handlers/parallel-handler/parallel-handler.ts)

Run independent operations concurrently. The JS SDK uses a `DurablePromise` pattern where operations are deferred until awaited; in .NET that isn't necessary because `ParallelAsync` and `MapAsync` cover the same use case idiomatically. `Task`-returning methods start immediately and `await` retrieves the result, so there's no gap to fill with a lazy wrapper.

> **Prefer `ParallelAsync` over `Task.WhenAll`:** While `Task.WhenAll` works correctly with durable operations (operation IDs are allocated deterministically), it bypasses completion policies, concurrency limits, branch naming, and `IBatchResult` structured output. Always use `ParallelAsync` or `MapAsync` for concurrent durable operations. A future Roslyn analyzer (DE004) will flag `Task.WhenAll` usage with durable tasks and suggest `ParallelAsync` as a replacement.

```csharp
// Run multiple operations in parallel
var results = await context.ParallelAsync(
    new Func<IDurableContext, Task<object>>[]
    {
        async (ctx) => await ctx.StepAsync(async (step) => await FetchUserData(userId), name: "fetch_user"),
        async (ctx) => await ctx.StepAsync(async (step) => await FetchOrderHistory(userId), name: "fetch_orders"),
        async (ctx) => await ctx.StepAsync(async (step) => await FetchPreferences(userId), name: "fetch_prefs"),
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
        new("fetch_user", async (ctx) => await ctx.StepAsync(async (step) => await FetchUserData(userId))),
        new("fetch_orders", async (ctx) => await ctx.StepAsync(async (step) => await FetchOrderHistory(userId))),
        new("fetch_prefs", async (ctx) => await ctx.StepAsync(async (step) => await FetchPreferences(userId))),
    },
    name: "parallel_fetch");

// In tests, you can find specific branches by name
var fetchUserBranch = result.GetOperation("fetch_user");
```

#### Completion Configurations

`ParallelAsync` and `MapAsync` accept a `CompletionConfig` to control when the overall operation is considered complete:

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

> **Implementations:** [Python](https://github.com/aws/aws-durable-execution-sdk-python/blob/main/src/aws_durable_execution_sdk_python/operation/map.py) | [JavaScript](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js/src/handlers/map-handler/map-handler.ts)

Process a collection in parallel with configurable concurrency. The `items` parameter accepts any `IReadOnlyList<T>` (arrays, lists, etc.).

```csharp
var orders = new[] { "order-1", "order-2", "order-3", "order-4", "order-5" };

var results = await context.MapAsync(
    items: orders, // IReadOnlyList<string>
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

> **Implementations:** [Python](https://github.com/aws/aws-durable-execution-sdk-python/blob/main/src/aws_durable_execution_sdk_python/operation/child.py) | [JavaScript](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js/src/handlers/run-in-child-context-handler/run-in-child-context-handler.ts)

Child contexts group related durable operations into a sub-workflow. Use them when you need waits or multiple steps inside a logical unit (you cannot nest durable calls inside a step directly).

```csharp
// Group operations into a child context
var enrichedData = await context.RunInChildContextAsync(
    async (childCtx) =>
    {
        var validated = await childCtx.StepAsync(
            async () => await Validate(data),
            name: "validate");

        await childCtx.WaitAsync(TimeSpan.FromSeconds(1), name: "rate_limit");

        var enriched = await childCtx.StepAsync(
            async () => await Enrich(validated),
            name: "enrich");

        return enriched;
    },
    name: "validation_phase");

// Use the enriched data in a subsequent step
var finalResult = await context.StepAsync(
    async () => await SubmitEnrichedData(enrichedData),
    name: "submit");
```

> **Why child contexts?** You cannot nest durable operations inside a step. Steps are leaf operations. If you need multiple durable operations grouped together, use a child context.

---

### Error Handling & Retry

> **Implementations:** [Python](https://github.com/aws/aws-durable-execution-sdk-python/blob/main/src/aws_durable_execution_sdk_python/retries.py) | [JavaScript](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js/src/utils/retry/retry-config/index.ts)

#### Retry Strategies

```csharp
// Exponential backoff with jitter
var result = await context.StepAsync(
    async () => await CallUnreliableApi(),
    name: "api_call",
    config: new StepConfig
    {
        RetryStrategy = RetryStrategy.Exponential(
            maxAttempts: 5,
            initialDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromSeconds(60),
            backoffRate: 2.0,
            jitter: JitterStrategy.Full)
    });

// Using presets
var result = await context.StepAsync(
    async () => await CallApi(),
    name: "api_call",
    config: new StepConfig
    {
        RetryStrategy = RetryStrategy.Default  // 6 attempts, 2x backoff, 5s initial, Full jitter
    });

// Available presets:
// RetryStrategy.None       — maxAttempts: 1 (no retry)
// RetryStrategy.Default    — 6 attempts, 2x backoff, 5s initial delay, Full jitter
// RetryStrategy.Transient  — 3 attempts, 2x backoff, 1s initial delay, Full jitter

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

// Retry with message pattern matching (regex)
var result = await context.StepAsync(
    async () => await CallApi(),
    name: "api_call",
    config: new StepConfig
    {
        RetryStrategy = RetryStrategy.Exponential(
            maxAttempts: 3,
            retryableExceptions: new[] { typeof(HttpRequestException) },
            retryableMessagePatterns: new[] { "timeout", "throttl", "5\\d{2}" })
    });
```

#### Jitter Strategies

Jitter prevents thundering-herd scenarios where multiple retrying clients converge on the same backoff schedule. The SDK supports three jitter strategies:

```csharp
public enum JitterStrategy
{
    /// No randomization — delay is exactly the calculated backoff value.
    None,

    /// Random delay between 0 and the calculated backoff value (recommended).
    Full,

    /// Random delay between 50% and 100% of the calculated backoff value.
    Half
}
```

The default jitter for `RetryStrategy.Exponential()` is `JitterStrategy.Full`. All built-in presets (`RetryStrategy.Default`, `RetryStrategy.Transient`) also use `JitterStrategy.Full`. Use `JitterStrategy.None` only when you need deterministic retry timing (e.g., for testing).

#### Retry Strategy Interface

```csharp
public interface IRetryStrategy
{
    RetryDecision ShouldRetry(Exception exception, int attemptNumber);
}

public record RetryDecision
{
    public bool ShouldRetry { get; }
    public TimeSpan Delay { get; }

    public static RetryDecision DoNotRetry() => new() { ShouldRetry = false };
    public static RetryDecision RetryAfter(TimeSpan delay) => new() { ShouldRetry = true, Delay = delay };
}
```

`IRetryStrategy` supports implicit conversion from `Func<Exception, int, RetryDecision>`, enabling inline lambdas:

```csharp
config: new StepConfig
{
    RetryStrategy = (ex, attempt) =>
        attempt < 3 && ex is HttpRequestException
            ? RetryDecision.RetryAfter(TimeSpan.FromSeconds(Math.Pow(2, attempt)))
            : RetryDecision.DoNotRetry()
}
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

> **Implementations:** [Python](https://github.com/aws/aws-durable-execution-sdk-python/blob/main/src/aws_durable_execution_sdk_python/logger.py) | [JavaScript](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js/src/utils/logger/logger.ts)

`context.Logger` is replay-aware: it suppresses duplicate messages that would otherwise repeat on every invocation. Use it instead of `Console.WriteLine`.

> **Implementation note:** The replay-aware logger is implemented entirely in the durable execution SDK. During replay, the SDK tracks which operations are being restored from checkpoint state vs. executing for the first time, and suppresses log output for replayed operations. No changes to `Amazon.Lambda.RuntimeSupport` or the Lambda Runtime API are required.

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
5. `RunAsync` returns PENDING; the abandoned user task is left to be GC'd; Lambda terminates

### Lifecycle and cleanup

`RunAsync` manages the full lifecycle internally. When the handler completes (SUCCEEDED/FAILED) or suspends (PENDING), `RunAsync` stops the background checkpoint batcher, flushes any pending checkpoint operations, and disposes internal state. Users never call `Dispose` or wrap anything in `await using`.

---

## API Reference

### DurableEntryPoint

The non-Annotations Lambda handler. Reads the wire envelope from a `Stream`, runs the workflow, and writes the response envelope back. Same shape regardless of JIT or AOT — the only thing that varies is the `ILambdaSerializer` you register with `LambdaBootstrapBuilder`.

```csharp
/// <summary>
/// AOT-friendly entry point for a durable workflow. Owns (de)serialization of
/// the wire envelope so users only register their own POCO types in their
/// JsonSerializerContext.
/// </summary>
public sealed class DurableEntryPoint<TInput, TOutput>
{
    /// <summary>
    /// Uses a default AmazonLambdaClient, constructed lazily and cached process-wide.
    /// </summary>
    public DurableEntryPoint(Func<TInput, IDurableContext, Task<TOutput>> workflow);

    /// <summary>
    /// Uses the supplied IAmazonLambda for checkpoint and state-fetch calls.
    /// </summary>
    public DurableEntryPoint(Func<TInput, IDurableContext, Task<TOutput>> workflow, IAmazonLambda lambdaClient);

    /// <summary>
    /// Lambda handler entry point. Register with LambdaBootstrapBuilder alongside
    /// an ILambdaSerializer that knows how to (de)serialize TInput/TOutput.
    /// </summary>
    public Task<Stream> InvokeAsync(Stream input, ILambdaContext context);
}

/// <summary>
/// AOT-friendly entry point for a void durable workflow.
/// </summary>
public sealed class DurableEntryPoint<TInput>
{
    public DurableEntryPoint(Func<TInput, IDurableContext, Task> workflow);
    public DurableEntryPoint(Func<TInput, IDurableContext, Task> workflow, IAmazonLambda lambdaClient);
    public Task<Stream> InvokeAsync(Stream input, ILambdaContext context);
}
```

`DurableEntryPoint.InvokeAsync` requires an `ILambdaSerializer` to be registered on `ILambdaContext.Serializer`; if it's null, the entry point throws `InvalidOperationException` with a message pointing to `LambdaBootstrapBuilder.Create(handler, serializer)`. In tests, set `TestLambdaContext.Serializer` directly.

The wire-envelope types (`DurableExecutionInvocationInput`/`Output`, `InvocationStatus`, `ErrorObject`) are intentionally `internal` — user code never constructs or reads them.

### IDurableContext

> **Implementations:** [Python](https://github.com/aws/aws-durable-execution-sdk-python/blob/main/src/aws_durable_execution_sdk_python/context.py) | [JavaScript](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js/src/types/durable-context.ts)

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

    // ── StepAsync overloads ────────────────────────────────────────────
    //  The user's function always receives IStepContext, matching the
    //  Python and JS SDKs (Java has no-context overloads but deprecated
    //  them — see https://github.com/aws/aws-durable-execution-sdk-java).

    /// <summary>
    /// Execute a step with automatic checkpointing using reflection-based JSON.
    /// The IStepContext provides a step-scoped logger with operation metadata
    /// (step name, attempt number, operation ID) and the current attempt number.
    /// </summary>
    [RequiresUnreferencedCode("Reflection-based JSON for T. Use the ICheckpointSerializer<T> overload for AOT/trimmed deployments.")]
    [RequiresDynamicCode("Reflection-based JSON for T. Use the ICheckpointSerializer<T> overload for AOT/trimmed deployments.")]
    Task<T> StepAsync<T>(
        Func<IStepContext, Task<T>> func,
        string? name = null,
        StepConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a step that returns no value. AOT-safe (no payload to serialize).
    /// </summary>
    Task StepAsync(
        Func<IStepContext, Task> func,
        string? name = null,
        StepConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a step with AOT-safe checkpoint serialization. The supplied
    /// serializer is used in place of reflection-based JSON.
    /// </summary>
    Task<T> StepAsync<T>(
        Func<IStepContext, Task<T>> func,
        ICheckpointSerializer<T> serializer,
        string? name = null,
        StepConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspend execution for the specified duration.
    /// Throws ArgumentOutOfRangeException if duration is less than 1 second.
    /// </summary>
    Task WaitAsync(
        TimeSpan duration,
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
    /// Execute multiple operations in parallel (unnamed branches).
    /// </summary>
    Task<IBatchResult<T>> ParallelAsync<T>(
        IReadOnlyList<Func<IDurableContext, Task<T>>> functions,
        string? name = null,
        ParallelConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute multiple named operations in parallel. Named branches appear in
    /// execution traces and can be inspected by name in tests.
    /// </summary>
    Task<IBatchResult<T>> ParallelAsync<T>(
        IReadOnlyList<DurableBranch<T>> branches,
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

#### Supporting Types

```csharp
/// <summary>
/// Context passed to step functions. Provides step-scoped logging and metadata.
/// </summary>
public interface IStepContext
{
    /// <summary>
    /// Logger scoped to this step. Includes step name, operation ID, and attempt
    /// number in structured log metadata automatically.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// The current retry attempt number (1-based).
    /// </summary>
    int AttemptNumber { get; }

    /// <summary>
    /// The deterministic operation ID for this step.
    /// </summary>
    string OperationId { get; }
}

/// <summary>
/// A named branch for parallel execution. Named branches appear in execution
/// traces and can be inspected by name in the test runner.
/// </summary>
public record DurableBranch<T>(string Name, Func<IDurableContext, Task<T>> Func);
```

#### CancellationToken behavior

All methods accept a per-call `CancellationToken` that follows standard .NET semantics: cancellation throws `OperationCanceledException` and the execution fails. Cancellation does **not** trigger suspension — those are separate concepts.

The durable execution service handles timeout scenarios automatically: if Lambda terminates mid-execution, the next invocation simply replays from the last checkpoint. For advanced users who want to suspend gracefully before timeout, check `context.LambdaContext.RemainingTime` and return early.

### Configuration Types

> **Implementations:** [Python](https://github.com/aws/aws-durable-execution-sdk-python/blob/main/src/aws_durable_execution_sdk_python/config.py) | JavaScript: [step](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js/src/types/step.ts) &#124; [batch](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js/src/types/batch.ts)

```csharp
/// <summary>
/// Configuration for step execution.
/// </summary>
public class StepConfig
{
    /// <summary>
    /// Retry strategy for failed steps. Default is no retry.
    /// Accepts IRetryStrategy implementations (RetryStrategy.Exponential, etc.)
    /// or an inline function via implicit conversion from
    /// Func&lt;Exception, int, RetryDecision&gt;.
    /// </summary>
    public IRetryStrategy? RetryStrategy { get; set; }

    /// <summary>
    /// Execution semantics. Default is AtLeastOncePerRetry.
    /// </summary>
    public StepSemantics Semantics { get; set; } = StepSemantics.AtLeastOncePerRetry;

    // Note: there is no Serializer property here. Custom serializers are
    // supplied via the AOT-safe StepAsync(..., ICheckpointSerializer<T>, ...)
    // overload, which is type-safe (ICheckpointSerializer<T> instead of the
    // non-generic marker) and gives one obvious way to opt into custom or
    // AOT-friendly serialization.
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
    /// Maximum time to wait for callback response. Default (TimeSpan.Zero) means no timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Maximum time between heartbeat signals before timeout. Default (TimeSpan.Zero) means no heartbeat timeout.
    /// </summary>
    public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.Zero;

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
    /// Maximum time to wait for the invoked function. Default (TimeSpan.Zero) means no timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.Zero;

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
/// Controls how branches are represented in the checkpoint graph.
/// </summary>
public enum NestingType
{
    /// <summary>
    /// Each branch creates a full isolated CONTEXT operation. Higher observability
    /// in execution traces but more checkpoint operations (default).
    /// </summary>
    Nested,

    /// <summary>
    /// Branches use virtual contexts sharing the parent. Reduces checkpoint cost
    /// by ~30% at the expense of less granular execution traces.
    /// </summary>
    Flat
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

    /// <summary>
    /// How branches are represented in the checkpoint graph.
    /// Nested = full isolated context per branch (default).
    /// Flat = virtual contexts sharing parent (~30% fewer checkpoint operations).
    /// </summary>
    public NestingType NestingType { get; set; } = NestingType.Nested;
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

    /// <summary>
    /// How item branches are represented in the checkpoint graph.
    /// </summary>
    public NestingType NestingType { get; set; } = NestingType.Nested;

    /// <summary>
    /// Optional batching configuration for grouping items before processing.
    /// When set, items are grouped into batches and each batch is processed as a unit.
    /// Reduces checkpoint overhead for large collections.
    /// </summary>
    public ItemBatcher? Batcher { get; set; }

    /// <summary>
    /// Optional function to generate a custom name for each item's branch.
    /// Improves observability in execution traces. Receives the item and its index.
    /// If null, branches are named by index (e.g., "0", "1", "2").
    /// </summary>
    public Func<object, int, string>? ItemNamer { get; set; }
}

/// <summary>
/// Groups items into batches for map operations to reduce checkpoint overhead.
/// At least one of MaxItemsPerBatch or MaxBytesPerBatch must be set.
/// </summary>
public class ItemBatcher
{
    /// <summary>
    /// Maximum number of items per batch. Null = no count limit.
    /// </summary>
    public int? MaxItemsPerBatch { get; set; }

    /// <summary>
    /// Maximum serialized size (bytes) per batch. Null = no size limit.
    /// </summary>
    public int? MaxBytesPerBatch { get; set; }
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

    /// <summary>
    /// Optional function to transform exceptions from the child context before
    /// surfacing them to the parent. Useful for wrapping low-level errors into
    /// domain-specific exceptions.
    /// </summary>
    public Func<Exception, Exception>? ErrorMapping { get; set; }
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

> **Implementations:** [Python](https://github.com/aws/aws-durable-execution-sdk-python/blob/main/src/aws_durable_execution_sdk_python/exceptions.py) | [JavaScript](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js/src/errors/durable-error/durable-error.ts)

```csharp
/// <summary>
/// Base exception for all durable execution errors.
/// </summary>
public class DurableExecutionException : Exception { }

/// <summary>
/// Thrown when user code inside a step fails (after retries exhausted).
/// Contains the original error details from the checkpoint.
/// </summary>
public class StepException : DurableExecutionException
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
    public bool IsTimeout { get; }
}

/// <summary>
/// Thrown when an invoked function fails.
/// </summary>
public class InvokeException : DurableExecutionException
{
    public string? FunctionName { get; }
    public string? ErrorType { get; }
    public string? ErrorData { get; }
}

/// <summary>
/// Thrown when a child context operation fails.
/// </summary>
public class ChildContextException : DurableExecutionException
{
    public string? SubType { get; }
}

/// <summary>
/// Thrown when a wait-for-condition operation exhausts all attempts
/// without the condition being met.
/// </summary>
public class WaitForConditionException : DurableExecutionException
{
    public int AttemptsExhausted { get; }
}

/// <summary>
/// Thrown when the operation sequence during replay does not match
/// the previously checkpointed history. Indicates non-deterministic code.
/// </summary>
public class NonDeterministicException : DurableExecutionException
{
    public string? ExpectedOperationId { get; }
    public string? ActualOperationId { get; }
}

/// <summary>
/// Thrown when a step is interrupted mid-execution (e.g., Lambda timeout or
/// runtime termination). The step did not complete and its result was not
/// checkpointed. On the next invocation, the step will re-execute from scratch.
/// </summary>
public class StepInterruptedException : DurableExecutionException
{
    public string? StepName { get; }
    public int AttemptNumber { get; }
}

/// <summary>
/// Thrown when checkpoint serialization or deserialization fails.
/// </summary>
public class SerializationException : DurableExecutionException { }

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

> **Implementations:** [Python](https://github.com/aws/aws-durable-execution-sdk-python/blob/main/src/aws_durable_execution_sdk_python/serdes.py) | [JavaScript](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js/src/utils/serdes/serdes.ts)

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

Usage — pass the serializer to the AOT-safe `StepAsync` overload directly.
This is the only way to override the default reflection-based JSON path; it's
intentional that there's no `StepConfig.Serializer` knob, so you have one
obvious place to opt in (and the type is `ICheckpointSerializer<T>`, not the
non-generic marker, so the compiler catches a mismatched `T`):

```csharp
var result = await context.StepAsync(
    async () => await GetLargeData(),
    new CompressedJsonSerializer<LargeData>(),
    name: "get_data");
```

### Class library vs. executable output

All samples in this doc use the class library pattern (no `Main` method). This is the default for Lambda functions. To turn a durable function project into an executable (required for NativeAOT or custom runtimes):

**With Annotations** — add the global attribute to auto-generate a `Main` method:
```csharp
[assembly: LambdaGlobalProperties(GenerateMain = true)]
```

**Without Annotations** — provide your own `Main` method:
```csharp
public static async Task Main(string[] args)
{
    using var bootstrap = new LambdaBootstrap(
        new Function().FunctionHandler,
        new DefaultLambdaJsonSerializer());
    await bootstrap.RunAsync();
}
```

Both approaches produce a self-contained executable that the Lambda custom runtime can invoke.

### NativeAOT compatibility

The SDK is AOT-friendly but does not require AOT. AOT safety is addressed at two levels:

1. **Entry point** — `DurableEntryPoint` owns wire-envelope (de)serialization through an internal `JsonSerializerContext`. The user-supplied `ILambdaSerializer` (registered with `LambdaBootstrapBuilder`) only handles `TInput`/`TOutput`. For AOT, register a `SourceGeneratorLambdaJsonSerializer<TContext>` whose context lists only your own POCOs — envelope types stay private to the library and never need to appear in the user's context.

2. **Step checkpoints (`IDurableContext.StepAsync`)** — there are two overload families: a reflection-based one annotated with `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`, and an AOT-safe one that takes an `ICheckpointSerializer<T>` parameter. Internally, the reflection overload constructs `ReflectionJsonCheckpointSerializer<T>` (whose constructor carries `[RequiresUnreferencedCode]`); the AOT-safe overload uses the user-supplied serializer and never touches reflection. The void `StepAsync` overloads are AOT-safe by default — they use a built-in null-only serializer since they have no payload.

The SDK itself avoids `Activator.CreateInstance`, `Type.GetType()`, and other reflection patterns, and uses `[DynamicallyAccessedMembers]` trimming annotations where needed.

```csharp
// Default: works with reflection (JIT mode); flagged for AOT.
var result = await context.StepAsync<Order>(async (step) => await GetOrder());

// AOT mode — entry point: register a source-generated ILambdaSerializer.
// Only your own types appear in the context — no envelope types.
[JsonSerializable(typeof(OrderEvent))]
[JsonSerializable(typeof(OrderResult))]
[JsonSerializable(typeof(Order))]
public partial class MyJsonContext : JsonSerializerContext { }

private static readonly DurableEntryPoint<OrderEvent, OrderResult> _entry = new(MyWorkflow);

public static async Task Main()
{
    await LambdaBootstrapBuilder
        .Create(_entry.InvokeAsync, new SourceGeneratorLambdaJsonSerializer<MyJsonContext>())
        .Build()
        .RunAsync();
}

// AOT mode — step checkpoint: pass ICheckpointSerializer<T> to StepAsync directly.
var result = await context.StepAsync(
    async () => await GetOrder(),
    new JsonCheckpointSerializer<Order>(MyJsonContext.Default.Order),
    name: "get_order");
```

### Large payload and checkpoint overflow

The durable execution service imposes size limits:

- **256 KB** per individual operation checkpoint
- **6 MB** maximum Lambda response payload

The SDK handles overflow transparently:

**Step results exceeding 256 KB:** When a step's serialized result exceeds the checkpoint size limit, the SDK splits the checkpoint into a START operation (before execution) and a separate result checkpoint (after execution). On replay, the SDK fetches the result via the paginated `GetDurableExecutionState` API rather than reading it inline from the operation record.

**Batch results (map/parallel) exceeding limits:** For large map/parallel operations, the SDK generates a compact summary for the parent operation's checkpoint. The summary includes item count, success/failure counts, and completion reason — but not individual item results. During replay, the SDK sets `ReplayChildren = true` on the state request, which causes the service to return child operation records so full results can be reconstructed.

**Lambda response exceeding 6 MB:** If the final orchestration result exceeds the response payload limit, the SDK checkpoints the result before returning the response envelope. The service reads the result from the checkpoint rather than from the response body.

**Guidance for very large results:** For results that are inherently large (multi-MB payloads), use a custom `ICheckpointSerializer<T>` that offloads to external storage (S3, DynamoDB) and returns a reference. This keeps checkpoint sizes small and avoids pagination overhead:

```csharp
public class S3BackedSerializer<T> : ICheckpointSerializer<T>
{
    public string Serialize(T value, SerializationContext context)
    {
        var key = $"results/{context.DurableExecutionArn}/{context.OperationId}";
        // Upload to S3, return the key as the checkpoint value
        _s3Client.PutObject(new PutObjectRequest { BucketName = _bucket, Key = key, ... });
        return key;
    }

    public T Deserialize(string data, SerializationContext context)
    {
        // Download from S3 using the stored key
        var response = _s3Client.GetObject(new GetObjectRequest { BucketName = _bucket, Key = data });
        return JsonSerializer.Deserialize<T>(response.ResponseStream);
    }
}
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
// The [DurableExecution] attribute signals that the handler is a durable workflow.
// The Annotations source generator emits a Main method that registers a
// DurableEntryPoint<TInput, TOutput> with LambdaBootstrapBuilder. The wire envelope
// is invisible to the user's handler — they receive TInput and return TOutput.
```

### Amazon.Lambda.Annotations (optional)

`Amazon.Lambda.Annotations` is an **optional** dependency. Users can write durable functions without it (see [Manual Handler](#manual-handler-without-annotations) above), but adding Annotations to the project reduces boilerplate significantly.

When both packages are referenced, the Annotations source generator detects `[DurableExecution]` by fully-qualified name and at compile time:

1. Generates a `Main` entry point that wires up `DurableEntryPoint<TInput, TOutput>` for your workflow
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
            async (step) => await Validate(request),
            name: "validate");
        // ...
    }
}
```

#### Custom Lambda Client

For VPC endpoints, custom retry policies, or testing with mocked clients, inject a custom `IAmazonLambda` client via the `[DurableExecution]` attribute:

```csharp
public class Functions
{
    private readonly IAmazonLambda _lambdaClient;

    public Functions(IAmazonLambda lambdaClient)
    {
        _lambdaClient = lambdaClient;
    }

    [LambdaFunction]
    [DurableExecution(LambdaClientFactory = nameof(_lambdaClient))]
    public async Task<OrderResult> ProcessOrder(
        [FromBody] OrderRequest request,
        IDurableContext context)
    {
        // ...
    }
}
```

When no `LambdaClientFactory` is specified, the generated code creates a default `AmazonLambdaClient`. For the manual handler path, pass the client to the `DurableEntryPoint` constructor.

> **Dependency boundaries:** `Amazon.Lambda.Annotations` has **no dependency** on the AWS SDK or on `Amazon.Lambda.DurableExecution`. The Annotations source generator references durable execution types by fully-qualified name strings only — it never takes a compile-time dependency on the durable package. The `[DurableExecution]` attribute is defined in `Amazon.Lambda.DurableExecution`, and the generated code resolves against the user's project references. There is only one source generator (Annotations) — no coordination between multiple generators is needed.

### AWSSDK.Lambda

The `Amazon.Lambda.DurableExecution` package depends on the AWS SDK for .NET Lambda client to make checkpoint API calls. This dependency is confined to the durable execution package — `Amazon.Lambda.Annotations` does not depend on the AWS SDK.


- `CheckpointDurableExecutionAsync`
- `GetDurableExecutionStateAsync`

---

## Testing (customer-facing package)

> **Implementations:** [JavaScript (local runner)](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js-testing/src/test-runner/local/local-durable-test-runner.ts) | [JavaScript (cloud runner)](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js-testing/src/test-runner/cloud/cloud-durable-test-runner.ts)

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

### Cloud Test Runner

For integration testing against deployed functions, the testing package also ships a `CloudDurableTestRunner` with the same API as the local runner. This lets developers run the exact same assertions against a real Lambda function:

```csharp
var runner = new CloudDurableTestRunner<OrderEvent, OrderResult>(
    functionArn: "arn:aws:lambda:us-east-1:123456789012:function:process-order:$LATEST");

var result = await runner.RunAsync(
    input: new OrderEvent { OrderId = "order-123" },
    timeout: TimeSpan.FromSeconds(60));

Assert.Equal(InvocationStatus.Succeeded, result.Status);
var validateStep = result.GetStep("validate_order");
Assert.True(validateStep.GetResult<ValidationResult>().IsValid);
```

The cloud runner invokes the deployed function and polls `GetDurableExecutionState` until the execution reaches a terminal state, then reconstructs the same `TestResult` structure as the local runner.

### Function Registration for Invoke Testing

To test workflows that use `InvokeAsync` without deploying, register sibling functions with the local test runner:

```csharp
var paymentHandler = new PaymentFunction().Handler;

var runner = new DurableTestRunner<OrderEvent, OrderResult>(
    handler: new OrderFunction().Handler,
    options: new TestRunnerOptions { SkipTime = true });

runner.RegisterFunction("process-payment", paymentHandler);
runner.RegisterFunction(
    "arn:aws:lambda:us-east-1:123:function:process-payment:$LATEST",
    paymentHandler);

var result = await runner.RunAsync(input: new OrderEvent { OrderId = "123" });
```

When the workflow calls `context.InvokeAsync("process-payment", payload)`, the test runner routes to the registered handler instead of making an AWS API call.

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

### Blueprints (`dotnet new` Templates)

New `dotnet new` templates ship as part of the existing `Amazon.Lambda.Templates` NuGet package (same as all other Lambda blueprints in this repo under `Blueprints/BlueprintDefinitions/`).

**Templates to ship:**

| Template short name | Description |
|---------------------|-------------|
| `lambda.DurableFunction` | Minimal durable function with a single step and wait. Includes test project with `DurableTestRunner`. |
| `lambda.DurableFunction.Agentic` | GenAI agentic loop pattern (invoke model → check tool call → execute tool → repeat). |
| `lambda.DurableFunction.HumanInTheLoop` | Callback-based human approval workflow. |

Each template includes:
- `.csproj` with correct NuGet references (`Amazon.Lambda.DurableExecution`, `Amazon.Lambda.Annotations`)
- Handler class with `[LambdaFunction]` + `[DurableExecution]` attributes
- `serverless.template` (auto-generated by source generator on build)
- Test project with `DurableTestRunner` and a passing test
- `aws-lambda-tools-defaults.json` for deployment via `dotnet lambda deploy-function`

Running `dotnet new lambda.DurableFunction` should produce a buildable, testable, deployable project in under 30 seconds.

---

## Implementation plan

| Workstream | Scope | Estimate |
|------------|-------|----------|
| **Durable execution runtime** | Core SDK: replay engine, all context operations (step, wait, callback, invoke, parallel, map), checkpoint batching, retry, logging | ~5-6 weeks |
| **Annotations / source generator** | `[DurableExecution]` attribute, handler wrapper codegen, CloudFormation DurableConfig + IAM policy generation | ~2 weeks |
| **Testing SDK** | Local test runner (in-memory, time-skipping), cloud test runner, step inspection API | ~1.5 weeks |
| **Blueprints, docs, examples** | `dotnet new` project templates, developer guide, API reference, sample projects | ~2 weeks |
| **Roslyn analyzers** (P1 follow-up) | Static analysis detecting non-determinism, nesting violations, closure mutations | ~2 weeks |

**Total: ~10-11 weeks (1 engineer familiar with the Python/JS SDKs)** + Roslyn analyzers as follow-up

### Roslyn Analyzers (P1 Follow-up)

> **Reference implementation:** JavaScript ESLint plugin — [no-non-deterministic-outside-step](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js-eslint-plugin/src/rules/no-non-deterministic-outside-step/no-non-deterministic-outside-step.ts) | [no-nested-durable-operations](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js-eslint-plugin/src/rules/no-nested-durable-operations/no-nested-durable-operations.ts) | [no-closure-in-durable-operations](https://github.com/aws/aws-durable-execution-sdk-js/blob/main/packages/aws-durable-execution-sdk-js-eslint-plugin/src/rules/no-closure-in-durable-operations/no-closure-in-durable-operations.ts)

Ship as a separate NuGet package: `Amazon.Lambda.DurableExecution.Analyzers`

The JavaScript SDK ships an ESLint plugin (`@aws/durable-execution-sdk-js-eslint-plugin`) with three rules that catch the most common durable execution mistakes at author time. The .NET equivalent uses Roslyn diagnostic analyzers:

| Diagnostic ID | Severity | Rule | Rationale |
|---------------|----------|------|-----------|
| DE001 | Warning | `DateTime.Now`, `DateTime.UtcNow`, `Guid.NewGuid()`, `Random.Next()`, `Random.Shared`, `Environment.TickCount` used outside a `StepAsync` body | Non-deterministic values produce different results on replay, breaking checkpoint consistency |
| DE002 | Error | Calling `context.StepAsync`, `WaitAsync`, `ParallelAsync`, `MapAsync`, `InvokeAsync`, `RunInChildContextAsync`, `CreateCallbackAsync`, or `WaitForCallbackAsync` inside a `StepAsync` lambda | Steps are leaf operations — nesting durable operations inside a step produces unpredictable behavior |
| DE003 | Warning | Mutable variable captured by a `StepAsync` lambda and written to inside the lambda body | On replay the step returns cached result without executing, so the write never happens — the outer variable has stale state |
| DE004 | Info | `Task.WhenAll` or `Task.WhenAny` called with tasks returned by durable context methods | Suggest using `ParallelAsync` for completion policies, nesting control, and observability |

These analyzers run at compile time in the IDE (IntelliSense squiggles) and during `dotnet build`, preventing the most confusing class of runtime failures.

---

## Cross-SDK API comparison

All four SDKs expose the same core operations. The differences are naming conventions, parameter ordering, and concurrency model.

| Operation | .NET | Python | JavaScript | Java |
|-----------|------|--------|------------|------|
| Step | `context.StepAsync(func, name?, config?)` | `context.step(func, name?, config?)` | `context.step(name?, fn, config?)` → `DurablePromise<T>` | `context.step(name, type, func, config?)` (blocking) / `context.stepAsync(...)` → `DurableFuture<T>` |
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
| Jitter strategy | `JitterStrategy` enum on `Exponential()` | `jitter_strategy` on `RetryStrategyConfig` | `jitter` on `createRetryStrategy()` |
| Retry presets | `RetryStrategy.None/Default/Transient` | `RetryPresets.none()/default()/transient()` | `retryPresets.default/linear/noRetry` |
| Nesting type | `NestingType` on `ParallelConfig`/`MapConfig` | `NestingType` on parallel/map config | `NestingType` on parallel/map config |
| Item batching | `ItemBatcher` on `MapConfig` | `ItemBatcher` on `MapConfig` | *(checkpoint manager handles batching)* |
| Item namer | `ItemNamer` on `MapConfig` | Item naming function on `MapConfig` | `itemNamer` on `MapConfig` |
| Error mapping | `ErrorMapping` on `ChildContextConfig` | *(typed exception wrapping)* | `errorMapping` on child context config |
| Message-based retry filter | `retryableMessagePatterns` (regex) | `retryable_errors` (regex) | `retryableErrors` (RegExp[]) |
| Step context / scoped logger | `IStepContext` with `Logger`, `AttemptNumber` | `StepContext` with `logger` | `ctx` with `logger` in step callback |
| Named parallel branches | `DurableBranch<T>(name, func)` | Function `__name__` | `{ name, func }` objects |
| Inline retry lambda | `Func<Exception, int, RetryDecision>` | `Callable[[Exception, int], RetryDecision]` | `(error, attempt) => RetryDecision` |
| Static analysis | Roslyn analyzers (P1 follow-up) | *(N/A)* | ESLint plugin (3 rules) |
| Cloud test runner | `CloudDurableTestRunner<TIn,TOut>` | `pytest --runner-mode=cloud` | `CloudDurableTestRunner` |

**Key differences:**

- **Concurrency model:** JS returns `DurablePromise<T>` (lazy, deferred until awaited). Python is synchronous (blocks the thread). Java exposes both `step` (blocking) and `stepAsync` (returns `DurableFuture<T>`). .NET returns `Task<T>` (standard async/await). Note: `Task.WhenAll` works with durable operations but `ParallelAsync`/`MapAsync` are preferred for completion policies and observability.
- **Why .NET ships only the async form:** Java's two-API split exists because Java has no language-level `await` — `step` is the simple blocking ergonomic, `stepAsync` is the composable form. In .NET, `Task<T>` is *already* both: `await context.StepAsync(...)` reads as sequential code, and `Task.WhenAll(...)` composes concurrently. A `Step` (blocking, returns `T`) overload would do nothing except call `.GetAwaiter().GetResult()` on the async version, which is also a Lambda-thread anti-pattern (deadlock-prone, blocks a thread the runtime needs). So .NET intentionally has one shape — `*Async` — matching the rest of `IAmazonLambda` and the broader .NET async convention. Python is single-shape for the same reason in reverse: no async runtime in scope, so blocking is the only ergonomic shape.
- **Step function signature:** Python and JS only expose `Func<IStepContext, ...>` — the user always receives a step context. Java has both `Function<StepContext, T>` and `Supplier<T>` overloads, but the `Supplier<T>` ones are deprecated (*"use the variants accepting StepContext instead"*). .NET follows Python/JS: `IStepContext` is always passed.
- **Name parameter position:** JS puts `name` first; Python, Java, and .NET put it after the function/duration.
- **Parallel semantics in JS:** JS uses `context.promise.all/any/race/allSettled` to combine DurablePromises. .NET, Python, and Java use `CompletionConfig` on the `Parallel`/`Map` operations instead.
- **.NET-only:** `CancellationToken` on every method (standard .NET pattern).
- **Jitter default:** All four SDKs default to full jitter on retry strategies.

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
            async (step) => await InvokeModel(messages),
            name: "invoke_model");

        if (response.ToolCall == null)
            return response.Content;

        var toolResult = await context.StepAsync(
            async (step) => await ExecuteTool(response.ToolCall),
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
        async (step) => await AnalyzeDocument(input.DocumentUrl),
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
            Timeout = TimeSpan.FromDays(7),
            HeartbeatTimeout = TimeSpan.FromHours(24)
        });

    if (review.Approved)
    {
        await context.StepAsync(
            async (step) => await PublishDocument(input.DocumentUrl),
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
        async (step) => await ExtractFromSource(input.SourceId),
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
                async (step) => await TransformChunk(chunk),
                name: $"transform_{index}");
        },
        name: "transform_all",
        config: new MapConfig { MaxConcurrency = 10 });

    transformed.ThrowIfError();

    // Load
    var loadResult = await context.StepAsync(
        async (step) => await LoadToDestination(transformed.GetResults()),
        name: "load",
        config: new StepConfig
        {
            Semantics = StepSemantics.AtMostOncePerRetry
        });

    // Wait before next run
    await context.WaitAsync(TimeSpan.FromHours(1), name: "schedule_delay");

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
