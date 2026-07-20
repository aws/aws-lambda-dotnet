// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution;

namespace Amazon.Lambda.DurableExecution.Testing.Shared;

/// <summary>
/// Real-world durable workflows shared verbatim between the local
/// (<c>DurableTestRunner</c>) and cloud (<c>CloudDurableTestRunner</c>) backends.
/// The local test project references these methods directly as handler delegates;
/// each deployed <c>TestFunctions/&lt;X&gt;</c> wraps the same method behind a
/// <c>bootstrap</c> entry point. Because both backends run identical workflow
/// code, the portable scenarios in <c>PortableScenarios</c> assert the same way
/// regardless of where the workflow executes.
/// </summary>
/// <remarks>
/// Workflow code must be deterministic across replays — same operations, same
/// order, same names. Every durable call here is given an explicit name so the
/// scenarios can locate operations by name on either backend.
/// </remarks>
public static class FulfillmentWorkflow
{
    /// <summary>
    /// An order-fulfillment workflow that exercises the full breadth of the
    /// durable operation surface in one cohesive flow: a validation
    /// <see cref="IDurableContext.StepAsync{T}"/>, a
    /// <see cref="IDurableContext.ParallelAsync{T}(IReadOnlyList{DurableBranch{T}}, string?, ParallelConfig?, System.Threading.CancellationToken)"/>
    /// inventory fan-out, a <see cref="IDurableContext.MapAsync{TItem, TResult}"/>
    /// over the line items, a <see cref="IDurableContext.WaitAsync"/> settlement
    /// delay, a <see cref="IDurableContext.WaitForConditionAsync{TState}"/> poll
    /// until payment settles, and a
    /// <see cref="IDurableContext.RunInChildContextAsync{T}"/> shipping
    /// sub-workflow.
    /// </summary>
    public static async Task<FulfillmentResult> RunAsync(OrderRequest input, IDurableContext context)
    {
        // 1. Validate the incoming order (single checkpointed step).
        var validated = await context.StepAsync(
            async (_, _) =>
            {
                await Task.CompletedTask;
                if (string.IsNullOrEmpty(input.OrderId))
                    throw new InvalidOperationException("OrderId is required");
                return input.Items?.Length ?? 0;
            },
            name: "validate_order");

        // 2. Confirm inventory from two independent sources concurrently, then
        //    checkpoint the aggregate in a step. Capturing the sum in a step (vs.
        //    a plain local re-derived on every replay) makes the value survive the
        //    later suspends (wait + condition poll) deterministically on both
        //    backends.
        var inventory = await context.ParallelAsync(
            new[]
            {
                new DurableBranch<int>("warehouse", async (_, _) => { await Task.CompletedTask; return 100; }),
                new DurableBranch<int>("supplier",  async (_, _) => { await Task.CompletedTask; return 50; }),
            },
            name: "check_inventory");
        var inventoryResults = inventory.GetResults();
        var availableStock = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return inventoryResults.Sum(); },
            name: "total_stock");

        // 3. Price every line item concurrently (map fan-out), then checkpoint the
        //    order total the same way.
        var items = input.Items ?? Array.Empty<string>();
        var priced = await context.MapAsync(
            items,
            async (ctx, sku, index, _, _) =>
                await ctx.StepAsync(
                    async (_, _) => { await Task.CompletedTask; return (index + 1) * 10m; },
                    name: "price"),
            name: "price_items",
            config: new MapConfig<string> { ItemNamer = (sku, index) => $"sku-{sku}" });
        var pricedResults = priced.GetResults();
        var orderTotal = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return pricedResults.Sum(); },
            name: "total_price");

        // 4. Brief settlement delay (suspends with no compute charge in the cloud;
        //    completes immediately under the local runner's SkipTime default).
        await context.WaitAsync(TimeSpan.FromSeconds(1), name: "settlement_delay");

        // 5. Poll until the payment processor reports the charge settled. The
        //    per-poll state carries the attempt counter across re-invocations.
        var settlement = await context.WaitForConditionAsync<SettlementState>(
            check: async (state, ctx, _) =>
            {
                await Task.CompletedTask;
                return new SettlementState(state.Polls + 1);
            },
            config: new WaitForConditionConfig<SettlementState>
            {
                InitialState = new SettlementState(0),
                WaitStrategy = WaitStrategy.Fixed<SettlementState>(
                    delay: TimeSpan.FromSeconds(1),
                    maxAttempts: 10,
                    isDone: s => s.Polls >= 2),
            },
            name: "await_settlement");

        // 6. Ship the order inside a child context so packing + labeling group
        //    into a single CONTEXT operation.
        var trackingId = await context.RunInChildContextAsync(
            async (childCtx, _) =>
            {
                await childCtx.StepAsync(async (_, _) => { await Task.CompletedTask; return "packed"; }, name: "pack");
                return await childCtx.StepAsync(
                    async (_, _) => { await Task.CompletedTask; return $"TRK-{input.OrderId}"; },
                    name: "label");
            },
            name: "ship_order");

        return new FulfillmentResult
        {
            Status = "fulfilled",
            OrderId = input.OrderId,
            ItemCount = validated,
            AvailableStock = availableStock,
            OrderTotal = orderTotal,
            SettlementPolls = settlement.Polls,
            TrackingId = trackingId,
        };
    }
}

