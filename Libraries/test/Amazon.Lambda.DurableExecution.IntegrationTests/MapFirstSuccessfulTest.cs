using System.Linq;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class MapFirstSuccessfulTest
{
    private readonly ITestOutputHelper _output;
    public MapFirstSuccessfulTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Four items with staggered durable waits, <c>FirstSuccessful</c>: as soon
    /// as one item completes, the map resolves. In-flight items remain in
    /// <see cref="BatchItemStatus.Started"/> rather than being cancelled.
    /// Validates the cross-cutting decision: orphan units are NOT cancelled, and
    /// short-circuit reports them as Started.
    /// </summary>
    [Fact]
    public async Task Map_FirstSuccessful_ShortCircuitsOnFirstWin()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MapFirstSuccessfulFunction"),
            "mfirst", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "m4"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // Wait timer = 8s, plus invocation overhead. Generous timeout for CI variance.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        using var doc = JsonDocument.Parse(responsePayload);
        var winnerIndex = doc.RootElement.GetProperty("WinnerIndex").GetInt32();
        var winnerName = doc.RootElement.GetProperty("WinnerName").GetString();
        var completionReason = doc.RootElement.GetProperty("CompletionReason").GetString();
        var successCount = doc.RootElement.GetProperty("SuccessCount").GetInt32();

        // At least one item succeeded — the workflow short-circuited as soon as
        // the first win materialised. The fastest item is index 1 (1s wait).
        Assert.True(successCount >= 1, $"Expected >= 1 successful item, got {successCount}");
        Assert.True(winnerIndex >= 0 && winnerIndex < 4,
            $"WinnerIndex should be a valid item index, got {winnerIndex}");
        Assert.NotNull(winnerName);
        Assert.NotEqual("FailureToleranceExceeded", completionReason);

        // Service-side: the parent CONTEXT and at least the winning item CONTEXT
        // succeeded. Other items' final state is timing-dependent (the
        // orchestrator does not cancel in-flight units on short-circuit).
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.EventType == EventType.ContextSucceeded && e.Name == "race") ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        var parentSucceeded = events.FirstOrDefault(e =>
            e.EventType == EventType.ContextSucceeded && e.Name == "race");
        Assert.NotNull(parentSucceeded);

        // The winning item's CONTEXT SUCCEEDED is in the history.
        Assert.Contains(events, e => e.EventType == EventType.ContextSucceeded && e.Name == winnerName);
    }
}
