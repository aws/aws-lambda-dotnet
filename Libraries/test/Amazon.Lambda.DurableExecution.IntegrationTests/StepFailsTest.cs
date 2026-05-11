using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class StepFailsTest
{
    private readonly ITestOutputHelper _output;
    public StepFailsTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task StepFails_PropagatesAsFailedStatus()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("StepFailsFunction"),
            "stepfail", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "x"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        // Failed workflows return null payload to the Invoke caller. Locate the execution
        // by name and verify the service marked it FAILED.
        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("FAILED", status, ignoreCase: true);

        var execution = await deployment.GetExecutionAsync(arn!);
        Assert.NotNull(execution.Error);
        Assert.Contains("intentional failure", execution.Error.ErrorMessage);

        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => h.Events?.Any(e => e.StepFailedDetails != null) ?? false,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // The failing step recorded a StepFailed event with the exception message.
        var stepFailed = events.FirstOrDefault(e => e.StepFailedDetails != null && e.Name == "fail_step");
        Assert.NotNull(stepFailed);
        Assert.Contains("intentional failure", stepFailed!.StepFailedDetails.Error?.Payload?.ErrorMessage ?? string.Empty);

        // No step ever succeeded — the workflow body was unreachable past the throw.
        Assert.Empty(events.Where(e => e.StepSucceededDetails != null));
    }
}
