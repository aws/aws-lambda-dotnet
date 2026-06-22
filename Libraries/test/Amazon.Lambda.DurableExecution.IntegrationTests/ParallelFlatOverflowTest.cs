using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class ParallelFlatOverflowTest
{
    private readonly ITestOutputHelper _output;
    public ParallelFlatOverflowTest(ITestOutputHelper output) => _output = output;

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
    /// End-to-end exercise of the LARGE-PAYLOAD OVERFLOW + ReplayChildren replay path
    /// for a <see cref="NestingType.Flat"/> parallel.
    ///
    /// Three branches each return a deterministic ~150 KB string (~450 KB aggregate),
    /// which exceeds the 256 KB checkpoint threshold, so the parallel OVERFLOWS: the SDK
    /// checkpoints a STRIPPED summary (no inline results) and sets
    /// ContextOptions.ReplayChildren=true on the parent CONTEXT op.
    ///
    /// The workflow is shaped to actually drive the RECOVERY path (ReplayChildrenAsync):
    ///   - invoke 1: branches suspend on their in-branch waits -> PENDING.
    ///   - invoke 2: the parallel re-runs the branches, overflow-checkpoints the parent
    ///     as SUCCEEDED + ReplayChildren, then suspends on the post-parallel
    ///     "post-overflow" wait (so the parallel does NOT also return in this invoke).
    ///   - invoke 3: re-enters the already-terminal SUCCEEDED + ReplayChildren parallel,
    ///     routing through ReplayChildrenAsync to RE-EXECUTE the branch bodies and
    ///     recover the stripped values (reading per-unit Status/CompletionReason from the
    ///     frozen summary, never re-checkpointing). The final result is computed from
    ///     those recovered values.
    ///
    /// This test proves the whole path works against the real durable-execution service:
    ///   1. The execution SUCCEEDED — proving the overflow checkpoint was accepted AND
    ///      ReplayChildrenAsync correctly reconstructed the aggregate result. (If the
    ///      ReplayChildren recovery path were broken, reconstruction would fail and the
    ///      execution would FAIL/TIME_OUT.)
    ///   2. Exactly ONE parent CONTEXT op exists — Flat emits no per-branch CONTEXT.
    ///   3. The three "generate" steps succeeded and re-parent to the Parallel op.
    ///   4. There were >= 3 InvocationCompleted events (initial PENDING + the resume that
    ///      overflow-checkpoints the parallel + the post-overflow resume that runs
    ///      ReplayChildrenAsync) — proving the parallel was re-entered while terminal, so
    ///      the ReplayChildren recovery path really ran.
    ///   5. The FINAL execution result (read via GetExecutionAsync after SUCCEEDED, not
    ///      the first PENDING invoke response) reports the recovered per-branch lengths
    ///      ("153600" x3) and first chars ("abc") — proving the large deterministic
    ///      values were recovered EXACTLY by ReplayChildrenAsync, not lost or defaulted.
    /// </summary>
    [Fact]
    public async Task Parallel_Flat_Overflow_ReplaysChildren_AndRecoversLargeResults()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ParallelFlatOverflowFunction"),
            "pflow", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "po1"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // SUCCEEDED alone proves the >256 KB overflow checkpoint was accepted and that
        // ReplayChildrenAsync (re-entered on the post-overflow resume) reconstructed the
        // result. A broken overflow recovery would FAIL or TIME_OUT here.
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
        // Each branch's "generate" step is the 1st inner op under that branch's own id
        // space: SHA256("<branchOpId>-1").
        var expectedStepIds = branchOpIds.Select(b => HashOpId($"{b}-1")).ToList();

        // Wait until the parent CONTEXT succeeded and all three branches' inner step +
        // wait events are visible.
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

        // 2. Exactly ONE CONTEXT operation exists — the parent Parallel op. No
        // per-branch CONTEXT events under Flat (even on the overflow path).
        var contextStartedIds = allEvents
            .Where(e => e.EventType == EventType.ContextStarted)
            .Select(e => e.Id)
            .Distinct()
            .ToList();
        Assert.Equal(new[] { parentOpId }, contextStartedIds);
        Assert.Empty(allEvents.Where(e =>
            e.EventType == EventType.ContextStarted && branchOpIds.Contains(e.Id)));

        // 3. Each branch's "generate" step re-parents to the Parallel op (NOT to its
        // virtual branch op), and the three step ids match the per-branch id space.
        var generateSteps = allEvents
            .Where(e => e.EventType == EventType.StepSucceeded && e.Name == "generate")
            .ToList();
        Assert.Equal(3, generateSteps.Count);
        Assert.All(generateSteps, e => Assert.Equal(parentOpId, e.ParentId));

        var observedStepIds = generateSteps.Select(e => e.Id).Distinct().ToList();
        Assert.Equal(3, observedStepIds.Count);
        foreach (var expected in expectedStepIds)
        {
            Assert.Contains(expected, observedStepIds);
        }

        // 4. There are at least 3 invocations: the initial PENDING, the resume that
        // overflow-checkpoints the parallel and suspends on the post-overflow wait, and
        // the post-overflow resume that re-enters the already-terminal parallel and runs
        // ReplayChildrenAsync. >= 3 proves the parallel was re-entered while terminal, so
        // the ReplayChildren recovery path really ran (>= 2 alone would only prove a
        // single suspend/resume cycle).
        var invocations = allEvents.Where(e => e.InvocationCompletedDetails != null).ToList();
        Assert.True(
            invocations.Count >= 3,
            $"Expected >= 3 InvocationCompleted events (initial + overflow-checkpoint resume + post-overflow ReplayChildren resume), got {invocations.Count}");

        // 5. The FINAL execution result (NOT the first invoke response, which is PENDING
        // because the branch waits suspend it) reports the recovered per-branch metadata.
        // Each branch produced a 150 KB (153600-byte) string built from its branch char,
        // so a correct ReplayChildrenAsync recovery yields lengths "153600,153600,153600"
        // and first chars "abc". This proves the large values were recovered EXACTLY by
        // the ReplayChildren path, not lost or defaulted.
        var execution = await deployment.GetExecutionAsync(arn!);
        Assert.NotNull(execution.Result);
        Assert.Contains("\"Lengths\":\"153600,153600,153600\"", execution.Result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"FirstChars\":\"abc\"", execution.Result, StringComparison.OrdinalIgnoreCase);
    }
}
