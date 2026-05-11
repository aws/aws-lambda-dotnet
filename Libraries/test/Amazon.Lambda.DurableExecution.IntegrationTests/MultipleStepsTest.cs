using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class MultipleStepsTest
{
    private readonly ITestOutputHelper _output;
    public MultipleStepsTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task MultipleSteps_AllCheckpointed()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MultipleStepsFunction"),
            "multi", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "chain"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // History is eventually consistent — the execution can be SUCCEEDED before
        // all events are indexed. Wait until we see all 5 step-succeeded events.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.StepSucceededDetails != null) ?? 0) >= 5,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // Each step ran exactly once (no replay-induced duplicates) in declaration order,
        // and each step's output chained from the previous one.
        var stepResults = events
            .Where(e => e.StepSucceededDetails != null)
            .Select(e => $"{e.Name}={e.StepSucceededDetails.Result?.Payload?.Trim('"')}")
            .ToList();
        Assert.Equal(
            new[]
            {
                "step_1=a-chain",
                "step_2=a-chain-b",
                "step_3=a-chain-b-c",
                "step_4=a-chain-b-c-d",
                "step_5=a-chain-b-c-d-e",
            },
            stepResults);
    }
}