/// <summary>
/// A human-in-the-loop approval workflow. It allocates a callback via
/// <see cref="IDurableContext.CreateCallbackAsync{T}"/> and suspends on
/// <see cref="ICallback{T}.GetResultAsync"/>. The "external system" that resolves
/// the callback is the test harness itself — the portable scenario polls for the
/// pending callback and delivers a result through the runner's
/// <c>SendCallback*</c> methods. No paired approver Lambda is needed because the
/// runner's <c>StartAsync</c> returns the execution ARN immediately while the
/// workflow stays suspended.
/// </summary>
public static class ApprovalWorkflow
{
    public static async Task<ApprovalResult> RunAsync(ApprovalRequest input, IDurableContext context)
    {
        // Pre-work step proves the workflow makes progress before suspending.
        var ticket = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return $"ticket-{input.RequestId}"; },
            name: "open_ticket");

        try
        {
            var callback = await context.CreateCallbackAsync<ApprovalDecision>(name: "approval");
            var decision = await callback.GetResultAsync();

            return new ApprovalResult
            {
                Status = "decided",
                Ticket = ticket,
                Approved = decision.Approved,
                Approver = decision.Approver,
            };
        }
        catch (CallbackException ex)
        {
            // The external system rejected the request (SendCallbackFailureAsync).
            return new ApprovalResult
            {
                Status = "rejected",
                Ticket = ticket,
                Approved = false,
                Approver = ex.ErrorType,
            };
        }
    }
}

/// <summary>
/// A workflow that durably invokes a downstream function via
/// <see cref="IDurableContext.InvokeAsync{TPayload, TResult}"/>. The downstream
/// target is read from the <c>DOWNSTREAM_FUNCTION_ARN</c> environment variable
/// (set by the cloud deployment harness to the deployed downstream's qualified
/// ARN); when unset it falls back to <see cref="DownstreamFunctionName"/>, which
/// the local runner registers as a sibling. Both code paths resolve to the same
/// short name, so the workflow body is identical on either backend.
/// </summary>
public static class ChainedInvokeWorkflow
{
    /// <summary>Short name the local runner registers the durable sibling under.</summary>
    public const string DownstreamFunctionName = "fulfillment-downstream";

    public static async Task<ChainedInvokeResult> RunAsync(ChainedInvokeRequest input, IDurableContext context)
    {
        var target = System.Environment.GetEnvironmentVariable("DOWNSTREAM_FUNCTION_ARN")
            ?? DownstreamFunctionName;

        var enriched = await context.InvokeAsync<ChildRequest, ChildResult>(
            target,
            new ChildRequest { Sku = input.Sku, Quantity = input.Quantity },
            name: "enrich_order");

        return new ChainedInvokeResult
        {
            Status = "enriched",
            Sku = input.Sku,
            LineTotal = enriched.LineTotal,
            Warehouse = enriched.Warehouse,
        };
    }

    /// <summary>
    /// The downstream durable workflow. Deployed as its own function in the cloud
    /// and registered via <c>RegisterDurableFunction</c> locally.
    /// </summary>
    public static async Task<ChildResult> DownstreamAsync(ChildRequest input, IDurableContext context)
    {
        var lineTotal = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return input.Quantity * 25m; },
            name: "compute_line_total");

        return new ChildResult
        {
            LineTotal = lineTotal,
            Warehouse = $"wh-{input.Sku}",
        };
    }
}

/// <summary>
/// A workflow with a flaky step that throws on its first
/// <see cref="MaxAttempts"/>-1 attempts and succeeds on the last, exercising the
/// step retry path. The retry delays are real (2s, 4s, …) on the cloud runtime
/// and skipped by the local runner (<c>SkipTime</c>), so the workflow body is
/// identical on both backends — only what each backend can observe differs.
/// </summary>
public static class RetryWorkflow
{
    /// <summary>Attempts the flaky step is allowed before giving up.</summary>
    public const int MaxAttempts = 3;

    /// <summary>Name of the flaky step (for both result inspection and history assertions).</summary>
    public const string StepName = "flaky_step";

    public static async Task<RetryResult> RunAsync(RetryRequest input, IDurableContext context)
    {
        var outcome = await context.StepAsync<string>(
            async (ctx, _) =>
            {
                await Task.CompletedTask;
                if (ctx.AttemptNumber < MaxAttempts)
                    throw new InvalidOperationException($"flake on attempt {ctx.AttemptNumber}");
                return $"ok on attempt {ctx.AttemptNumber}";
            },
            name: StepName,
            config: new StepConfig
            {
                RetryStrategy = RetryStrategy.Exponential(
                    maxAttempts: MaxAttempts,
                    initialDelay: TimeSpan.FromSeconds(2),
                    maxDelay: TimeSpan.FromSeconds(10),
                    backoffRate: 2.0,
                    jitter: JitterStrategy.None)
            });

        return new RetryResult { Status = "completed", Data = outcome };
    }
}

