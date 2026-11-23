// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.ConformanceTests.Otel;

internal static class Otel5ChildContext
{
    public static readonly Scenario Scenario = new(nameof(Otel5ChildContext), "child-context", async (_, context) =>
        await context.RunInChildContextAsync(
            (child, _) => child.StepAsync((_, _) => Task.FromResult("child-complete"), name: "otel-child-step"),
            name: "otel-child-context",
            // The .NET SDK sends no subtype by default; the shared requirements expect the one other SDKs send.
            config: new ChildContextConfig { SubType = "RunInChildContext" }));
}

internal static class Otel6Parallel
{
    public static readonly Scenario Scenario = new(nameof(Otel6Parallel), "parallel-hierarchy", async (_, context) =>
    {
        var result = await context.ParallelAsync(
            new[]
            {
                new DurableBranch<string>("otel-parallel-branch-a", (branch, _) =>
                    branch.StepAsync((_, _) => Task.FromResult("a"), name: "otel-parallel-step-a")),
                new DurableBranch<string>("otel-parallel-branch-b", (branch, _) =>
                    branch.StepAsync((_, _) => Task.FromResult("b"), name: "otel-parallel-step-b")),
            },
            name: "otel-parallel",
            config: new ParallelConfig { MaxConcurrency = 1 });
        return result.All.Select(item => item.Result).ToList();
    });
}

internal static class Otel7Map
{
    public static readonly Scenario Scenario = new(nameof(Otel7Map), "map-hierarchy", async (_, context) =>
    {
        var result = await context.MapAsync(
            new[] { 1, 2 },
            (iteration, item, index, _, _) =>
                iteration.StepAsync((_, _) => Task.FromResult(item * 2), name: $"otel-map-step-{index}"),
            name: "otel-map",
            config: new MapConfig<int> { MaxConcurrency = 1, ItemNamer = (_, index) => $"otel-map-iteration-{index}" });
        return result.All.Select(item => item.Result).ToList();
    });
}

internal static class Otel8HandledFailure
{
    public static readonly Scenario Scenario = new(nameof(Otel8HandledFailure), "handled-failure", async (_, context) =>
    {
        try
        {
            await context.StepAsync<string>(
                (_, _) => throw new InvalidOperationException("Intentional handled failure"),
                name: "otel-handled-failure",
                config: new StepConfig { RetryStrategy = RetryStrategy.None });
        }
        catch (StepException)
        {
        }
        return await context.StepAsync((_, _) => Task.FromResult("recovered"), name: "otel-recovery-step");
    });
}

internal static class Otel9WaitForCondition
{
    public static readonly Scenario Scenario = new(nameof(Otel9WaitForCondition), "wait-for-condition", async (_, context) =>
        await context.WaitForConditionAsync(
            (state, _, _) => Task.FromResult(state + 1),
            new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.FromDelegate<int>((state, _) =>
                    state >= 2 ? WaitDecision.Stop() : WaitDecision.ContinueAfter(TimeSpan.FromSeconds(1))),
            },
            name: "otel-condition"));
}
