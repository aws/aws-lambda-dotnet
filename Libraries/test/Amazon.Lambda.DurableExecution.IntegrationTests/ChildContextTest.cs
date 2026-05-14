using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class ChildContextTest
{
    private readonly ITestOutputHelper _output;
    public ChildContextTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end RunInChildContextAsync: the workflow runs a child context that
    /// performs step + wait + step and returns a typed result. The unit tests
    /// fake state transitions in-memory; this test verifies the service actually
    /// round-trips CONTEXT START/SUCCEED records, parents the inner step/wait
    /// events under the context op, and persists the child's return value as
    /// the ContextSucceeded payload.
    /// </summary>
    [Fact]
    public async Task ChildContext_CompletesViaService()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ChildContextFunction"),
            "childctx", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "integ-test-456"}""");
        Assert.Equal(200, invokeResponse.StatusCode);

        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.EventType == EventType.ContextStarted) ?? false)
              && (h.Events?.Any(e => e.EventType == EventType.ContextSucceeded) ?? false)
              && (h.Events?.Count(e => e.StepSucceededDetails != null) ?? 0) >= 2
              && (h.Events?.Any(e => e.WaitSucceededDetails != null) ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // Exactly one child context was opened and closed successfully.
        var contextStarted = events.SingleOrDefault(e => e.EventType == EventType.ContextStarted && e.Name == "phase");
        Assert.NotNull(contextStarted);
        Assert.Equal("OrderProcessing", contextStarted!.SubType);

        // The child boundary opens and closes at the parent scope (root, ParentId=null).
        Assert.Null(contextStarted.ParentId);

        var contextSucceeded = events.SingleOrDefault(e => e.EventType == EventType.ContextSucceeded && e.Name == "phase");
        Assert.NotNull(contextSucceeded);
        Assert.Null(contextSucceeded!.ParentId);

        // The child's return value was checkpointed as the CONTEXT SUCCEED payload.
        Assert.Equal(
            "\"processed-validated-integ-test-456\"",
            contextSucceeded.ContextSucceededDetails.Result?.Payload);

        // Inner operations are parented to the context op so the service
        // visualizes them nested under the child.
        var contextOpId = contextStarted.Id;
        Assert.NotNull(contextOpId);

        var innerStepEvents = events
            .Where(e => e.EventType == EventType.StepStarted && e.ParentId == contextOpId)
            .OrderBy(e => e.EventTimestamp)
            .ToList();
        Assert.Equal(2, innerStepEvents.Count);
        Assert.Equal("validate", innerStepEvents[0].Name);
        Assert.Equal("process", innerStepEvents[1].Name);

        var innerWaitStarted = events.SingleOrDefault(
            e => e.WaitStartedDetails != null && e.Name == "short_wait" && e.ParentId == contextOpId);
        Assert.NotNull(innerWaitStarted);
        Assert.Equal(2, innerWaitStarted!.WaitStartedDetails.Duration);

        // Inner step results chain: validate -> wait -> process.
        var stepResults = events
            .Where(e => e.StepSucceededDetails != null && e.ParentId == contextOpId)
            .OrderBy(e => e.EventTimestamp)
            .Select(e => (Name: e.Name, Payload: e.StepSucceededDetails.Result?.Payload?.Trim('"')))
            .ToList();
        Assert.Equal(2, stepResults.Count);
        Assert.Equal("validate", stepResults[0].Name);
        Assert.Equal("validated-integ-test-456", stepResults[0].Payload);
        Assert.Equal("process", stepResults[1].Name);
        Assert.Equal("processed-validated-integ-test-456", stepResults[1].Payload);

        // Every inner step/wait event for this workflow is parented under the
        // child context — the child is a single observability boundary.
        var innerOpEvents = events
            .Where(e => e.StepStartedDetails != null
                     || e.StepSucceededDetails != null
                     || e.StepFailedDetails != null
                     || e.WaitStartedDetails != null
                     || e.WaitSucceededDetails != null)
            .ToList();
        Assert.NotEmpty(innerOpEvents);
        Assert.All(innerOpEvents, e => Assert.Equal(contextOpId, e.ParentId));
    }
}
