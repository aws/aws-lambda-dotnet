// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class ChildContextFailsTest
{
    private readonly ITestOutputHelper _output;
    public ChildContextFailsTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end RunInChildContextAsync failure path: the user func inside the
    /// child throws, the SDK emits a CONTEXT FAIL checkpoint, the child's prior
    /// inner step is preserved, and the workflow is marked FAILED with the
    /// original exception details surfaced via ContextFailedDetails.Error.
    /// </summary>
    [Fact]
    public async Task ChildContext_FailureSurfacesAsContextFailed()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ChildContextFailsFunction"),
            "childctxfail", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "integ-test-fail"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        // Failed workflows return null payload; locate the execution by name.
        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("FAILED", status, ignoreCase: true);

        var execution = await deployment.GetExecutionAsync(arn!);
        Assert.NotNull(execution.Error);
        Assert.Contains("intentional child context failure", execution.Error.ErrorMessage);

        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.EventType == EventType.ContextStarted) ?? false)
              && (h.Events?.Any(e => e.EventType == EventType.ContextFailed) ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        var contextStarted = events.SingleOrDefault(e => e.EventType == EventType.ContextStarted && e.Name == "phase");
        Assert.NotNull(contextStarted);
        Assert.Equal("OrderProcessing", contextStarted!.SubType);
        // The child context op itself is at root — its boundary opens at the parent scope.
        Assert.Null(contextStarted.ParentId);

        // The CONTEXT FAIL record carries the original exception details and
        // closes the boundary back at the parent scope (root, ParentId=null).
        var contextFailed = events.SingleOrDefault(e => e.EventType == EventType.ContextFailed && e.Name == "phase");
        Assert.NotNull(contextFailed);
        Assert.Null(contextFailed!.ParentId);
        var error = contextFailed.ContextFailedDetails.Error?.Payload;
        Assert.NotNull(error);
        Assert.Contains("intentional child context failure", error!.ErrorMessage ?? string.Empty);
        Assert.Equal(typeof(InvalidOperationException).FullName, error.ErrorType);
        // The wire ErrorObject preserves StackTrace from ToSdkError end-to-end —
        // the service stores it and returns it on replay (or directly in the
        // history event), so user-facing ChildContextException.OriginalStackTrace
        // is populated rather than dropped.
        Assert.NotNull(error.StackTrace);
        Assert.NotEmpty(error.StackTrace);

        // The step that ran before the throw was checkpointed under the child.
        var contextOpId = contextStarted.Id;
        var innerStep = events.SingleOrDefault(
            e => e.StepSucceededDetails != null && e.Name == "prepare" && e.ParentId == contextOpId);
        Assert.NotNull(innerStep);
        Assert.Equal("\"prepared-integ-test-fail\"", innerStep!.StepSucceededDetails.Result?.Payload);

        // Every inner step/wait event for this workflow is parented under the
        // child context — the child is a single observability boundary.
        var innerOpEvents = events
            .Where(e => e.StepStartedDetails != null
                     || e.StepSucceededDetails != null
                     || e.StepFailedDetails != null
                     || e.WaitStartedDetails != null
                     || e.WaitSucceededDetails != null)
            .ToList();
        Assert.NotEmpty(innerOpEvents);
        Assert.All(innerOpEvents, e => Assert.Equal(contextOpId, e.ParentId));

        // The child never reached SUCCEED; the workflow body past the throw is unreachable.
        Assert.DoesNotContain(events, e => e.EventType == EventType.ContextSucceeded);
    }
}
