// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace Amazon.Lambda.DurableExecution.ConformanceTests.Otel;

/// <summary>
/// One conformance case. <see cref="Name"/> is the class name template.yaml puts in CONFORMANCE_SCENARIO;
/// <see cref="PayloadScenario"/> is the <c>scenario</c> field the shared requirement sends.
/// </summary>
public sealed record Scenario(string Name, string PayloadScenario, Func<JsonElement, IDurableContext, Task<object?>> Run);

public static class Scenarios
{
    private static readonly Dictionary<string, Scenario> All = new[]
    {
        Otel1Success.Scenario,
        Otel2WaitResume.Scenario,
        Otel3Retry.Scenario,
        Otel4TerminalFailure.Scenario,
        Otel5ChildContext.Scenario,
        Otel6Parallel.Scenario,
        Otel7Map.Scenario,
        Otel8HandledFailure.Scenario,
        Otel9WaitForCondition.Scenario,
        Otel10WaitForCallback.Scenario,
        Otel11ChainedInvoke.Scenario,
        Otel11ChainedInvoke.Target,
        Otel12ChildContextFailure.Scenario,
        Otel13ParallelFailure.Scenario,
        Otel14MapFailure.Scenario,
        Otel15WaitInterrupted.Scenario,
        Otel16WaitForConditionFailure.Scenario,
        Otel17WaitForCallbackFailure.Scenario,
        Otel18ChainedInvokeFailure.Scenario,
        Otel18ChainedInvokeFailure.Target,
        Otel19ExecutionFailure.Scenario,
        Otel20VirtualContext.Scenario,
    }.ToDictionary(scenario => scenario.Name);

    /// <summary>The function a chained-invoke case calls, set by template.yaml.</summary>
    public static string InvokeTarget => Environment.GetEnvironmentVariable("OTEL_INVOKE_TARGET_FUNCTION_NAME")
        ?? throw new InvalidOperationException("OTEL_INVOKE_TARGET_FUNCTION_NAME is not set");

    public static Scenario Find(string name)
        => All.TryGetValue(name, out var scenario)
            ? scenario
            : throw new InvalidOperationException($"Unknown conformance scenario '{name}'");

    /// <summary>Rejects an event routed to the wrong function.</summary>
    public static void RequireScenario(JsonElement payload, string expected)
    {
        var actual = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("scenario", out var value)
            ? value.GetString()
            : null;
        if (actual != expected)
        {
            throw new ArgumentException($"Expected scenario {expected}, received {actual}");
        }
    }
}