/// <summary>
/// A workflow whose body is a single <see cref="IDurableContext.WaitAsync"/> with
/// no steps. On the cloud runtime the wait suspends and the service re-invokes
/// after the timer; the local runner skips the delay. Exercises the wait
/// operation in isolation.
/// </summary>
public static class WaitOnlyWorkflow
{
    /// <summary>The wait's duration in seconds (asserted by the cloud-only test).</summary>
    public const int WaitSeconds = 5;

    /// <summary>Name of the wait operation.</summary>
    public const string WaitName = "only_wait";

    public static async Task<WaitResult> RunAsync(WaitRequest input, IDurableContext context)
    {
        await context.WaitAsync(TimeSpan.FromSeconds(WaitSeconds), name: WaitName);
        return new WaitResult { Status = "completed", Data = "wait_only" };
    }
}

/// <summary>
/// Five sequential steps, each chaining its output onto the previous one's. Each
/// step is checkpointed, so on the cloud runtime every step runs exactly once
/// even across replays. Exercises straightforward multi-step checkpointing.
/// </summary>
public static class MultipleStepsWorkflow
{
    public static async Task<StepsResult> RunAsync(StepsRequest input, IDurableContext context)
    {
        var step1 = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return $"a-{input.OrderId}"; },
            name: "step_1");

        var step2 = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return $"{step1}-b"; },
            name: "step_2");

        var step3 = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return $"{step2}-c"; },
            name: "step_3");

        var step4 = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return $"{step3}-d"; },
            name: "step_4");

        var step5 = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return $"{step4}-e"; },
            name: "step_5");

        return new StepsResult { Status = "completed", Data = step5 };
    }
}

/// <summary>
/// A workflow whose single step throws (no retry configured), so the workflow
/// terminates Failed on the first attempt. Exercises failure propagation: the
/// step's exception becomes the execution's terminal error.
/// </summary>
public static class StepFailsWorkflow
{
    /// <summary>The message the failing step throws (asserted on both backends).</summary>
    public const string FailureMessage = "intentional failure for integration test";

    public static async Task<StepsResult> RunAsync(StepsRequest input, IDurableContext context)
    {
        await context.StepAsync<string>(
            async (_, _) =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException(FailureMessage);
            },
            name: "fail_step");

        return new StepsResult { Status = "should_not_reach" };
    }
}

/// <summary>
/// A step, then a wait, then a step — the wait forces a suspend/resume cycle
/// between the two steps. Exercises that a workflow correctly threads a value
/// from before a suspension to after it (the second step chains off the first).
/// </summary>
public static class StepWaitStepWorkflow
{
    /// <summary>The middle wait's duration in seconds (asserted by the cloud-only test).</summary>
    public const int WaitSeconds = 3;

    /// <summary>Name of the middle wait operation.</summary>
    public const string WaitName = "short_wait";

    public static async Task<StepsResult> RunAsync(StepsRequest input, IDurableContext context)
    {
        var step1 = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return $"validated-{input.OrderId}"; },
            name: "validate");

        await context.WaitAsync(TimeSpan.FromSeconds(WaitSeconds), name: WaitName);

        var step2 = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return $"processed-{step1}"; },
            name: "process");

        return new StepsResult { Status = "completed", Data = step2 };
    }
}

/// <summary>
/// Step, then a longer (15s) wait, then a step. Same shape as
/// <see cref="StepWaitStepWorkflow"/> but with a longer suspension; the
/// cloud-only test asserts the real 15s timer fired.
/// </summary>
public static class LongerWaitWorkflow
{
    /// <summary>The wait's duration in seconds (asserted by the cloud-only test).</summary>
    public const int WaitSeconds = 15;

    /// <summary>Name of the wait operation.</summary>
    public const string WaitName = "long_wait";

    public static async Task<StepsResult> RunAsync(StepsRequest input, IDurableContext context)
    {
        var step1 = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return $"started-{input.OrderId}"; },
            name: "before_wait");

        await context.WaitAsync(TimeSpan.FromSeconds(WaitSeconds), name: WaitName);

        var step2 = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return $"after_wait-{step1}"; },
            name: "after_wait");

        return new StepsResult { Status = "completed", Data = step2 };
    }
}

/// <summary>
/// A step that always throws, with a 3-attempt retry policy. After all attempts
/// are exhausted the workflow terminates Failed with the last attempt's
/// exception. Exercises retry exhaustion (the failure counterpart to
/// <see cref="RetryWorkflow"/>).
/// </summary>
public static class RetryExhaustionWorkflow
{
    /// <summary>Attempts before the step gives up.</summary>
    public const int MaxAttempts = 3;

    /// <summary>Name of the always-failing step.</summary>
    public const string StepName = "always_fails_step";

    public static async Task<StepsResult> RunAsync(StepsRequest input, IDurableContext context)
    {
        var result = await context.StepAsync<string>(
            async (ctx, _) =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException($"always-fails attempt {ctx.AttemptNumber}");
            },
            name: StepName,
            config: new StepConfig
            {
                RetryStrategy = RetryStrategy.Exponential(
                    maxAttempts: MaxAttempts,
                    initialDelay: TimeSpan.FromSeconds(2),
                    maxDelay: TimeSpan.FromSeconds(10),
                    backoffRate: 2.0,
                    jitter: JitterStrategy.None)
            });

