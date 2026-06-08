using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

public class Function
{
    // Each branch produces a ~150 KB string. Three branches => ~450 KB of inline
    // results, comfortably over the 256 KB checkpoint threshold. This forces the
    // FLAT parallel aggregate to OVERFLOW: the SDK checkpoints a stripped summary
    // (no inline results) and sets ContextOptions.ReplayChildren=true on the parent
    // CONTEXT op, keeping the full result in memory for the current invoke.
    private const int BranchPayloadSize = 150 * 1024; // 153600 bytes

    public static async Task Main(string[] args)
    {
        var handler = new Function();
        var serializer = new DefaultLambdaJsonSerializer();
        using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<DurableExecutionInvocationInput, DurableExecutionInvocationOutput>(handler.Handler, serializer);
        using var bootstrap = new LambdaBootstrap(handlerWrapper);
        await bootstrap.RunAsync();
    }

    public Task<DurableExecutionInvocationOutput> Handler(
        DurableExecutionInvocationInput input, ILambdaContext context)
        => DurableFunction.WrapAsync<TestEvent, TestResult>(Workflow, input, context);

    private async Task<TestResult> Workflow(TestEvent input, IDurableContext context)
    {
        // Three branches run under NestingType.Flat. Each branch generates a LARGE
        // (~150 KB) string inside a step, then does an in-branch durable wait. The
        // combined ~450 KB aggregate exceeds the 256 KB threshold, so the parallel
        // OVERFLOWS: the SDK checkpoints a stripped summary (no inline per-branch
        // results) + ReplayChildren=true on the parent CONTEXT op.
        //
        // To actually exercise the RECOVERY path (ReplayChildrenAsync), the
        // already-overflowed parallel must be re-entered on a FRESH invoke while it is
        // already terminal (SUCCEEDED + ReplayChildren). The in-branch waits alone are
        // NOT enough: the resume invoke that overflow-checkpoints the parallel also
        // immediately returns SUCCEEDED, so the parallel goes STARTED -> SUCCEEDED in a
        // single invoke and ReplayChildrenAsync is never hit. So we add a durable wait
        // AFTER ParallelAsync returns (the "post-overflow" wait below): the overflow
        // invoke suspends on that wait, and the NEXT invoke re-enters the already-
        // terminal overflowed parallel and routes through ReplayChildrenAsync to
        // RE-EXECUTE the branch bodies and recover the stripped values (reading per-unit
        // Status/CompletionReason from the frozen summary, never re-checkpointing).
        //
        // The branch values are built DETERMINISTICALLY from the branch character
        // (NOT Guid/random/DateTime). This is critical: the value produced on the
        // original execution must be IDENTICAL to the value produced on replay
        // re-execution, so the test can prove the large values were recovered exactly
        // rather than lost or defaulted.
        var batch = await context.ParallelAsync(
            new[]
            {
                new DurableBranch<string>("a", (ctx, _) => BranchAsync(ctx, 'a')),
                new DurableBranch<string>("b", (ctx, _) => BranchAsync(ctx, 'b')),
                new DurableBranch<string>("c", (ctx, _) => BranchAsync(ctx, 'c')),
            },
            name: "fanout",
            config: new ParallelConfig { NestingType = NestingType.Flat });

        // Force another invocation so the already-overflowed parallel is re-entered
        // (already SUCCEEDED + ReplayChildren) and replayed via ReplayChildrenAsync,
        // which re-executes the branch bodies to recover the stripped >256 KB results.
        await context.WaitAsync(TimeSpan.FromSeconds(1), name: "post-overflow");

        // Compute the verifiable metadata AFTER the post-overflow wait: on the final
        // invoke these results come from ReplayChildrenAsync's re-execution, which is
        // exactly the recovery we want to prove survives.
        var results = batch.GetResults().ToList();

        // Keep the returned payload SMALL (well under the 6 MB Lambda response
        // limit): do NOT echo the ~450 KB back. Instead return verifiable metadata
        // proving the large values were recovered on replay:
        //   - Lengths: comma-joined per-branch result LENGTHS (e.g. "153600,153600,153600")
        //   - FirstChars: the first character of each recovered branch result, in order
        //     (e.g. "abc") — confirms each branch's deterministic content survived.
        var lengths = string.Join(",", results.Select(r => r.Length));
        var firstChars = string.Concat(results.Select(r => r.Length > 0 ? r[0] : '?'));

        return new TestResult { Status = "completed", Lengths = lengths, FirstChars = firstChars };
    }

    private static async Task<string> BranchAsync(IDurableContext ctx, char branchChar)
    {
        // Deterministic large payload: same branchChar => same string on original
        // execution and on replay re-execution. ~150 KB per branch.
        var large = await ctx.StepAsync(
            async (_, _) => { await Task.CompletedTask; return new string(branchChar, BranchPayloadSize); },
            name: "generate");

        // Force a suspend/resume cycle to trigger replay of the (overflowed) parallel.
        await ctx.WaitAsync(TimeSpan.FromSeconds(2), name: "boundary");

        return large;
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Lengths { get; set; } public string? FirstChars { get; set; } }
