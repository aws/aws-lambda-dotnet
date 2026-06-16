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
        // Five branches, two throw. ToleratedFailureCount = 1 means a second
        // failure exceeds tolerance and the parallel surfaces a ParallelException.
        var batch = await context.ParallelAsync(
            new[]
            {
                new DurableBranch<string>("ok1", async (_, _) => { await Task.CompletedTask; return "1"; }),
                new DurableBranch<string>("bad1", async (_, _) =>
                {
                    await Task.CompletedTask;
                    throw new InvalidOperationException("bad1 boom");
                }),
                new DurableBranch<string>("ok2", async (_, _) => { await Task.CompletedTask; return "2"; }),
                new DurableBranch<string>("bad2", async (_, _) =>
                {
                    await Task.CompletedTask;
                    throw new InvalidOperationException("bad2 boom");
                }),
                new DurableBranch<string>("ok3", async (_, _) => { await Task.CompletedTask; return "3"; }),
            },
            name: "tolerance",
            config: new ParallelConfig
            {
                CompletionConfig = new CompletionConfig { ToleratedFailureCount = 1 }
            });

        // Should not reach here — the parallel must throw ParallelException.
        return new TestResult { Status = "should_not_reach", SuccessCount = batch.SuccessCount };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult
{
    public string? Status { get; set; }
    public int SuccessCount { get; set; }
}
