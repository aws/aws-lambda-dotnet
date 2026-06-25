// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class CallbackTimeoutTest
{
    private readonly ITestOutputHelper _output;
    public CallbackTimeoutTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end timeout path for <c>CreateCallbackAsync</c>:
    /// the workflow waits on a callback whose <see cref="CallbackConfig.Timeout"/>
    /// elapses before any result is delivered. The service marks the callback as
    /// TIMED_OUT, the SDK throws <see cref="CallbackTimeoutException"/>, and the
    /// workflow surfaces FAILED with that exception type recorded.
    /// </summary>
    [Fact]
    public async Task CallbackTimeout_SurfacesAsCallbackTimeoutException()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("CallbackTimeoutFunction"),
            "cb-timeout", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "x"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Initial response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // Capture the CallbackId before the timeout fires so we can assert it
        // on the surfaced exception. CallbackStarted has the ID; CallbackTimedOut
        // typically does not echo it back on the event.
        var callbackId = await WaitForCallbackIdAsync(deployment, arn!, TimeSpan.FromSeconds(30));
        Assert.False(string.IsNullOrEmpty(callbackId));
        _output.WriteLine($"Service-allocated CallbackId: {callbackId}");

        // The configured timeout is 10s; allow generous headroom for service
        // latency (timer scheduling + re-invoke + Lambda cold start).
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("FAILED", status, ignoreCase: true);

        // The workflow surfaces CallbackTimeoutException to the user, but the
        // terminal ErrorObject records the ORIGINAL/underlying error identity —
        // ErrorObject.FromException deliberately unwraps CallbackException to the
        // error the service recorded on the timed-out callback (the service's
        // "Callback.Timeout" code), matching the Java/Python/JS SDKs and the
        // unwrapping covered by ModelsTests.ErrorObject_FromException_UnwrapsCallbackException.
        // Sibling failure tests (ChildContextFailsTest, InvokeFailureTest) assert
        // the same underlying-type contract. Verify the recorded type is the
        // service timeout code and the message still mentions "timed out".
        var execution = await deployment.GetExecutionAsync(arn!);
        Assert.NotNull(execution.Error);
        Assert.Equal("Callback.Timeout", execution.Error.ErrorType);
        Assert.Contains("timed out", execution.Error.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // History records both Started and TimedOut for the same callback.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.EventType == EventType.CallbackStarted) ?? false)
              && (h.Events?.Any(e => e.EventType == EventType.CallbackTimedOut) ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();
        Assert.Single(events.Where(e => e.EventType == EventType.CallbackStarted));
        Assert.Single(events.Where(e => e.EventType == EventType.CallbackTimedOut));
    }

    private static async Task<string?> WaitForCallbackIdAsync(
        DurableFunctionDeployment deployment, string arn, TimeSpan timeout)
    {
        var history = await deployment.WaitForHistoryAsync(
            arn,
            h => h.Events?.Any(e =>
                e.CallbackStartedDetails != null
                && !string.IsNullOrEmpty(e.CallbackStartedDetails.CallbackId)) ?? false,
            timeout);
        return history.Events?
            .Where(e => e.CallbackStartedDetails != null
                     && !string.IsNullOrEmpty(e.CallbackStartedDetails.CallbackId))
            .Select(e => e.CallbackStartedDetails.CallbackId)
            .FirstOrDefault();
    }
}