        return new StepsResult { Status = "completed", Data = result };
    }
}

/// <summary>
/// Polls a condition that increments a counter each attempt and completes once
/// the counter reaches 3. Each poll is a separate invocation; the per-poll state
/// is carried across the RETRY checkpoints. Exercises the happy path of
/// <see cref="IDurableContext.WaitForConditionAsync{TState}"/>.
/// </summary>
public static class WaitForConditionHappyWorkflow
{
    public const string PollName = "happy_poll";

    public static async Task<ConditionResult> RunAsync(ConditionRequest input, IDurableContext context)
    {
        var finalState = await context.WaitForConditionAsync<ConditionCounter>(
            check: async (state, ctx, _) =>
            {
                await Task.CompletedTask;
                return new ConditionCounter(state.Counter + 1, ctx.AttemptNumber);
            },
            config: new WaitForConditionConfig<ConditionCounter>
            {
                InitialState = new ConditionCounter(0, 0),
                WaitStrategy = WaitStrategy.Fixed<ConditionCounter>(
                    delay: TimeSpan.FromSeconds(2),
                    maxAttempts: 10,
                    isDone: s => s.Counter >= 3)
            },
            name: PollName);

        return new ConditionResult
        {
            Status = "completed",
            Counter = finalState.Counter,
            AttemptsTaken = finalState.AttemptNumber,
        };
    }
}

/// <summary>
/// Polls a condition that is never satisfied, so the strategy exhausts its
/// max attempts and throws <see cref="WaitForConditionException"/>. The workflow
/// catches it and reports the exhausted-attempt count, so the execution itself
/// succeeds. Exercises the max-attempts exhaustion path.
/// </summary>
public static class WaitForConditionMaxAttemptsWorkflow
{
    public const int MaxAttempts = 3;
    public const string PollName = "exhausting_poll";

    public static async Task<ConditionResult> RunAsync(ConditionRequest input, IDurableContext context)
    {
        try
        {
            await context.WaitForConditionAsync<int>(
                check: async (state, _, _) => { await Task.CompletedTask; return state + 1; },
                config: new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.Fixed<int>(
                        delay: TimeSpan.FromSeconds(1),
                        maxAttempts: MaxAttempts,
                        isDone: _ => false)
                },
                name: PollName);

            return new ConditionResult { Status = "should_not_reach", AttemptsExhausted = -1 };
        }
        catch (WaitForConditionException ex)
        {
            return new ConditionResult { Status = "exhausted", AttemptsExhausted = ex.AttemptsExhausted };
        }
    }
}

/// <summary>
/// Polls with an exponential backoff strategy, completing on attempt 3.
/// Exercises the exponential wait strategy.
/// </summary>
public static class WaitForConditionExponentialWorkflow
{
    public const string PollName = "exp_poll";

    public static async Task<ConditionResult> RunAsync(ConditionRequest input, IDurableContext context)
    {
        var finalState = await context.WaitForConditionAsync<ConditionDone>(
            check: async (state, ctx, _) =>
            {
                await Task.CompletedTask;
                return new ConditionDone(ctx.AttemptNumber >= 3, ctx.AttemptNumber);
            },
            config: new WaitForConditionConfig<ConditionDone>
            {
                InitialState = new ConditionDone(false, 0),
                WaitStrategy = WaitStrategy.Exponential<ConditionDone>(
                    maxAttempts: 5,
                    initialDelay: TimeSpan.FromSeconds(1),
                    maxDelay: TimeSpan.FromSeconds(4),
                    backoffRate: 1.5,
                    jitter: JitterStrategy.None,
                    isDone: s => s.Done)
            },
            name: PollName);

        return new ConditionResult
        {
            Status = "completed",
            AttemptsTaken = finalState.AttemptNumber,
            Done = finalState.Done,
        };
    }
}

/// <summary>
/// The condition check throws on attempt 2. The thrown exception is checkpointed
/// and surfaced as a <see cref="StepException"/> carrying the original exception
/// type; the workflow catches it and reports the captured error so the execution
/// succeeds. Exercises a user check throwing inside WaitForCondition.
/// </summary>
public static class WaitForConditionUserCheckThrowsWorkflow
{
    public const string PollName = "throwing_poll";
    public const string FailureMessage = "intentional check failure on attempt 2";

    public static async Task<ConditionResult> RunAsync(ConditionRequest input, IDurableContext context)
    {
        try
        {
            await context.WaitForConditionAsync<int>(
                check: async (state, ctx, _) =>
                {
                    await Task.CompletedTask;
                    if (ctx.AttemptNumber == 2)
                        throw new InvalidOperationException(FailureMessage);
                    return state + 1;
                },
                config: new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.Fixed<int>(
                        delay: TimeSpan.FromSeconds(1),
                        maxAttempts: 10,
                        isDone: _ => false)
                },
                name: PollName);

            return new ConditionResult { Status = "should_not_reach", ErrorType = null };
        }
        catch (StepException ex)
        {
            return new ConditionResult
            {
                Status = "caught_step_exception",
                ErrorType = ex.ErrorType,
                ErrorMessage = ex.Message,
            };
        }
    }
}

