using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Microsoft.Extensions.Logging;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace BlueprintBaseName._1;

public class Function
{
    [LambdaFunction]
    [DurableExecution(executionTimeout: 86400)]
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
