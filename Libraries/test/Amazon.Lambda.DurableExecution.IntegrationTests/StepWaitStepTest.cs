using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class StepWaitStepTest
{
    private readonly ITestOutputHelper _output;
    public StepWaitStepTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task StepWaitStep_CompletesViaService()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("StepWaitStepFunction"),
            "stepwait", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "integ-test-123"}""");
        Assert.Equal(200, invokeResponse.StatusCode);

        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.StepSucceededDetails != null) ?? 0) >= 2
              && (h.Events?.Any(e => e.WaitSucceededDetails != null) ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // Both steps ran in order and produced the expected chained outputs.
        var stepResults = events
            .Where(e => e.StepSucceededDetails != null)
            .Select(e => (Name: e.Name, Payload: e.StepSucceededDetails.Result?.Payload?.Trim('"')))
            .ToList();
        Assert.Equal(2, stepResults.Count);
        Assert.Equal("validate", stepResults[0].Name);
        Assert.Equal("validated-integ-test-123", stepResults[0].Payload);
        Assert.Equal("process", stepResults[1].Name);
        Assert.Equal("processed-validated-integ-test-123", stepResults[1].Payload);

        // The wait was actually scheduled with the expected duration.
        var waitStarted = events.FirstOrDefault(e => e.WaitStartedDetails != null && e.Name == "short_wait");
        Assert.NotNull(waitStarted);
        Assert.Equal(3, waitStarted!.WaitStartedDetails.Duration);
        var waitSucceeded = events.FirstOrDefault(e => e.WaitSucceededDetails != null && e.Name == "short_wait");
        Assert.NotNull(waitSucceeded);
    }
}