/// <summary>
/// Parallel workflows exercising <see cref="IDurableContext.ParallelAsync{T}(IReadOnlyList{DurableBranch{T}}, string?, ParallelConfig?, System.Threading.CancellationToken)"/>
/// in its various completion modes. Each returns a <see cref="BatchResult"/>;
/// the unused fields stay at their defaults per scenario.
/// </summary>
public static class ParallelWorkflows
{
    /// <summary>Three branches all succeed; results are joined into Data.</summary>
    public static async Task<BatchResult> HappyAsync(BatchRequest input, IDurableContext context)
    {
        var batch = await context.ParallelAsync(
            new[]
            {
                new DurableBranch<string>("alpha", async (_, _) => { await Task.CompletedTask; return $"alpha-{input.OrderId}"; }),
                new DurableBranch<string>("beta",  async (_, _) => { await Task.CompletedTask; return $"beta-{input.OrderId}"; }),
                new DurableBranch<string>("gamma", async (_, _) => { await Task.CompletedTask; return $"gamma-{input.OrderId}"; }),
            },
            name: "fanout");

        return new BatchResult { Status = "completed", Data = string.Join(",", batch.GetResults()) };
    }

    /// <summary>One branch throws; AllCompleted tolerates it so the workflow still succeeds.</summary>
    public static async Task<BatchResult> PartialFailureAsync(BatchRequest input, IDurableContext context)
    {
        var batch = await context.ParallelAsync(
            new[]
            {
                new DurableBranch<string>("ok1", async (_, _) => { await Task.CompletedTask; return "first"; }),
                new DurableBranch<string>("boom", async (_, _) =>
                {
                    await Task.CompletedTask;
                    throw new InvalidOperationException("intentional partial failure");
                }),
                new DurableBranch<string>("ok2", async (_, _) => { await Task.CompletedTask; return "third"; }),
            },
            name: "partial",
            config: new ParallelConfig { CompletionConfig = CompletionConfig.AllCompleted() });

        var errorSummary = string.Join("|", batch.GetErrors().Select(e => $"{e.GetType().Name}:{e.Message}"));
        return new BatchResult
        {
            Status = "completed",
            SuccessCount = batch.SuccessCount,
            FailureCount = batch.FailureCount,
            ErrorSummary = errorSummary,
        };
    }

    /// <summary>Branches with staggered waits race; FirstSuccessful short-circuits on the fastest.</summary>
    public static async Task<BatchResult> FirstSuccessfulAsync(BatchRequest input, IDurableContext context)
    {
        var batch = await context.ParallelAsync(
            new[]
            {
                new DurableBranch<int>("slowest", async (ctx, _) => { await ctx.WaitAsync(TimeSpan.FromSeconds(8), name: "wait_3"); return 3; }),
                new DurableBranch<int>("fastest", async (ctx, _) => { await ctx.WaitAsync(TimeSpan.FromSeconds(1), name: "wait_0"); return 0; }),
                new DurableBranch<int>("mid1", async (ctx, _) => { await ctx.WaitAsync(TimeSpan.FromSeconds(5), name: "wait_1"); return 1; }),
                new DurableBranch<int>("mid2", async (ctx, _) => { await ctx.WaitAsync(TimeSpan.FromSeconds(6), name: "wait_2"); return 2; }),
            },
            name: "race",
            config: new ParallelConfig { CompletionConfig = CompletionConfig.FirstSuccessful() });

        var winner = batch.Succeeded.FirstOrDefault();
        return new BatchResult
        {
            Status = "completed",
            WinnerIndex = winner?.Index ?? -1,
            WinnerName = winner?.Name,
            CompletionReason = batch.CompletionReason.ToString(),
            SuccessCount = batch.SuccessCount,
            StartedCount = batch.StartedCount,
        };
    }

    /// <summary>
    /// Two branches throw with tolerance=1, so the parallel resolves with
    /// <see cref="CompletionReason.FailureToleranceExceeded"/>. The operation does
    /// NOT throw (JS parity) — the workflow inspects the result and reports it.
    /// </summary>
    public static async Task<BatchResult> FailureToleranceAsync(BatchRequest input, IDurableContext context)
    {
        var batch = await context.ParallelAsync(
            new[]
            {
                new DurableBranch<string>("ok1", async (_, _) => { await Task.CompletedTask; return "1"; }),
                new DurableBranch<string>("bad1", async (_, _) => { await Task.CompletedTask; throw new InvalidOperationException("bad1 boom"); }),
                new DurableBranch<string>("ok2", async (_, _) => { await Task.CompletedTask; return "2"; }),
                new DurableBranch<string>("bad2", async (_, _) => { await Task.CompletedTask; throw new InvalidOperationException("bad2 boom"); }),
                new DurableBranch<string>("ok3", async (_, _) => { await Task.CompletedTask; return "3"; }),
            },
            name: "tolerance",
            config: new ParallelConfig { CompletionConfig = new CompletionConfig { ToleratedFailureCount = 1 } });

        return new BatchResult
        {
            Status = "completed",
            SuccessCount = batch.SuccessCount,
            FailureCount = batch.FailureCount,
            CompletionReason = batch.CompletionReason.ToString(),
        };
    }

