using Amazon.Lambda.DurableExecution.Testing;
using Xunit;

namespace BlueprintBaseName._1.Tests;

public class FunctionTest
{
    [Fact]
    public async Task ProcessOrder_ShipsOrder()
    {
        var function = new Function();

        // The local runner drives the workflow to completion in-process using the real durable
        // runtime with an in-memory backend. SkipTime collapses the settlement WaitAsync delay so
        // the test does not actually block for 5 seconds.
        await using var runner = new DurableTestRunner<OrderRequest, OrderResult>(
            handler: function.ProcessOrder,
            options: new TestRunnerOptions { SkipTime = true });

        var input = new OrderRequest
        {
            OrderId = "order-123",
            Items = new[] { "sku-1", "sku-2" },
        };

        var result = await runner.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken);

        result.EnsureSucceeded();
        Assert.NotNull(result.Result);
        Assert.Equal("order-123", result.Result!.OrderId);
        Assert.Equal("shipped", result.Result.Status);
        Assert.Equal(2, result.Result.ItemCount);
        Assert.Equal("txn-order-123", result.Result.TransactionId);
        Assert.Equal("trk-order-123", result.Result.TrackingId);

        // Each named operation is checkpointed and inspectable.
        Assert.Equal(OperationStatus.Succeeded, result.GetStep("validate_order").Status);
        Assert.Equal(OperationStatus.Succeeded, result.GetStep("charge_payment").Status);
    }

    [Fact]
    public async Task ProcessOrder_EmptyOrder_Fails()
    {
        var function = new Function();

        await using var runner = new DurableTestRunner<OrderRequest, OrderResult>(
            handler: function.ProcessOrder,
            options: new TestRunnerOptions { SkipTime = true });

        var input = new OrderRequest { OrderId = "order-456", Items = System.Array.Empty<string>() };

        var result = await runner.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsFailed);
    }
}
