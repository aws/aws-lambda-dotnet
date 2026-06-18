using System.Linq;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class MapPartialFailureTest
{
    private readonly ITestOutputHelper _output;
    public MapPartialFailureTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Three items, one throws, two succeed — with NO config supplied. Map's
    /// default <c>CompletionConfig</c> is <c>AllCompleted()</c> (permissive),
    /// unlike Parallel's <c>AllSuccessful()</c>. This validates the headline
    /// Map-vs-Parallel behavioral difference end-to-end: a partial failure does
    /// NOT fail the workflow; it surfaces success/failure counts and per-item
    /// errors through the service round-trip and back into the rebuilt
    /// <see cref="IBatchResult{T}"/>.
    /// </summary>
    [Fact]
    public async Task Map_PartialFailure_DefaultIsPermissive_ReportsCounts()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MapPartialFailureFunction"),
            "mpartial", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "m2"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        // Permissive default means partial failure is NOT a workflow failure —
        // the workflow accepted the failure and returned a result.
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        using var doc = JsonDocument.Parse(responsePayload);
        var successCount = doc.RootElement.GetProperty("SuccessCount").GetInt32();
        var failureCount = doc.RootElement.GetProperty("FailureCount").GetInt32();
        var errorSummary = doc.RootElement.GetProperty("ErrorSummary").GetString();

        Assert.Equal(2, successCount);
        Assert.Equal(1, failureCount);
        Assert.NotNull(errorSummary);
        Assert.Contains("intentional partial failure", errorSummary);

        // History: 1 parent + 3 items = 4 ContextStarted; 3 ContextSucceeded
        // (parent + 2 ok items); 1 ContextFailed (the boom item).
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.ContextStarted) ?? 0) >= 4
              && (h.Events?.Any(e => e.EventType == EventType.ContextFailed) ?? false)
              && (h.Events?.Count(e => e.EventType == EventType.ContextSucceeded) ?? 0) >= 3,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        Assert.Equal(4, events.Count(e => e.EventType == EventType.ContextStarted));
        Assert.Equal(3, events.Count(e => e.EventType == EventType.ContextSucceeded));
        Assert.Equal(1, events.Count(e => e.EventType == EventType.ContextFailed));

        // The failing item's checkpoint preserves the exception message. Its
        // branch name is the default index ("1", the middle item).
        var failedEvent = events.SingleOrDefault(e => e.EventType == EventType.ContextFailed);
        Assert.NotNull(failedEvent);
        Assert.Equal("1", failedEvent!.Name);
        Assert.Contains("intentional partial failure",
            failedEvent.ContextFailedDetails?.Error?.Payload?.ErrorMessage ?? string.Empty);
    }
}