    /// <summary>Six branches, MaxConcurrency=2; each waits then timestamps. All must succeed.</summary>
    public static async Task<BatchResult> MaxConcurrencyAsync(BatchRequest input, IDurableContext context)
    {
        var branches = new DurableBranch<long>[6];
        for (var i = 0; i < 6; i++)
        {
            var localIndex = i;
            branches[i] = new DurableBranch<long>(
                $"b{localIndex}",
                async (ctx, _) =>
                {
                    await ctx.WaitAsync(TimeSpan.FromSeconds(2), name: $"wait_{localIndex}");
                    return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                });
        }

        var batch = await context.ParallelAsync(
            branches,
            name: "throttled",
            config: new ParallelConfig { MaxConcurrency = 2, CompletionConfig = CompletionConfig.AllCompleted() });

        return new BatchResult
        {
            Status = "completed",
            SuccessCount = batch.SuccessCount,
            Timestamps = batch.GetResults().ToArray(),
        };
    }
}

/// <summary>
/// Map workflows exercising <see cref="IDurableContext.MapAsync{TItem, TResult}"/>
/// in its various completion modes. Mirrors <see cref="ParallelWorkflows"/> but
/// over a collection of items. Each returns a <see cref="BatchResult"/>.
/// </summary>
public static class MapWorkflows
{
    /// <summary>Maps three orders through a per-item step; results are joined.</summary>
    public static async Task<BatchResult> HappyAsync(BatchRequest input, IDurableContext context)
    {
        var orders = new[] { "order-1", "order-2", "order-3" };
        var batch = await context.MapAsync(
            orders,
            async (ctx, orderId, index, all, _) =>
                await ctx.StepAsync(
                    async (_, _) => { await Task.CompletedTask; return $"{orderId}-{input.OrderId}"; },
                    name: "process"),
            name: "process_all",
            config: new MapConfig<string> { ItemNamer = (item, index) => $"item-{item}" });

        return new BatchResult { Status = "completed", Data = string.Join(",", batch.GetResults()) };
    }

    /// <summary>
    /// Middle item throws. Under the fail-fast default the map resolves with
    /// <see cref="CompletionReason.FailureToleranceExceeded"/>, but with unlimited
    /// concurrency all three items are dispatched before any completes, so the
    /// result still reports two successes and one failure. The map does not throw.
    /// </summary>
    public static async Task<BatchResult> PartialFailureAsync(BatchRequest input, IDurableContext context)
    {
        var items = new[] { "ok1", "boom", "ok2" };
        var batch = await context.MapAsync(
            items,
            async (ctx, item, index, all, _) =>
            {
                await Task.CompletedTask;
                if (item == "boom")
                    throw new InvalidOperationException("intentional partial failure");
                return item;
            },
            name: "partial",
            config: new MapConfig<string> { CompletionConfig = CompletionConfig.AllCompleted() });

        var errorSummary = string.Join("|", batch.GetErrors().Select(e => $"{e.GetType().Name}:{e.Message}"));
        return new BatchResult
        {
            Status = "completed",
            SuccessCount = batch.SuccessCount,
            FailureCount = batch.FailureCount,
            ErrorSummary = errorSummary,
        };
    }

    /// <summary>Items wait staggered durations; FirstSuccessful short-circuits on the fastest.</summary>
    public static async Task<BatchResult> FirstSuccessfulAsync(BatchRequest input, IDurableContext context)
    {
        var waitSeconds = new[] { 8, 1, 5, 6 };
        var batch = await context.MapAsync(
            waitSeconds,
            async (ctx, seconds, index, all, _) =>
            {
                await ctx.WaitAsync(TimeSpan.FromSeconds(seconds), name: $"wait_{index}");
                return index;
            },
            name: "race",
            config: new MapConfig<int> { CompletionConfig = CompletionConfig.FirstSuccessful() });

        var winner = batch.Succeeded.FirstOrDefault();
        return new BatchResult
        {
            Status = "completed",
            WinnerIndex = winner?.Index ?? -1,
            WinnerName = winner?.Name,
            CompletionReason = batch.CompletionReason.ToString(),
            SuccessCount = batch.SuccessCount,
            StartedCount = batch.StartedCount,
        };
    }

    /// <summary>
    /// Two items throw with tolerance=1, so the map resolves with
    /// <see cref="CompletionReason.FailureToleranceExceeded"/>. The operation does
    /// NOT throw (JS parity) — the workflow inspects the result and reports it.
    /// </summary>
    public static async Task<BatchResult> FailureToleranceAsync(BatchRequest input, IDurableContext context)
    {
        var items = new[] { "ok1", "bad1", "ok2", "bad2", "ok3" };
        var batch = await context.MapAsync(
            items,
            async (ctx, item, index, all, _) =>
            {
                await Task.CompletedTask;
                if (item.StartsWith("bad"))
                    throw new InvalidOperationException($"{item} boom");
                return item;
            },
            name: "tolerance",
            config: new MapConfig<string> { CompletionConfig = new CompletionConfig { ToleratedFailureCount = 1 } });

        return new BatchResult
        {
            Status = "completed",
            SuccessCount = batch.SuccessCount,
            FailureCount = batch.FailureCount,
            CompletionReason = batch.CompletionReason.ToString(),
        };
    }

