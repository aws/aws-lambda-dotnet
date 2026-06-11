// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

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
        => DurableFunction.WrapAsync<TestEvent, TestResult>(Workflow, input, context);

    private async Task<TestResult> Workflow(TestEvent input, IDurableContext context)
    {
        // Four branches with different durable wait durations. The shortest
        // wait should win and short-circuit the parallel via FirstSuccessful.
        // Wait durations are at least 1s (service timer granularity).
        var batch = await context.ParallelAsync(
            new[]
            {
                new DurableBranch<int>("slowest", async (ctx, _) =>
                {
                    await ctx.WaitAsync(TimeSpan.FromSeconds(8), name: "wait_3");
                    return 3;
                }),
                new DurableBranch<int>("fastest", async (ctx, _) =>
                {
                    await ctx.WaitAsync(TimeSpan.FromSeconds(1), name: "wait_0");
                    return 0;
                }),
                new DurableBranch<int>("mid1", async (ctx, _) =>
                {
                    await ctx.WaitAsync(TimeSpan.FromSeconds(5), name: "wait_1");
                    return 1;
                }),
                new DurableBranch<int>("mid2", async (ctx, _) =>
                {
                    await ctx.WaitAsync(TimeSpan.FromSeconds(6), name: "wait_2");
                    return 2;
                }),
            },
            name: "race",
            config: new ParallelConfig { CompletionConfig = CompletionConfig.FirstSuccessful() });

        // The winner is whichever branch came back first. Surface the index +
        // its name so the test can assert one branch won.
        var winner = batch.Succeeded.FirstOrDefault();
        return new TestResult
        {
            Status = "completed",
            WinnerIndex = winner?.Index ?? -1,
            WinnerName = winner?.Name,
            CompletionReason = batch.CompletionReason.ToString(),
            SuccessCount = batch.SuccessCount,
            StartedCount = batch.StartedCount
        };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult
{
    public string? Status { get; set; }
    public int WinnerIndex { get; set; }
    public string? WinnerName { get; set; }
    public string? CompletionReason { get; set; }
    public int SuccessCount { get; set; }
    public int StartedCount { get; set; }
}
