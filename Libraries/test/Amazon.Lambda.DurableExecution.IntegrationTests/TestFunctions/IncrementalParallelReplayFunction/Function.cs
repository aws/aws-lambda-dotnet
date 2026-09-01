// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

/// <summary>
/// Deployed entry point exercising deterministic replay of the incremental
/// (<see cref="IDurableContext.CreateParallel"/>) API across two distinct replay
/// paths:
/// <list type="number">
///   <item>Each branch does a step (generating a GUID) then a durable wait. The
///       wait suspends the whole invocation, so the parallel re-runs with its
///       parent CONTEXT still STARTED — branches replay from their own checkpoints
///       and the cached GUID must survive.</item>
///   <item>After the parallel completes, a second durable wait suspends again. On
///       that resume the parent CONTEXT is already SUCCEEDED, so the incremental
///       operation takes the terminal-reconstruct path: branch handles resolve
///       from the frozen inline summary WITHOUT re-running, and the aggregate is
///       rebuilt from the checkpoint.</item>
/// </list>
/// If replay determinism were broken, the per-branch GUIDs would change between
/// invocations, or a branch step would re-execute (surfacing as duplicate
/// StepSucceeded events).
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
        => DurableFunction.WrapAsync<TestEvent, TestResult>(Workflow, input, context);

    private static async Task<TestResult> Workflow(TestEvent input, IDurableContext context)
    {
        await using var parallel = context.CreateParallel(name: "fanout");

        IParallelBranch<string> a = parallel.BranchAsync("a", BranchAsync);
        IParallelBranch<string> b = parallel.BranchAsync("b", BranchAsync);
        IParallelBranch<string> c = parallel.BranchAsync("c", BranchAsync);

        var summary = await parallel.CompleteAsync();

        // Retrieve each branch's typed result through its own handle.
        var joined = string.Join(",", await a, await b, await c);

        // Force a resume where the parallel is ALREADY terminal, so CreateParallel
        // takes the terminal-reconstruct path on the next invocation.
        await context.WaitAsync(TimeSpan.FromSeconds(2), name: "post-boundary");

        return new TestResult
        {
            Status = "completed",
            Data = joined,
            SuccessCount = summary.SuccessCount
        };
    }

    private static async Task<string> BranchAsync(IDurableContext ctx, CancellationToken cancellationToken)
    {
        var generatedId = await ctx.StepAsync(
            async (_, _) => { await Task.CompletedTask; return Guid.NewGuid().ToString(); },
            name: "generate");

        // Suspend/resume cycle so the parallel replays with its parent still STARTED.
        await ctx.WaitAsync(TimeSpan.FromSeconds(2), name: "boundary");

        return generatedId;
    }
}

public class TestEvent { public string? OrderId { get; set; } }

public class TestResult
{
    public string? Status { get; set; }
    public string? Data { get; set; }
    public int SuccessCount { get; set; }
}