    /// <summary>Six items, MaxConcurrency=2; each waits then timestamps. All must succeed.</summary>
    public static async Task<BatchResult> MaxConcurrencyAsync(BatchRequest input, IDurableContext context)
    {
        var items = new[] { 0, 1, 2, 3, 4, 5 };
        var batch = await context.MapAsync(
            items,
            async (ctx, item, index, all, _) =>
            {
                await ctx.WaitAsync(TimeSpan.FromSeconds(2), name: $"wait_{index}");
                return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            },
            name: "throttled",
            config: new MapConfig<int> { MaxConcurrency = 2, CompletionConfig = CompletionConfig.AllCompleted() });

        return new BatchResult
        {
            Status = "completed",
            SuccessCount = batch.SuccessCount,
            Timestamps = batch.GetResults().ToArray(),
        };
    }
}

/// <summary>
/// Child-context workflows exercising
/// <see cref="IDurableContext.RunInChildContextAsync{T}(System.Func{IDurableContext, System.Threading.CancellationToken, System.Threading.Tasks.Task{T}}, string?, ChildContextConfig?, System.Threading.CancellationToken)"/>
/// as a grouping + error boundary. Reuses <see cref="StepsRequest"/>/<see cref="StepsResult"/>.
/// </summary>
public static class ChildContextWorkflows
{
    /// <summary>A child context running step → wait → step; its result is checkpointed as one CONTEXT op.</summary>
    public static async Task<StepsResult> HappyAsync(StepsRequest input, IDurableContext context)
    {
        var phaseResult = await context.RunInChildContextAsync<string>(
            async (childCtx, _) =>
            {
                var validated = await childCtx.StepAsync(
                    async (_, _) => { await Task.CompletedTask; return $"validated-{input.OrderId}"; },
                    name: "validate");

                await childCtx.WaitAsync(TimeSpan.FromSeconds(2), name: "short_wait");

                var processed = await childCtx.StepAsync(
                    async (_, _) => { await Task.CompletedTask; return $"processed-{validated}"; },
                    name: "process");

                return processed;
            },
            name: "phase",
            config: new ChildContextConfig { SubType = "OrderProcessing" });

        return new StepsResult { Status = "completed", Data = phaseResult };
    }

    /// <summary>Throws inside a child context, so the workflow terminates Failed.</summary>
    public const string FailureMessage = "intentional child context failure for integration test";

    public static async Task<StepsResult> FailsAsync(StepsRequest input, IDurableContext context)
    {
        await context.RunInChildContextAsync<string>(
            async (childCtx, _) =>
            {
                await childCtx.StepAsync(
                    async (_, _) => { await Task.CompletedTask; return $"prepared-{input.OrderId}"; },
                    name: "prepare");

                throw new InvalidOperationException(FailureMessage);
            },
            name: "phase",
            config: new ChildContextConfig { SubType = "OrderProcessing" });

        return new StepsResult { Status = "should_not_reach" };
    }

    /// <summary>A retry-then-exhaust step inside a child context; the child closes Failed.</summary>
    public const int MaxAttempts = 3;

    public static async Task<StepsResult> RetryFailsAsync(StepsRequest input, IDurableContext context)
    {
        await context.RunInChildContextAsync<string>(
            async (childCtx, _) =>
            {
                return await childCtx.StepAsync<string>(
                    async (ctx, _) =>
                    {
                        await Task.CompletedTask;
                        throw new InvalidOperationException(
                            $"always-fails on attempt {ctx.AttemptNumber} for {input.OrderId}");
                    },
                    name: "always_fails",
                    config: new StepConfig
                    {
                        RetryStrategy = RetryStrategy.Exponential(
                            maxAttempts: MaxAttempts,
                            initialDelay: TimeSpan.FromSeconds(2),
                            maxDelay: TimeSpan.FromSeconds(10),
                            backoffRate: 2.0,
                            jitter: JitterStrategy.None)
                    });
            },
            name: "phase",
            config: new ChildContextConfig { SubType = "OrderProcessing" });

        return new StepsResult { Status = "should_not_reach" };
    }
}

/// <summary>
/// A workflow whose chained <see cref="IDurableContext.InvokeAsync{TPayload, TResult}"/>
/// targets a downstream that throws. The parent catches the surfaced
/// <see cref="InvokeException"/> and converts it into a normal result, so the
/// workflow itself succeeds. Exercises chained-invoke failure handling.
/// </summary>
public static class InvokeFailureWorkflow
{
    /// <summary>Short name the local runner registers the failing downstream under.</summary>
    public const string DownstreamFunctionName = "invoke-failure-downstream";

    public static async Task<ChainedInvokeResult> RunAsync(ChainedInvokeRequest input, IDurableContext context)
    {
        var target = System.Environment.GetEnvironmentVariable("DOWNSTREAM_FUNCTION_ARN")
            ?? DownstreamFunctionName;

        try
        {
            await context.InvokeAsync<ChildRequest, ChildResult>(
                target,
                new ChildRequest { Sku = input.Sku, Quantity = input.Quantity },
                name: "call_failing_child");

            return new ChainedInvokeResult { Status = "unexpected_success" };
        }
        catch (InvokeException ex)
        {
            return new ChainedInvokeResult
            {
                Status = "completed",
                Sku = input.Sku,
                Warehouse = $"parent-saw-{ex.ErrorType ?? "unknown"}",
            };
        }
    }

