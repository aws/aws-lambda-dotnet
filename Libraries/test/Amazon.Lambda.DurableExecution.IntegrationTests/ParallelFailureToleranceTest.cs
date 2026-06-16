// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class ParallelFailureToleranceTest
{
    private readonly ITestOutputHelper _output;
    public ParallelFailureToleranceTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Five branches, two fail, ToleratedFailureCount=1. The parallel must surface a
    /// <see cref="ParallelException"/> with reason
    /// <see cref="CompletionReason.FailureToleranceExceeded"/>; the workflow must
    /// terminate FAILED. Validates the failure-tolerance short-circuit and that
    /// <c>ParallelException</c> propagates as the workflow's terminal error.
    /// </summary>
    [Fact]
    public async Task Parallel_FailureToleranceExceeded_FailsWorkflow()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ParallelFailureToleranceFunction"),
            "ptol", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "p3"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        // Failed workflows return null payload to the Invoke caller — locate the
        // execution by name to inspect its terminal status.
        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("FAILED", status, ignoreCase: true);

        var execution = await deployment.GetExecutionAsync(arn!);
        Assert.NotNull(execution.Error);
        // ParallelException is the terminal error type the SDK throws when the
        // failure-tolerance short-circuit fires.
        var errorType = execution.Error.ErrorType ?? string.Empty;
        var errorMessage = execution.Error.ErrorMessage ?? string.Empty;
        Assert.True(
            errorType.Contains("ParallelException", StringComparison.Ordinal)
                || errorMessage.Contains("Parallel", StringComparison.OrdinalIgnoreCase),
            $"Expected error to indicate ParallelException; got type='{errorType}' message='{errorMessage}'");

        // History: parent CONTEXT and at least 2 failed branch contexts visible.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.ContextStarted) ?? 0) >= 3
              && (h.Events?.Count(e => e.EventType == EventType.ContextFailed) ?? 0) >= 2,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // At least 2 branches failed (the third may or may not have been
        // dispatched depending on race; the parent CONTEXT itself also fails).
        Assert.True(
            events.Count(e => e.EventType == EventType.ContextFailed) >= 2,
            $"Expected >= 2 ContextFailed events; got {events.Count(e => e.EventType == EventType.ContextFailed)}");

        // The parent context (named "tolerance") records the aggregate failure.
        var parentFailed = events.FirstOrDefault(e =>
            e.EventType == EventType.ContextFailed && e.Name == "tolerance");
        Assert.NotNull(parentFailed);
    }
}
