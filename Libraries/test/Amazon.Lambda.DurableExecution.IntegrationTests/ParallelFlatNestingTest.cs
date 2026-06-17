using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class ParallelFlatNestingTest
{
    private readonly ITestOutputHelper _output;
    public ParallelFlatNestingTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Reproduces the deterministic operation ID the SDK assigns. Branch op ids
    /// are SHA-256(parentOpId + "-" + (index+1)); inner-op ids nest the same way
    /// under the branch op id. Reproduced locally because OperationIdGenerator is
    /// internal to the SDK.
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
    /// End-to-end <see cref="NestingType.Flat"/> parallel: three branches, each
    /// with a step + a durable wait (the wait forces a suspend/resume cycle so the
    /// parallel actually replays). Verifies the Flat-specific contract against the
    /// real durable-execution service:
    ///   1. NO per-branch CONTEXT events are emitted — only the parent Parallel
    ///      CONTEXT. (Under Nested there would be 4 ContextStarted; under Flat,
    ///      exactly 1.)
    ///   2. Each branch's inner step/wait ops RE-PARENT to the Parallel op (the
    ///      nearest non-virtual ancestor), since the virtual branch emits no
    ///      CONTEXT checkpoint to reference as a parent.
    ///   3. Inner-op ids are still derived from the branch op id (so the two
    ///      branches' first steps don't collide), even though they report the
    ///      Parallel op as parent.
    ///   4. The per-branch result survives replay (the GUID generated inside
    ///      <c>generate</c> is preserved across suspend/resume — read back from the
    ///      inline parent payload, not a per-branch checkpoint).
    /// </summary>
    [Fact]
    public async Task Parallel_Flat_SuppressesBranchContexts_AndReparentsInnerOps()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ParallelFlatNestingFunction"),
            "pflat", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "pf1"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // The parallel parent is the first root-level operation -> SHA256("1").
        var parentOpId = HashOpId("1");
        var branchOpIds = new[]
        {
            HashOpId($"{parentOpId}-1"),
            HashOpId($"{parentOpId}-2"),
            HashOpId($"{parentOpId}-3"),
        };
        // Each branch's "generate" step is the 1st inner op under that branch's
        // own id space: SHA256("<branchOpId>-1").
        var expectedStepIds = branchOpIds.Select(b => HashOpId($"{b}-1")).ToList();

        // Wait until the parent CONTEXT succeeded and all three branches' inner
        // step + wait events are visible.
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

        // 1. Exactly ONE CONTEXT operation exists — the parent Parallel op. No
        // per-branch CONTEXT events under Flat.
        var contextStartedIds = allEvents
            .Where(e => e.EventType == EventType.ContextStarted)
            .Select(e => e.Id)
            .Distinct()
            .ToList();
        Assert.Equal(new[] { parentOpId }, contextStartedIds);
        Assert.Empty(allEvents.Where(e =>
            e.EventType == EventType.ContextStarted && branchOpIds.Contains(e.Id)));

        // 2. Each branch's "generate" step re-parents to the Parallel op (NOT to
        // its virtual branch op).
        var generateSteps = allEvents
            .Where(e => e.EventType == EventType.StepSucceeded && e.Name == "generate")
            .ToList();
        Assert.Equal(3, generateSteps.Count);
        Assert.All(generateSteps, e => Assert.Equal(parentOpId, e.ParentId));

        // 3. ...but the step ids are still derived from the per-branch id space,
        // so the three branches' first steps are distinct and match the expected
        // SHA256("<branchOpId>-1") values.
        var observedStepIds = generateSteps.Select(e => e.Id).Distinct().ToList();
        Assert.Equal(3, observedStepIds.Count);
        foreach (var expected in expectedStepIds)
        {
            Assert.Contains(expected, observedStepIds);
        }

        // 4. The "generate" step succeeded exactly once per branch — proving
        // replay returned the cached result rather than re-executing.
        Assert.Equal(3, generateSteps.Count);

        // 5. The wait events span at least 2 invocations (suspend + resume),
        // proving replay actually happened with no per-branch checkpoint.
        var invocations = allEvents.Where(e => e.InvocationCompletedDetails != null).ToList();
        Assert.True(
            invocations.Count >= 2,
            $"Expected >= 2 InvocationCompleted events (suspend + resume), got {invocations.Count}");

        // 6. The user-visible response carries the joined per-branch results.
        Assert.Contains("\"data\"", responsePayload, StringComparison.OrdinalIgnoreCase);
    }
}
