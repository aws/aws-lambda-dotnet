using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class WaitForCallbackSubmitterFailsTest
{
    private readonly ITestOutputHelper _output;
    public WaitForCallbackSubmitterFailsTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end submitter-failure path for <c>WaitForCallbackAsync</c>:
    /// the submitter throws on attempt 1 with <see cref="RetryStrategy.None"/>;
    /// the SDK fails the composite operation terminally and surfaces
    /// <see cref="CallbackSubmitterException"/>. The workflow surfaces FAILED.
    /// </summary>
    [Fact]
    public async Task WaitForCallback_SubmitterThrows_SurfacesAsCallbackSubmitterException()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitForCallbackSubmitterFailsFunction"),
            "wfcb-fail", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "x"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Initial response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("FAILED", status, ignoreCase: true);

        // The workflow surfaces CallbackSubmitterException — the SDK's wrapper
        // type around the failed submitter step. Verify both the recorded
        // ErrorType and that the original "submitter intentional failure"
        // message survives in the error chain.
        var execution = await deployment.GetExecutionAsync(arn!);
        Assert.NotNull(execution.Error);
        Assert.Equal(typeof(CallbackSubmitterException).FullName, execution.Error.ErrorType);
        // ErrorObject.FromException records the outer exception's Message; that
        // message should reference the submitter failure context. Be lenient
        // about exact wording since the SDK may prepend / wrap the inner.
        Assert.False(string.IsNullOrEmpty(execution.Error.ErrorMessage));

        // History records the submitter step failed exactly once — RetryStrategy.None
        // means no retries — and no callback was ever started since the submitter
        // never delivered the ID.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => h.Events?.Any(e => e.StepFailedDetails != null) ?? false,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        var stepFailures = events.Where(e => e.StepFailedDetails != null).ToList();
        Assert.Single(stepFailures);
        var failureMessage = stepFailures[0].StepFailedDetails.Error?.Payload?.ErrorMessage ?? string.Empty;
        Assert.Contains("submitter intentional failure", failureMessage);

        // No SUCCEEDED step events — the submitter never succeeded.
        Assert.Empty(events.Where(e => e.StepSucceededDetails != null));
    }
}
