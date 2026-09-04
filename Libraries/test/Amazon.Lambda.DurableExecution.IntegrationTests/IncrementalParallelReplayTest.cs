// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class IncrementalParallelReplayTest
{
    private readonly ITestOutputHelper _output;
    public IncrementalParallelReplayTest(ITestOutputHelper output) => _output = output;

    private static string HashOpId(string raw)
    {
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>
    /// Deterministic replay of the incremental <c>CreateParallel</c> API across
    /// both replay paths. Three branches each do a step (generating a GUID) then a
    /// durable wait; a further wait runs after the parallel completes. This forces
    /// (1) a Run-mode replay while the parent CONTEXT is still STARTED, and (2) a
    /// terminal-reconstruct resume once the parent CONTEXT is SUCCEEDED. Verifies:
    ///   1. Branch operation IDs match SHA-256("&lt;parentId&gt;-&lt;n&gt;").
    ///   2. Each branch's "generate" step succeeds EXACTLY once — proving neither
    ///      the STARTED-parent replay nor the terminal reconstruct re-executes a
    ///      branch body.
    ///   3. The run spans multiple invocations (suspend/resume actually happened).
    /// </summary>
    [Fact]
    public async Task IncrementalParallel_ReplayDeterminism_AcrossRunAndTerminalPaths()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("IncrementalParallelReplayFunction"),
            "ipreplay", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "p6"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(180));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // The parallel parent is the first root-level operation -> SHA256("1").
        var parentOpId = HashOpId("1");
        var expectedBranchIds = new[]
        {
            HashOpId($"{parentOpId}-1"),
            HashOpId($"{parentOpId}-2"),
            HashOpId($"{parentOpId}-3"),
        };

        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h =>
            {
                var events = h.Events ?? new List<Event>();
                // Parent + 3 branch CONTEXTs all succeeded.
                if (events.Count(e => e.EventType == EventType.ContextSucceeded) < 4) return false;
                // Each branch ran one step and one wait, plus the post-parallel wait.
                if (events.Count(e => e.EventType == EventType.StepSucceeded) < 3) return false;
                if (events.Count(e => e.EventType == EventType.WaitSucceeded) < 4) return false;
                return true;
            },
            TimeSpan.FromSeconds(120));
        var allEvents = history.Events ?? new List<Event>();

        // 1. Branch operation IDs match the deterministic hash.
        var observedBranchIds = allEvents
            .Where(e => e.EventType == EventType.ContextStarted && e.Id != null && e.Id != parentOpId)
            .Select(e => e.Id)
            .Distinct()
            .ToList();
        Assert.Equal(3, observedBranchIds.Count);
        foreach (var expected in expectedBranchIds)
        {
            Assert.Contains(expected, observedBranchIds);
        }

        // 2. Each branch's "generate" step succeeded exactly once — no branch body
        // re-executed on either the STARTED-parent replay or the terminal resume.
        var generateSucceeded = allEvents
            .Where(e => e.EventType == EventType.StepSucceeded && e.Name == "generate")
            .ToList();
        Assert.Equal(3, generateSucceeded.Count);

        // 3. Parent + 3 branches succeeded once each.
        Assert.Equal(4, allEvents.Count(e => e.EventType == EventType.ContextSucceeded));

        // 4. The run spans multiple invocations (branch waits + post-parallel wait).
        var invocations = allEvents.Where(e => e.InvocationCompletedDetails != null).ToList();
        Assert.True(
            invocations.Count >= 2,
            $"Expected >= 2 InvocationCompleted events (suspend + resume), got {invocations.Count}");

        // 5. The user-visible response carries the joined per-branch results.
        Assert.Contains("completed", responsePayload, StringComparison.OrdinalIgnoreCase);
    }
}