    /// <summary>
    /// The downstream durable workflow. Throws inside a step so it records a
    /// step-failed event and the execution fails cleanly; the parent's
    /// InvokeAsync then surfaces the failure as an InvokeException.
    /// </summary>
    public static async Task<ChildResult> DownstreamAsync(ChildRequest input, IDurableContext context)
    {
        await context.StepAsync<string>(
            async (_, _) =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException("intentional child failure");
            },
            name: "fail_step");

        return new ChildResult();
    }
}

/// <summary>
/// A <see cref="IDurableContext.WaitForCallbackAsync{T}"/> whose submitter throws
/// on every attempt with no retries, so the operation fails terminally with
/// <see cref="CallbackSubmitterException"/> and the (uncaught) workflow fails.
/// No external system or callback delivery is involved, so it runs identically on
/// both backends.
/// </summary>
public static class CallbackSubmitterFailsWorkflow
{
    public const string SubmitterMessage = "submitter intentional failure";

    public static async Task<ApprovalResult> RunAsync(ApprovalRequest input, IDurableContext context)
    {
        var decision = await context.WaitForCallbackAsync<ApprovalDecision>(
            submitter: async (callbackId, cbCtx, _) =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException(SubmitterMessage);
            },
            name: "approve",
            config: new WaitForCallbackConfig { RetryStrategy = RetryStrategy.None });

        return new ApprovalResult { Status = "decided", Approved = decision.Approved };
    }
}

// ---- DTOs (shared by both backends; serialized with DefaultLambdaJsonSerializer) ----

public sealed class OrderRequest
{
    public string? OrderId { get; set; }
    public string[]? Items { get; set; }
}

public sealed class FulfillmentResult
{
    public string? Status { get; set; }
    public string? OrderId { get; set; }
    public int ItemCount { get; set; }
    public int AvailableStock { get; set; }
    public decimal OrderTotal { get; set; }
    public int SettlementPolls { get; set; }
    public string? TrackingId { get; set; }
}

public sealed record SettlementState(int Polls);

public sealed class ApprovalRequest
{
    public string? RequestId { get; set; }
}

public sealed class ApprovalDecision
{
    public bool Approved { get; set; }
    public string? Approver { get; set; }
}

public sealed class ApprovalResult
{
    public string? Status { get; set; }
    public string? Ticket { get; set; }
    public bool Approved { get; set; }
    public string? Approver { get; set; }
}

public sealed class ChainedInvokeRequest
{
    public string? Sku { get; set; }
    public int Quantity { get; set; }
}

public sealed class ChainedInvokeResult
{
    public string? Status { get; set; }
    public string? Sku { get; set; }
    public decimal LineTotal { get; set; }
    public string? Warehouse { get; set; }
}

public sealed class ChildRequest
{
    public string? Sku { get; set; }
    public int Quantity { get; set; }
}

public sealed class ChildResult
{
    public decimal LineTotal { get; set; }
    public string? Warehouse { get; set; }
}

public sealed class RetryRequest
{
    public string? OrderId { get; set; }
}

public sealed class RetryResult
{
    public string? Status { get; set; }
    public string? Data { get; set; }
}

public sealed class WaitRequest
{
    public string? OrderId { get; set; }
}

public sealed class WaitResult
{
    public string? Status { get; set; }
    public string? Data { get; set; }
}

public sealed class StepsRequest
{
    public string? OrderId { get; set; }
}

public sealed class StepsResult
{
    public string? Status { get; set; }
    public string? Data { get; set; }
}

public sealed class ConditionRequest
{
    public string? OrderId { get; set; }
}

/// <summary>
/// Unified result for the WaitForCondition workflows. Each scenario populates the
/// subset of fields it cares about; unused fields stay at their defaults.
/// </summary>
public sealed class ConditionResult
{
    public string? Status { get; set; }
    public int Counter { get; set; }
    public int AttemptsTaken { get; set; }
    public bool Done { get; set; }
    public int AttemptsExhausted { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>Per-poll state for the happy-path condition workflow.</summary>
public sealed record ConditionCounter(int Counter, int AttemptNumber);

/// <summary>Per-poll state for the exponential condition workflow.</summary>
public sealed record ConditionDone(bool Done, int AttemptNumber);

public sealed class BatchRequest
{
    public string? OrderId { get; set; }
}

/// <summary>
/// Unified result for the Parallel and Map workflows. Each scenario populates the
/// subset of fields it needs; unused fields stay at their defaults.
/// </summary>
public sealed class BatchResult
{
    public string? Status { get; set; }
    public string? Data { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string? ErrorSummary { get; set; }
    public int WinnerIndex { get; set; }
    public string? WinnerName { get; set; }
    public string? CompletionReason { get; set; }
    public int StartedCount { get; set; }
    public long[]? Timestamps { get; set; }
}
