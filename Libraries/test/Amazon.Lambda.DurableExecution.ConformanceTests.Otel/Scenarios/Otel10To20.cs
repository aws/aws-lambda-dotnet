// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.ConformanceTests.Otel;

// The conformance driver completes or fails these callbacks, so the submitters do nothing.

internal static class Otel10WaitForCallback
{
    public static readonly Scenario Scenario = new(nameof(Otel10WaitForCallback), "wait-for-callback", async (_, context) =>
        await context.WaitForCallbackAsync<string>((_, _, _) => Task.CompletedTask, name: "otel-callback"));
}

internal static class Otel12ChildContextFailure
{
    public static readonly Scenario Scenario = new(nameof(Otel12ChildContextFailure), "child-context-failure", async (_, context) =>
    {
        await context.RunInChildContextAsync<string>(
            (_, _) => throw new InvalidOperationException("Intentional child-context failure"),
            name: "otel-failed-child-context",
            config: new ChildContextConfig { SubType = "RunInChildContext" });
        return null;
    });
}

internal static class Otel13ParallelFailure
{
    public static readonly Scenario Scenario = new(nameof(Otel13ParallelFailure), "parallel-failure", async (_, context) =>
    {
        var result = await context.ParallelAsync(
            new[]
            {
                new DurableBranch<string>("otel-failed-parallel-branch", (_, _) =>
                    throw new InvalidOperationException("Intentional parallel branch failure")),
            },
            name: "otel-failed-parallel",
            config: new ParallelConfig { MaxConcurrency = 1 });
        result.ThrowIfError();
        return null;
    });
}

internal static class Otel14MapFailure
{
    public static readonly Scenario Scenario = new(nameof(Otel14MapFailure), "map-failure", async (_, context) =>
    {
        var result = await context.MapAsync<int, string>(
            new[] { 1 },
            (_, _, _, _, _) => throw new InvalidOperationException("Intentional map iteration failure"),
            name: "otel-failed-map",
            config: new MapConfig<int> { MaxConcurrency = 1, ItemNamer = (_, index) => $"otel-failed-map-iteration-{index}" });
        result.ThrowIfError();
        return null;
    });
}

internal static class Otel15WaitInterrupted
{
    public static readonly Scenario Scenario = new(nameof(Otel15WaitInterrupted), "wait-interrupted", async (_, context) =>
    {
        await context.WaitAsync(TimeSpan.FromSeconds(30), name: "otel-interrupted-wait");
        return null;
    });
}

internal static class Otel16WaitForConditionFailure
{
    public static readonly Scenario Scenario = new(nameof(Otel16WaitForConditionFailure), "wait-for-condition-failure", async (_, context) =>
    {
        await context.WaitForConditionAsync<int>(
            (_, _, _) => throw new InvalidOperationException("Intentional condition check failure"),
            new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.FromDelegate<int>((_, _) => WaitDecision.ContinueAfter(TimeSpan.FromSeconds(1))),
            },
            name: "otel-failed-condition");
        return null;
    });
}

internal static class Otel17WaitForCallbackFailure
{
    public static readonly Scenario Scenario = new(nameof(Otel17WaitForCallbackFailure), "wait-for-callback-failure", async (_, context) =>
    {
        await context.WaitForCallbackAsync<string>((_, _, _) => Task.CompletedTask, name: "otel-failed-callback");
        return null;
    });
}

internal static class Otel19ExecutionFailure
{
    public static readonly Scenario Scenario = new(nameof(Otel19ExecutionFailure), "execution-failure",
        (_, _) => throw new InvalidOperationException("Intentional execution failure"));
}

internal static class Otel20VirtualContext
{
    public static readonly Scenario Scenario = new(nameof(Otel20VirtualContext), "virtual-context", async (_, context) =>
        await context.RunInChildContextAsync(
            (_, _) => Task.FromResult("virtual-complete"),
            name: "otel-virtual-context",
            config: new ChildContextConfig { NestingType = NestingType.Flat }));
}

internal static class Otel11ChainedInvoke
{
    public static readonly Scenario Scenario = new(nameof(Otel11ChainedInvoke), "chained-invoke", async (payload, context) =>
    {
        var result = await context.InvokeAsync<System.Text.Json.JsonElement, System.Text.Json.JsonElement>(
            Scenarios.InvokeTarget, payload, name: "otel-invoke");
        // The driver completes this callback after the invocation that created it ends, forcing a replay of the invoke.
        await context.WaitForCallbackAsync<System.Text.Json.JsonElement>(
            (_, _, _) => Task.CompletedTask, name: "otel-invoke-replay-barrier");
        return result;
    });

    public static readonly Scenario Target = new("Otel11InvokeTarget", "chained-invoke", (payload, _) =>
        Task.FromResult<object?>(payload));
}

internal static class Otel18ChainedInvokeFailure
{
    public static readonly Scenario Scenario = new(nameof(Otel18ChainedInvokeFailure), "chained-invoke-failure", async (payload, context) =>
    {
        await context.InvokeAsync<System.Text.Json.JsonElement, System.Text.Json.JsonElement>(
            Scenarios.InvokeTarget, payload, name: "otel-failed-invoke");
        return null;
    });

    public static readonly Scenario Target = new("Otel18InvokeTarget", "chained-invoke-failure", (_, _) =>
        throw new InvalidOperationException("Intentional chained-invoke target failure"));
}
