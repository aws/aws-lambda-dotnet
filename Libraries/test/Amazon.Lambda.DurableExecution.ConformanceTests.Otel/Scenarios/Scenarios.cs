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
    }.ToDictionary(scenario => scenario.Name);

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
