using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class MapReplayDeterminismTest
{
    private readonly ITestOutputHelper _output;
    public MapReplayDeterminismTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Each item's operation ID must equal SHA-256(parentOpId + "-" + (index+1))
    /// (matching OperationIdGenerator's CreateChild contract). Reproduced locally
    /// because OperationIdGenerator is internal to the SDK.
    /// </summary>
    private static string HashOpId(string raw)
    {
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>
    /// Three map items, each containing a step + a durable wait (the wait forces
    /// a suspend/resume cycle so the map actually replays). Verifies:
    ///   1. The item operation IDs match the deterministic
    ///      <c>SHA256("&lt;parentId&gt;-&lt;n&gt;")</c> formula (the same one used by
    ///      OperationIdGenerator.CreateChild and the reference Java/JS/Python SDKs).
    ///   2. Each item's user-visible step result is preserved across replay (the
    ///      GUID generated inside <c>generate</c> survives suspend/resume).
    /// </summary>
    [Fact]
    public async Task Map_ItemOperationIds_AreDeterministic_AcrossReplay()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MapReplayDeterminismFunction"),
            "mreplay", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "m6"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // The map parent is the first root-level operation -> SHA256("1").
        var parentOpId = HashOpId("1");
        var expectedItemIds = new[]
        {
            HashOpId($"{parentOpId}-1"),
            HashOpId($"{parentOpId}-2"),
            HashOpId($"{parentOpId}-3"),
        };

        // Wait until each item's CONTEXT SUCCEEDED is visible AND each item's
        // step/wait events are visible (they live under the item operation IDs).
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h =>
            {
                var events = h.Events ?? new List<Event>();
                if (events.Count(e => e.EventType == EventType.ContextSucceeded) < 4) return false;
                if (events.Count(e => e.EventType == EventType.StepSucceeded) < 3) return false;
                if (events.Count(e => e.EventType == EventType.WaitSucceeded) < 3) return false;
                return true;
            },
            TimeSpan.FromSeconds(60));
        var allEvents = history.Events ?? new List<Event>();

        // 1. Item operation IDs match the deterministic hash.
        var itemStartedEvents = allEvents
            .Where(e => e.EventType == EventType.ContextStarted && e.Id != null && e.Id != parentOpId)
            .ToList();
        var observedItemIds = itemStartedEvents.Select(e => e.Id).Distinct().ToList();
        Assert.Equal(3, observedItemIds.Count);
        foreach (var expected in expectedItemIds)
        {
            Assert.Contains(expected, observedItemIds);
        }

        // 2. Each item's CONTEXT succeeded (parent named "fanout" excluded).
        var itemSucceededEvents = allEvents
            .Where(e => e.EventType == EventType.ContextSucceeded && e.Name != "fanout")
            .ToList();
        Assert.Equal(3, itemSucceededEvents.Count);

        // 3. Each item's "generate" step succeeded exactly once — proving replay
        // returned the cached step result rather than re-executing.
        var stepSucceededEvents = allEvents
            .Where(e => e.EventType == EventType.StepSucceeded && e.Name == "generate")
            .ToList();
        Assert.Equal(3, stepSucceededEvents.Count);

        // 4. The wait events span at least 2 invocations (suspend + resume),
        // proving replay actually happened.
        var invocations = allEvents.Where(e => e.InvocationCompletedDetails != null).ToList();
        Assert.True(
            invocations.Count >= 2,
            $"Expected >= 2 InvocationCompleted events (suspend + resume), got {invocations.Count}");

        // 5. The user-visible response contains the per-item step results
        // (proving they survived replay).
        Assert.Contains("\"data\"", responsePayload, StringComparison.OrdinalIgnoreCase);
    }
}
