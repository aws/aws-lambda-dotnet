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
- `ctx.CreateCallbackAsync` / `ctx.WaitForCallbackAsync` — wait for external events (approvals, webhooks). ([docs](docs/core/callbacks.md))
- `ctx.RunInChildContextAsync` — run an isolated child context with its own checkpoint log. ([docs](docs/core/child-contexts.md))

## Quick Start

### Installation

```bash
dotnet add package Amazon.Lambda.DurableExecution
```

### Your first durable function (managed runtime)

A complete order-processing workflow with two steps and a wait, deployed as a class library on a managed .NET runtime such as `dotnet8`. Register the serializer once with the `LambdaSerializer` assembly attribute and configure your handler as `MyAssembly::OrderProcessor.OrderProcessor::Handler`. `DurableFunction.WrapAsync` resolves the serializer from `ILambdaContext.Serializer`, which the managed runtime populates from the assembly attribute.

```csharp
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.Serialization.SystemTextJson;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace OrderProcessor;

public class OrderProcessor
{
    // Lambda handler: OrderProcessor::OrderProcessor.OrderProcessor::Handler
    public Task<DurableExecutionInvocationOutput> Handler(
        DurableExecutionInvocationInput input, ILambdaContext context)
        => DurableFunction.WrapAsync<Order, OrderResult>(Workflow, input, context);

    private async Task<OrderResult> Workflow(Order order, IDurableContext ctx)
    {
        var reservation = await ctx.StepAsync(
            async _ => await InventoryService.ReserveAsync(order.Items),
            name: "reserve-inventory");

        var payment = await ctx.StepAsync(
            async _ => await PaymentService.ChargeAsync(order.PaymentMethod, order.Total),
            name: "process-payment");

        await ctx.WaitAsync(TimeSpan.FromHours(2), name: "warehouse-processing");

        var shipment = await ctx.StepAsync(
            async _ => await ShippingService.ShipAsync(reservation, order.Address),
            name: "confirm-shipment");

        return new OrderResult(order.Id, shipment.TrackingNumber);
    }
}

public record Order(string Id, IReadOnlyList<OrderItem> Items, PaymentMethod PaymentMethod, decimal Total, Address Address);
public record OrderResult(string OrderId, string TrackingNumber);
```

For AOT or trim-friendly serialization, swap `DefaultLambdaJsonSerializer` for `SourceGeneratorLambdaJsonSerializer<TContext>` and register your `JsonSerializerContext`.

### Executable assembly variant (custom runtime)

If you target a custom runtime (`provided.al2023`) and deploy your function as an executable assembly, host it with `LambdaBootstrap` and pass the serializer in code instead of via an assembly attribute:

```csharp
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

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
        // ... same workflow body as above ...
    }
}
```

## Documentation

**Core operations**

- [Steps](docs/core/steps.md) — execute code with automatic checkpointing, retry strategies, and at-least/at-most-once semantics.
- [Wait](docs/core/wait.md) — pause execution without compute charges.
- [Callbacks](docs/core/callbacks.md) — wait for external systems to respond.
- [Child Contexts](docs/core/child-contexts.md) — group related operations into isolated, checkpointed units.

**Examples**

End-to-end test functions (each paired with an integration test) live under `Libraries/test/Amazon.Lambda.DurableExecution.IntegrationTests/TestFunctions/`.

## Related SDKs

- [aws-durable-execution-sdk-java](https://github.com/aws/aws-durable-execution-sdk-java) — Java SDK
- [aws-durable-execution-sdk-js](https://github.com/aws/aws-durable-execution-sdk-js) — JavaScript / TypeScript SDK
- [aws-durable-execution-sdk-python](https://github.com/aws/aws-durable-execution-sdk-python) — Python SDK
