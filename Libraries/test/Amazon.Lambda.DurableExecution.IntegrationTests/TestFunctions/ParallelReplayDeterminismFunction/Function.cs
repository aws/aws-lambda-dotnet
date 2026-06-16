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
        // Three branches. Each branch generates a fresh GUID inside a step,
        // then does a durable wait. The wait forces a suspend/resume cycle,
        // so the second invocation MUST replay the cached GUID rather than
        // re-running the step. If replay determinism is broken, the GUID
        // would change between the original execution and replay.
        var batch = await context.ParallelAsync(
            new[]
            {
                new DurableBranch<string>("a", BranchAsync),
                new DurableBranch<string>("b", BranchAsync),
                new DurableBranch<string>("c", BranchAsync),
            },
            name: "fanout");

        var joined = string.Join(",", batch.GetResults());
        return new TestResult { Status = "completed", Data = joined };
    }

    private static async Task<string> BranchAsync(IDurableContext ctx, System.Threading.CancellationToken cancellationToken)
    {
        var generatedId = await ctx.StepAsync(
            async (_, _) => { await Task.CompletedTask; return Guid.NewGuid().ToString(); },
            name: "generate");

        // Force a suspend/resume cycle to trigger replay of the parallel.
        await ctx.WaitAsync(TimeSpan.FromSeconds(2), name: "boundary");

        return generatedId;
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
