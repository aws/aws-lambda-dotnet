# AWS Lambda Durable Execution SDK for .NET

> **Preview.** `Amazon.Lambda.DurableExecution` is in active development (0.x). Public APIs may change before 1.0.

`Amazon.Lambda.DurableExecution` is the .NET SDK for building resilient, long-running AWS Lambda functions that automatically checkpoint progress and resume after failures. Workflows can run for up to one year, with charges only for active compute time.

## Key Features

- **Automatic checkpointing** — progress is saved after each step; failures resume from the last checkpoint.
- **Cost-effective waits** — suspend execution for minutes, hours, or days without compute charges.
- **Configurable retries** — built-in retry strategies with exponential backoff and jitter.
- **Replay safety** — functions deterministically resume from checkpoints after interruptions.
- **Type safety** — full generic type support for step results.
- **AOT-friendly** — pluggable `ILambdaSerializer` so you can register `SourceGeneratorLambdaJsonSerializer<TContext>` for trimmed / Native AOT functions.

## How It Works

Your handler delegates to `DurableFunction.WrapAsync`, which gives your workflow function an `IDurableContext`. The context is your interface to durable operations:

- `ctx.StepAsync` — run code and checkpoint the result. ([docs](docs/core/steps.md))
- `ctx.WaitAsync` — suspend execution without compute charges. ([docs](docs/core/wait.md))
- `ctx.WaitForConditionAsync` — poll a check function until a condition is met, suspending between polls. ([docs](docs/core/wait-for-condition.md))
- `ctx.CreateCallbackAsync` / `ctx.WaitForCallbackAsync` — wait for external events (approvals, webhooks). ([docs](docs/core/callbacks.md))
- `ctx.RunInChildContextAsync` — run an isolated child context with its own checkpoint log. ([docs](docs/core/child-contexts.md))
- `ctx.ParallelAsync` — run independent branches concurrently and aggregate their results. ([docs](docs/core/parallel.md))
- Every user `Func` receives a `CancellationToken` linking the caller's token with the SDK's workflow-shutdown signal. ([docs](docs/core/cancellation.md))

## Quick Start

### Installation

```bash
dotnet add package Amazon.Lambda.DurableExecution
```

### Your first durable function

> **Programming model:** durable functions support both the **executable programming model** (shown below) and the **class-library programming model** on the managed `dotnet10` runtime. See [the class-library variant](#class-library-programming-model) below.

A complete order-processing workflow with two steps and a wait, deployed as an executable assembly on the `dotnet10` runtime. `Main` builds a `LambdaBootstrap` with your handler and an `ILambdaSerializer`, and `DurableFunction.WrapAsync` uses that serializer to checkpoint step inputs and outputs.

```csharp
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace OrderProcessor;

public class OrderProcessor
{
    public static async Task Main()
    {
        var handler = new OrderProcessor();
        var serializer = new DefaultLambdaJsonSerializer();
        using var wrapper = HandlerWrapper.GetHandlerWrapper<DurableExecutionInvocationInput, DurableExecutionInvocationOutput>(
            handler.Handler, serializer);
        using var bootstrap = new LambdaBootstrap(wrapper);
        await bootstrap.RunAsync();
    }

    public Task<DurableExecutionInvocationOutput> Handler(
        DurableExecutionInvocationInput input, ILambdaContext context)
        => DurableFunction.WrapAsync<Order, OrderResult>(Workflow, input, context);

    private async Task<OrderResult> Workflow(Order order, IDurableContext ctx)
    {
        var reservation = await ctx.StepAsync(
            async (_, ct) => await InventoryService.ReserveAsync(order.Items, ct),
            name: "reserve-inventory");

        var payment = await ctx.StepAsync(
            async (_, ct) => await PaymentService.ChargeAsync(order.PaymentMethod, order.Total, ct),
            name: "process-payment");

        await ctx.WaitAsync(TimeSpan.FromHours(2), name: "warehouse-processing");

        var shipment = await ctx.StepAsync(
            async (_, ct) => await ShippingService.ShipAsync(reservation, order.Address, ct),
            name: "confirm-shipment");

        return new OrderResult(order.Id, shipment.TrackingNumber);
    }
}

public record Order(string Id, IReadOnlyList<OrderItem> Items, PaymentMethod PaymentMethod, decimal Total, Address Address);
public record OrderResult(string OrderId, string TrackingNumber);
```

For AOT or trim-friendly serialization, swap `DefaultLambdaJsonSerializer` for `SourceGeneratorLambdaJsonSerializer<TContext>` and register your `JsonSerializerContext`.

### Class-library programming model

On the managed `dotnet10` runtime you can skip the `Main`/`LambdaBootstrap` loop entirely and deploy a plain class-library handler — the same model used by non-durable Lambda functions. Declare the serializer with an assembly attribute and deploy with the `Assembly::Namespace.Type::Method` handler string (e.g. `OrderProcessor::OrderProcessor.OrderProcessor::Handler`):

```csharp
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.Serialization.SystemTextJson;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace OrderProcessor;

public class OrderProcessor
{
    public Task<DurableExecutionInvocationOutput> Handler(
        DurableExecutionInvocationInput input, ILambdaContext context)
        => DurableFunction.WrapAsync<Order, OrderResult>(Workflow, input, context);

    private async Task<OrderResult> Workflow(Order order, IDurableContext ctx)
    {
        // ...same workflow body as above...
    }
}
```

The project is a normal Lambda class library; the managed runtime supplies the bootstrap loop and invokes `Handler` directly.

## Documentation

**Core operations**

- [Steps](docs/core/steps.md) — execute code with automatic checkpointing, retry strategies, and at-least/at-most-once semantics.
- [Wait](docs/core/wait.md) — pause execution without compute charges.
- [Wait For Condition](docs/core/wait-for-condition.md) — poll until a condition is met, suspending between polls with a configurable wait strategy.
- [Callbacks](docs/core/callbacks.md) — wait for external systems to respond.
- [Child Contexts](docs/core/child-contexts.md) — group related operations into isolated, checkpointed units.
- [Parallel](docs/core/parallel.md) — fan out independent branches concurrently with configurable concurrency and completion policies.

**Examples**

End-to-end test functions (each paired with an integration test) live under `Libraries/test/Amazon.Lambda.DurableExecution.IntegrationTests/TestFunctions/`.

## Related SDKs

- [aws-durable-execution-sdk-java](https://github.com/aws/aws-durable-execution-sdk-java) — Java SDK
- [aws-durable-execution-sdk-js](https://github.com/aws/aws-durable-execution-sdk-js) — JavaScript / TypeScript SDK
- [aws-durable-execution-sdk-python](https://github.com/aws/aws-durable-execution-sdk-python) — Python SDK
