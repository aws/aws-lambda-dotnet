// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class ParallelPartialFailureTest
{
    private readonly ITestOutputHelper _output;
    public ParallelPartialFailureTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Three branches, one throws, two succeed. With <c>CompletionConfig.AllCompleted()</c>
    /// the parallel does NOT throw — it surfaces success/failure counts and the
    /// per-branch errors. Validates per-branch error preservation through the
    /// service round-trip and back into the rebuilt <see cref="IBatchResult{T}"/>.
    /// </summary>
    [Fact]
    public async Task Parallel_PartialFailure_AllCompleted_ReportsCounts()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ParallelPartialFailureFunction"),
            "ppartial", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "p2"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        // AllCompleted means partial failure is NOT a workflow failure — the
        // user accepted the failure and returned a result.
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // Decode the workflow result payload and verify the counts surface correctly.
        using var doc = JsonDocument.Parse(responsePayload);
        var successCount = doc.RootElement.GetProperty("SuccessCount").GetInt32();
        var failureCount = doc.RootElement.GetProperty("FailureCount").GetInt32();
        var errorSummary = doc.RootElement.GetProperty("ErrorSummary").GetString();

        Assert.Equal(2, successCount);
        Assert.Equal(1, failureCount);
        Assert.NotNull(errorSummary);
        // The originating exception type is captured on the rebuilt
        // ChildContextException when reconstructing the batch.
        Assert.Contains("intentional partial failure", errorSummary);

        // History: 1 parent + 3 branches = 4 ContextStarted; 3 ContextSucceeded
        // (parent + 2 ok branches); 1 ContextFailed (the boom branch).
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.ContextStarted) ?? 0) >= 4
              && (h.Events?.Any(e => e.EventType == EventType.ContextFailed) ?? false)
              && (h.Events?.Count(e => e.EventType == EventType.ContextSucceeded) ?? 0) >= 3,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        Assert.Equal(4, events.Count(e => e.EventType == EventType.ContextStarted));
        Assert.Equal(3, events.Count(e => e.EventType == EventType.ContextSucceeded));
        Assert.Equal(1, events.Count(e => e.EventType == EventType.ContextFailed));

        // The failing branch's checkpoint preserves the exception message.
        var failedEvent = events.SingleOrDefault(e => e.EventType == EventType.ContextFailed);
        Assert.NotNull(failedEvent);
        Assert.Equal("boom", failedEvent!.Name);
        Assert.Contains("intentional partial failure",
            failedEvent.ContextFailedDetails?.Error?.Payload?.ErrorMessage ?? string.Empty);
    }
}
