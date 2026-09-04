// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

/// <summary>
/// Deployed entry point exercising the incremental, heterogeneous branch API
/// (<see cref="IDurableContext.CreateParallel"/>). Three branches return three
/// unrelated types (a string, an int, and a POCO); each is retrieved through its
/// own typed <see cref="IParallelBranch{T}"/> handle with no shared base type,
/// cast, or envelope. Validates that heterogeneous per-branch results round-trip
/// through the service checkpoint history end-to-end.
/// </summary>
public class Function
{
    public static async Task Main(string[] args)
    {
        var handler = new Function();
        var serializer = new DefaultLambdaJsonSerializer();
        using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<DurableExecutionInvocationInput, DurableExecutionInvocationOutput>(handler.Handler, serializer);
        using var bootstrap = new LambdaBootstrap(handlerWrapper);
        await bootstrap.RunAsync();
    }

    public Task<DurableExecutionInvocationOutput> Handler(
        DurableExecutionInvocationInput input, ILambdaContext context)
        => DurableFunction.WrapAsync<OrderRequest, OrderResult>(Workflow, input, context);

    private static async Task<OrderResult> Workflow(OrderRequest input, IDurableContext context)
    {
        var orderId = input?.OrderId ?? "unknown";

        await using var parallel = context.CreateParallel(name: "process-order");

        // Each branch declares its own result type — string, int, and Money.
        IParallelBranch<string> inventory = parallel.Branch(
            "inventory",
            async (branch, ct) => await branch.StepAsync(
                (_, _) => Task.FromResult($"reserved-{orderId}"), name: "reserve"));

        IParallelBranch<int> payment = parallel.Branch(
            "payment",
            async (branch, ct) => await branch.StepAsync(
                (_, _) => Task.FromResult(200), name: "charge"));

        IParallelBranch<Money> shipping = parallel.Branch(
            "shipping",
            async (branch, ct) => await branch.StepAsync(
                (_, _) => Task.FromResult(new Money { Currency = "USD", Amount = 4200 }), name: "quote"));

        IBatchResult summary = await parallel.CompleteAsync();

        var reservedInventory = await inventory;
        var authorizedPayment = await payment;
        var shippingQuote = await shipping;

        return new OrderResult
        {
            Inventory = reservedInventory,
            Payment = authorizedPayment,
            Shipping = $"{shippingQuote.Currency}:{shippingQuote.Amount}",
            SuccessCount = summary.SuccessCount,
            TotalCount = summary.TotalCount,
        };
    }
}

public class OrderRequest
{
    public string? OrderId { get; set; }
}

public class OrderResult
{
    public string Inventory { get; set; } = "";
    public int Payment { get; set; }
    public string Shipping { get; set; } = "";
    public int SuccessCount { get; set; }
    public int TotalCount { get; set; }
}

public class Money
{
    public string Currency { get; set; } = "";
    public int Amount { get; set; }
}
