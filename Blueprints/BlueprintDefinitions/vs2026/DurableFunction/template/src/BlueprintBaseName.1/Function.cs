using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Microsoft.Extensions.Logging;

// Durable execution uses the Lambda Annotations programming model in the CLASS-LIBRARY variant:
// there is no hand-written Main. The Amazon.Lambda.Annotations source generator turns the
// [LambdaFunction] + [DurableExecution] method below into a handler wrapper that delegates to
// Amazon.Lambda.DurableExecution.DurableFunction.WrapAsync. The managed dotnet10 runtime hosts its
// own bootstrap, resolves the serializer from the assembly attribute here, and invokes the
// generated wrapper directly.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace BlueprintBaseName._1;

/// <summary>
/// A durable order-processing workflow. A single invocation reads like one straight-line method,
/// but durable execution checkpoints every operation, so the function can be suspended (during the
/// settlement wait) and re-invoked without re-running completed work. If the process crashes
/// mid-flight it resumes from the last checkpoint — no lost orders, no double charges.
/// </summary>
public class Function
{
    /// <summary>
    /// The durable workflow entry point. The method signature is
    /// <c>(TInput, IDurableContext) -&gt; Task&lt;TOutput&gt;</c>; the source generator wires it to the
    /// durable runtime and emits the matching CloudFormation resource (with DurableConfig and the
    /// durable IAM policy) in serverless.template.
    /// </summary>
    [LambdaFunction]
    [DurableExecution]
    public async Task<OrderResult> ProcessOrder(OrderRequest order, IDurableContext context)
    {
        // The durable logger is replay-aware: this line is emitted once, not once per replay.
        context.Logger.LogInformation("Processing order {OrderId}", order.OrderId);

        // 1) VALIDATE — a plain step. The result is checkpointed; on replay the cached value is
        //    returned instead of re-running the body.
        var itemCount = await context.StepAsync(
            async (_, _) =>
            {
                await Task.CompletedTask;
                if (order.Items is null || order.Items.Length == 0)
                    throw new InvalidOperationException("Order has no items.");
                return order.Items.Length;
            },
            name: "validate_order");

        // 2) CHARGE PAYMENT — a step with a retry policy. Payment gateways are flaky, so the SDK
        //    transparently retries with exponential backoff and checkpoints only the successful
        //    attempt. AtMostOncePerRetry avoids double-charging if Lambda is re-invoked mid-attempt.
        var transactionId = await context.StepAsync(
            async (_, _) =>
            {
                await Task.CompletedTask;
                return $"txn-{order.OrderId}";
            },
            name: "charge_payment",
            config: new StepConfig
            {
                RetryStrategy = RetryStrategy.Exponential(
                    maxAttempts: 5,
                    initialDelay: TimeSpan.FromSeconds(2),
                    maxDelay: TimeSpan.FromSeconds(30),
                    backoffRate: 2.0),
                Semantics = StepSemantics.AtMostOncePerRetry,
            });

        // 3) SETTLEMENT WAIT — suspend the workflow for a fixed delay. While suspended there is no
        //    compute charge; the runtime re-invokes the function when the timer fires.
        await context.WaitAsync(TimeSpan.FromSeconds(5), name: "settlement_delay");

        // 4) SHIP — group related steps into a single child context. The packing and labeling steps
        //    are checkpointed together as one logical operation.
        var trackingId = await context.RunInChildContextAsync(
            async (childContext, _) =>
            {
                await childContext.StepAsync(
                    async (_, _) => { await Task.CompletedTask; return "packed"; },
                    name: "pack");

                return await childContext.StepAsync(
                    async (_, _) => { await Task.CompletedTask; return $"trk-{order.OrderId}"; },
                    name: "label");
            },
            name: "ship_order");

        context.Logger.LogInformation("Order {OrderId} shipped: {TrackingId}", order.OrderId, trackingId);

        return new OrderResult
        {
            OrderId = order.OrderId,
            Status = "shipped",
            ItemCount = itemCount,
            TransactionId = transactionId,
            TrackingId = trackingId,
        };
    }
}

/// <summary>Input payload for the workflow.</summary>
public class OrderRequest
{
    public string? OrderId { get; set; }
    public string[]? Items { get; set; }
}

/// <summary>Output payload returned when the workflow completes.</summary>
public class OrderResult
{
    public string? OrderId { get; set; }
    public string? Status { get; set; }
    public int ItemCount { get; set; }
    public string? TransactionId { get; set; }
    public string? TrackingId { get; set; }
}
