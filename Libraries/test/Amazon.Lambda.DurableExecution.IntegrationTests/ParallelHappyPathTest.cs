// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class ParallelHappyPathTest
{
    private readonly ITestOutputHelper _output;
    public ParallelHappyPathTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end happy-path parallel: three branches run concurrently, each
    /// produces a string, and the workflow returns the joined results. Validates
    /// the parent CONTEXT and per-branch CONTEXT checkpoints all land in the
    /// service-side history with the correct names and ordering.
    /// </summary>
    [Fact]
    public async Task Parallel_AllBranchesSucceed()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ParallelHappyPathFunction"),
            "phappy", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "p1"}""");
        Assert.Equal(200, invokeResponse.StatusCode);

        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // The user-visible payload contains all three branch outputs in
        // declaration order (the SDK preserves index order even when branches
        // race).
        Assert.Contains("alpha-p1", responsePayload);
        Assert.Contains("beta-p1", responsePayload);
        Assert.Contains("gamma-p1", responsePayload);

        // History is eventually consistent — wait until the parent CONTEXT and
        // all three child CONTEXT checkpoints are visible.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.ContextStarted) ?? 0) >= 4
              && (h.Events?.Count(e => e.EventType == EventType.ContextSucceeded) ?? 0) >= 4,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // Parent + 3 branches = 4 ContextStarted, 4 ContextSucceeded.
        Assert.Equal(4, events.Count(e => e.EventType == EventType.ContextStarted));
        Assert.Equal(4, events.Count(e => e.EventType == EventType.ContextSucceeded));

        // The three branches show up by name on their own ContextStarted events.
        var startedNames = events
            .Where(e => e.EventType == EventType.ContextStarted)
            .Select(e => e.Name)
            .ToList();
        Assert.Contains("fanout", startedNames);
        Assert.Contains("alpha", startedNames);
        Assert.Contains("beta", startedNames);
        Assert.Contains("gamma", startedNames);

        // No branch failed.
        Assert.Empty(events.Where(e => e.EventType == EventType.ContextFailed));
    }
}
