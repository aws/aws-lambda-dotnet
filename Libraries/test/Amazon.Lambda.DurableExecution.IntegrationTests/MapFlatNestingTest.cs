using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class MapFlatNestingTest
{
    private readonly ITestOutputHelper _output;
    public MapFlatNestingTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Reproduces the deterministic operation ID the SDK assigns. Item op ids are
    /// SHA-256(parentOpId + "-" + (index+1)); inner-op ids nest the same way under
    /// the item op id. Reproduced locally because OperationIdGenerator is internal
    /// to the SDK.
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
    /// End-to-end <see cref="NestingType.Flat"/> map: three items, each with a
    /// step + a durable wait (the wait forces a suspend/resume cycle so the map
    /// actually replays). Verifies the Flat-specific contract against the real
    /// durable-execution service:
    ///   1. NO per-item CONTEXT events are emitted — only the parent Map CONTEXT.
    ///   2. Each item's inner step/wait ops RE-PARENT to the Map op (the nearest
    ///      non-virtual ancestor), since the virtual item emits no CONTEXT
    ///      checkpoint to reference as a parent.
    ///   3. Inner-op ids are still derived from the item op id space.
    ///   4. The per-item result survives replay (read back from the inline parent
    ///      payload, not a per-item checkpoint).
    /// </summary>
    [Fact]
    public async Task Map_Flat_SuppressesItemContexts_AndReparentsInnerOps()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MapFlatNestingFunction"),
            "mflat", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "mf1"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // The map parent is the first root-level operation -> SHA256("1").
        var parentOpId = HashOpId("1");
        var itemOpIds = new[]
        {
            HashOpId($"{parentOpId}-1"),
            HashOpId($"{parentOpId}-2"),
            HashOpId($"{parentOpId}-3"),
        };
        // Each item's "generate" step is the 1st inner op under that item's own
        // id space: SHA256("<itemOpId>-1").
        var expectedStepIds = itemOpIds.Select(i => HashOpId($"{i}-1")).ToList();

        // Wait until the parent CONTEXT succeeded and all three items' inner step
        // + wait events are visible.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h =>
            {
                var events = h.Events ?? new List<Event>();
                if (events.Count(e => e.EventType == EventType.ContextSucceeded) < 1) return false;
                if (events.Count(e => e.EventType == EventType.StepSucceeded) < 3) return false;
                if (events.Count(e => e.EventType == EventType.WaitSucceeded) < 3) return false;
                return true;
            },
            TimeSpan.FromSeconds(60));
        var allEvents = history.Events ?? new List<Event>();

        // 1. Exactly ONE CONTEXT operation exists — the parent Map op. No per-item
        // CONTEXT events under Flat.
        var contextStartedIds = allEvents
            .Where(e => e.EventType == EventType.ContextStarted)
            .Select(e => e.Id)
            .Distinct()
            .ToList();
        Assert.Equal(new[] { parentOpId }, contextStartedIds);
        Assert.Empty(allEvents.Where(e =>
            e.EventType == EventType.ContextStarted && itemOpIds.Contains(e.Id)));

        // 2. Each item's "generate" step re-parents to the Map op (NOT to its
        // virtual item op).
        var generateSteps = allEvents
            .Where(e => e.EventType == EventType.StepSucceeded && e.Name == "generate")
            .ToList();
        Assert.Equal(3, generateSteps.Count);
        Assert.All(generateSteps, e => Assert.Equal(parentOpId, e.ParentId));

        // 3. ...but the step ids are still derived from the per-item id space, so
        // the three items' first steps are distinct and match the expected
        // SHA256("<itemOpId>-1") values.
        var observedStepIds = generateSteps.Select(e => e.Id).Distinct().ToList();
        Assert.Equal(3, observedStepIds.Count);
        foreach (var expected in expectedStepIds)
        {
            Assert.Contains(expected, observedStepIds);
        }

        // 4. The wait events span at least 2 invocations (suspend + resume),
        // proving replay actually happened with no per-item checkpoint.
        var invocations = allEvents.Where(e => e.InvocationCompletedDetails != null).ToList();
        Assert.True(
            invocations.Count >= 2,
            $"Expected >= 2 InvocationCompleted events (suspend + resume), got {invocations.Count}");

        // 5. The user-visible response carries the joined per-item results.
        Assert.Contains("\"data\"", responsePayload, StringComparison.OrdinalIgnoreCase);
    }
}
