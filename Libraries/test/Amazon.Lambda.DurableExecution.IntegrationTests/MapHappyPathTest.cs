using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class MapHappyPathTest
{
    private readonly ITestOutputHelper _output;
    public MapHappyPathTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end happy-path map: three items each processed in a step, and the
    /// workflow returns the joined results. Validates the parent CONTEXT and
    /// per-item CONTEXT checkpoints all land in the service-side history with the
    /// correct (ItemNamer-derived) names and ordering.
    /// </summary>
    [Fact]
    public async Task Map_AllItemsSucceed()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MapHappyPathFunction"),
            "mhappy", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "m1"}""");
        Assert.Equal(200, invokeResponse.StatusCode);

        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // The user-visible payload contains all three item outputs in index
        // order (the SDK preserves index order even when items race).
        Assert.Contains("order-1-m1", responsePayload);
        Assert.Contains("order-2-m1", responsePayload);
        Assert.Contains("order-3-m1", responsePayload);

        // History is eventually consistent — wait until the parent CONTEXT and
        // all three item CONTEXT checkpoints are visible.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.ContextStarted) ?? 0) >= 4
              && (h.Events?.Count(e => e.EventType == EventType.ContextSucceeded) ?? 0) >= 4,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // Parent + 3 items = 4 ContextStarted, 4 ContextSucceeded.
        Assert.Equal(4, events.Count(e => e.EventType == EventType.ContextStarted));
        Assert.Equal(4, events.Count(e => e.EventType == EventType.ContextSucceeded));

        // The three items show up by their ItemNamer name on their own
        // ContextStarted events.
        var startedNames = events
            .Where(e => e.EventType == EventType.ContextStarted)
            .Select(e => e.Name)
            .ToList();
        Assert.Contains("process_all", startedNames);
        Assert.Contains("item-order-1", startedNames);
        Assert.Contains("item-order-2", startedNames);
        Assert.Contains("item-order-3", startedNames);

        // Each item ran one step => 3 StepSucceeded.
        Assert.Equal(3, events.Count(e => e.EventType == EventType.StepSucceeded));

        // No item failed.
        Assert.Empty(events.Where(e => e.EventType == EventType.ContextFailed));
    }
}
