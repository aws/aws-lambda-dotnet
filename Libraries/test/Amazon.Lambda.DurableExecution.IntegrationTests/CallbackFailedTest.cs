// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class CallbackFailedTest
{
    private readonly ITestOutputHelper _output;
    public CallbackFailedTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end failure path for <c>CreateCallbackAsync</c>:
    /// a paired <c>RejecterFunction</c> Lambda (Event-invoked from the workflow)
    /// reports a failure via <c>SendDurableExecutionCallbackFailure</c>. The SDK
    /// raises <see cref="CallbackFailedException"/> from <c>GetResultAsync</c>,
    /// and the workflow surfaces FAILED with that exception type recorded.
    /// </summary>
    /// <remarks>
    /// The callback delivery has to come from a separate Lambda — not from the
    /// test process — because the test's synchronous <c>InvokeAsync</c> blocks
    /// until the durable execution reaches a terminal state. If the test tried
    /// to deliver the callback itself, it would deadlock against its own
    /// blocked Invoke.
    /// </remarks>
    [Fact]
    public async Task CallbackFailed_SurfacesAsCallbackFailedException()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("CallbackFailedFunction"),
            "cb-failed", _output,
            externalFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("RejecterFunction"));

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "x"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Initial response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("FAILED", status, ignoreCase: true);

        // The workflow's surfaced exception is CallbackFailedException — the SDK
        // wraps the external error message into the exception's Message. Verify
        // the recorded error type is the SDK's CallbackFailedException and that
        // the original failure message survives.
        var execution = await deployment.GetExecutionAsync(arn!);
        Assert.NotNull(execution.Error);
        Assert.Equal(typeof(CallbackFailedException).FullName, execution.Error.ErrorType);
        Assert.Contains("rejected", execution.Error.ErrorMessage);

        // History records both Started and Failed for the same callback.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.EventType == EventType.CallbackStarted) ?? false)
              && (h.Events?.Any(e => e.EventType == EventType.CallbackFailed) ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();
        Assert.Single(events.Where(e => e.EventType == EventType.CallbackStarted));
        Assert.Single(events.Where(e => e.EventType == EventType.CallbackFailed));
    }
}
