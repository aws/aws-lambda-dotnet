// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;

namespace Amazon.Lambda.DurableExecution.ConformanceTests.Otel;

internal static class Otel21CompletedStepReplay
{
    public static readonly Scenario Scenario = new(nameof(Otel21CompletedStepReplay), "completed-step-replay", async (_, context) =>
    {
        var before = await context.StepAsync((_, _) => Task.FromResult("before"), name: "otel-before-wait");
        await context.WaitAsync(TimeSpan.FromSeconds(1), name: "otel-replay-wait");
        var after = await context.StepAsync((_, _) => Task.FromResult("after"), name: "otel-after-wait");
        return $"{before}-{after}";
    });
}

/// <summary>
/// Cases 22: user code creates an ordinary span at each probe point. The validator checks the span the plugin made
/// current there is its parent.
/// </summary>
internal static class Probes
{
    public const string SourceName = "aws-durable-execution-conformance";
    private static readonly ActivitySource Source = new(SourceName);

    public static void Probe(string label)
    {
        using var span = Source.StartActivity($"conformance.{label}");
        span?.SetTag("conformance.callback", label);
    }
}

internal static class Otel22UserFunctionContext
{
    public static readonly Scenario Scenario = new(nameof(Otel22UserFunctionContext), "user-function-context", async (_, context) =>
    {
        Probes.Probe("handler");
        await context.StepAsync((_, _) => { Probes.Probe("step"); return Task.FromResult("step"); }, name: "otel-context-step");
        await context.RunInChildContextAsync(async (child, _) =>
        {
            Probes.Probe("child");
            await child.StepAsync((_, _) => { Probes.Probe("child-step"); return Task.FromResult("child-step"); },
                name: "otel-context-child-step");
            Probes.Probe("child-restored");
            return "child";
        }, name: "otel-context-child", config: new ChildContextConfig { SubType = "RunInChildContext" });

        await context.ParallelAsync(
            new[]
            {
                new DurableBranch<string>("otel-context-branch-a", (branch, _) =>
                {
                    Probes.Probe("parallel-a");
                    return branch.StepAsync((_, _) => { Probes.Probe("parallel-step-a"); return Task.FromResult("a"); },
                        name: "otel-context-branch-step-a");
                }),
                new DurableBranch<string>("otel-context-branch-b", (branch, _) =>
                {
                    Probes.Probe("parallel-b");
                    return branch.StepAsync((_, _) => { Probes.Probe("parallel-step-b"); return Task.FromResult("b"); },
                        name: "otel-context-branch-step-b");
                }),
            },
            name: "otel-context-parallel",
            config: new ParallelConfig { MaxConcurrency = 2 });

        await context.MapAsync(
            new[] { 0, 1 },
            (iteration, item, index, _, _) =>
            {
                Probes.Probe($"map-{index}");
                return iteration.StepAsync((_, _) => { Probes.Probe($"map-step-{index}"); return Task.FromResult(item); },
                    name: $"otel-context-map-step-{index}");
            },
            name: "otel-context-map",
            config: new MapConfig<int> { MaxConcurrency = 2, ItemNamer = (_, index) => $"otel-context-iteration-{index}" });

        Probes.Probe("handler-restored");
        await context.WaitAsync(TimeSpan.FromSeconds(1), name: "otel-context-resume");
        Probes.Probe("handler-after-resume");
        return "context-complete";
    });
}

internal static class Otel25CaughtCallbackFailureReplay
{
    public static readonly Scenario Scenario = new(nameof(Otel25CaughtCallbackFailureReplay), "caught-callback-failure-replay", async (_, context) =>
    {
        try
        {
            await context.WaitForCallbackAsync<string>((_, _, _) => Task.CompletedTask, name: "otel-failed-callback");
            throw new InvalidOperationException("Expected the callback to fail");
        }
        catch (CallbackFailedException)
        {
        }

        var observed = await context.StepAsync((_, _) => Task.FromResult("caught"), name: "otel-failed-callback-observed");
        var one = await context.WaitForCallbackAsync<string>((_, _, _) => Task.CompletedTask, name: "otel-failed-callback-barrier-one");
        var two = await context.WaitForCallbackAsync<string>((_, _, _) => Task.CompletedTask, name: "otel-failed-callback-barrier-two");
        return $"{observed}/{one}/{two}";
    });
}

internal static class Otel26ExternalCompletionReplay
{
    public static readonly Scenario Scenario = new(nameof(Otel26ExternalCompletionReplay), "external-callback-completion-replay", async (_, context) =>
    {
        var callback = await context.CreateCallbackAsync<string>(name: "otel-external-target");
        var target = await callback.GetResultAsync();
        var observed = await context.StepAsync((_, _) => Task.FromResult(target), name: "otel-external-target-observed");
        var one = await context.WaitForCallbackAsync<string>((_, _, _) => Task.CompletedTask, name: "otel-external-barrier-one");
        var two = await context.WaitForCallbackAsync<string>((_, _, _) => Task.CompletedTask, name: "otel-external-barrier-two");
        return $"{observed}/{one}/{two}";
    });
}

internal static class Otel27ChainedInvokeSuspend
{
    public static readonly Scenario Scenario = new(nameof(Otel27ChainedInvokeSuspend), "chained-invoke-suspend", async (payload, context) =>
        await context.InvokeAsync<JsonElement, JsonElement>(Scenarios.InvokeTarget, payload, name: "otel-suspended-invoke"));

    public static readonly Scenario Target = new("Otel27InvokeTarget", "chained-invoke-suspend", async (payload, context) =>
    {
        await context.WaitAsync(TimeSpan.FromSeconds(1), name: "otel-suspended-target-wait");
        return payload;
    });
}
