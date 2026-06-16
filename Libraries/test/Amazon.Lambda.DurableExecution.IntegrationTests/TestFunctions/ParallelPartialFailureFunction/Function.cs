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
        var batch = await context.ParallelAsync(
            new[]
            {
                new DurableBranch<string>("ok1", async (_, _) => { await Task.CompletedTask; return "first"; }),
                new DurableBranch<string>("boom", async (_, _) =>
                {
                    await Task.CompletedTask;
                    throw new InvalidOperationException("intentional partial failure");
                }),
                new DurableBranch<string>("ok2", async (_, _) => { await Task.CompletedTask; return "third"; }),
            },
            name: "partial",
            // AllCompleted: drive every branch to terminal state regardless of failure.
            // Without this, the default AllSuccessful() would throw on the first failure.
            config: new ParallelConfig { CompletionConfig = CompletionConfig.AllCompleted() });

        var errors = batch.GetErrors();
        var errorSummary = string.Join("|", errors.Select(e => $"{e.GetType().Name}:{e.Message}"));

        return new TestResult
        {
            Status = "completed",
            SuccessCount = batch.SuccessCount,
            FailureCount = batch.FailureCount,
            ErrorSummary = errorSummary
        };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult
{
    public string? Status { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string? ErrorSummary { get; set; }
}
