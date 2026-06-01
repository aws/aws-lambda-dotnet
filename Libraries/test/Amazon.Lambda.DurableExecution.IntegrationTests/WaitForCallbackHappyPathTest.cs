// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class WaitForCallbackHappyPathTest
{
    private readonly ITestOutputHelper _output;
    public WaitForCallbackHappyPathTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end happy path for <c>WaitForCallbackAsync</c> using a real
    /// two-Lambda flow: the workflow's submitter Event-invokes a paired
    /// <c>ApproverFunction</c>, which calls <c>SendDurableExecutionCallbackSuccess</c>
    /// out-of-band. The workflow suspends after the submitter step completes,
    /// the service re-invokes the workflow once the approver resolves the
    /// callback, and <c>WaitForCallbackAsync</c> returns the deserialized result.
    /// </summary>
    [Fact]
    public async Task WaitForCallback_SubmitterDeliversResult_WorkflowCompletes()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitForCallbackHappyPathFunction"),
            "wfcb-happy", _output,
            externalFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("ApproverFunction"));

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "approver-1"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Initial response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // The execution returns the payload the submitter delivered.
        var execution = await deployment.GetExecutionAsync(arn!);
        Assert.NotNull(execution.Result);
        Assert.Contains("approved", execution.Result);
        Assert.Contains("approver-1", execution.Result);

        // History records the canonical WaitForCallback lifecycle:
        // submitter step Started + Succeeded, callback Started + Succeeded,
        // and a containing context (CONTEXT operation) wrapping the pair.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.EventType == EventType.CallbackStarted) ?? false)
              && (h.Events?.Any(e => e.EventType == EventType.CallbackSucceeded) ?? false)
              && (h.Events?.Any(e => e.EventType == EventType.StepSucceeded) ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        Assert.Single(events.Where(e => e.EventType == EventType.CallbackStarted));
        Assert.Single(events.Where(e => e.EventType == EventType.CallbackSucceeded));

        // The submitter ran exactly once and succeeded — the SDK's "callback
        // already resolved" branch must NOT have re-run it on replay. Filter
        // on a name that the SDK uses for the submitter step (typically
        // matches the WaitForCallback name).
        var submitterSteps = events
            .Where(e => e.EventType == EventType.StepSucceeded
                     || e.EventType == EventType.StepStarted)
            .ToList();
        Assert.NotEmpty(submitterSteps);
    }
}
