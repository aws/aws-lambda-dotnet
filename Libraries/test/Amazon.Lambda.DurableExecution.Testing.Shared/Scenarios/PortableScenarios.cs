// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Testing;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Shared;

/// <summary>
/// Backend-agnostic end-to-end scenarios written once against
/// <see cref="IDurableTestRunner{TInput, TOutput}"/> and run unchanged on both
/// the local <c>DurableTestRunner</c> and the cloud <c>CloudDurableTestRunner</c>.
/// Each method here exercises one or more <see cref="IDurableTestRunner{TInput, TOutput}"/>
/// APIs; together they cover the full interface surface:
/// <list type="bullet">
/// <item><see cref="RunFulfillmentAsync"/> — <c>RunAsync</c> + step inspection across every operation kind.</item>
/// <item><see cref="ApproveAsync"/> — <c>StartAsync</c> / <c>WaitForCallbackAsync</c> / <c>SendCallbackHeartbeatAsync</c> / <c>SendCallbackSuccessAsync</c> / <c>WaitForResultAsync</c>.</item>
/// <item><see cref="RejectAsync"/> — <c>SendCallbackFailureAsync</c> failure delivery.</item>
/// <item><see cref="ChainedInvokeAsync"/> — durable-to-durable <c>InvokeAsync</c> through the runner.</item>
/// </list>
/// The methods take an already-constructed runner so each backend can wire its
/// own construction (in-process delegate vs deployed ARN) and sibling resolution
/// (registry vs deployed downstream).
/// </summary>
public static class PortableScenarios
{
    /// <summary>
    /// Drives the order-fulfillment workflow to completion via
    /// <see cref="IDurableTestRunner{TInput, TOutput}.RunAsync"/> and asserts on
    /// the terminal result plus the recorded operations for every operation kind
    /// the workflow uses (step, parallel, map, wait, wait-for-condition, child
    /// context). Assertions avoid <c>InvocationCount</c> so they hold on both
    /// backends.
    /// </summary>
    public static async Task RunFulfillmentAsync(
        IDurableTestRunner<OrderRequest, FulfillmentResult> runner)
    {
        var result = await runner.RunAsync(new OrderRequest
        {
            OrderId = "order-42",
            Items = new[] { "widget", "gadget", "gizmo" },
        });

        result.EnsureSucceeded();
        Assert.Equal(InvocationStatus.Succeeded, result.Status);

        var fulfillment = result.Result!;
        Assert.Equal("fulfilled", fulfillment.Status);
        Assert.Equal("order-42", fulfillment.OrderId);
        Assert.Equal(3, fulfillment.ItemCount);
        Assert.Equal(150, fulfillment.AvailableStock);    // 100 (warehouse) + 50 (supplier)
        Assert.Equal(60m, fulfillment.OrderTotal);        // 10 + 20 + 30
        Assert.True(fulfillment.SettlementPolls >= 2);
        Assert.Equal("TRK-order-42", fulfillment.TrackingId);

        // The validation step is recorded with its typed result.
        var validate = result.GetStep("validate_order");
        Assert.Equal(OperationKind.Step, validate.Kind);
        Assert.Equal(OperationStatus.Succeeded, validate.Status);
        Assert.Equal(3, validate.GetResult<int>());

        // The parallel fan-out is recorded; both branches succeeded.
        var inventory = result.GetStep("check_inventory");
        Assert.Equal(OperationStatus.Succeeded, inventory.Status);

        // The map produced one operation per line item.
        Assert.Equal(OperationKind.Wait, result.GetStep("settlement_delay").Kind);

        // The shipping child context wraps the pack + label steps.
        var ship = result.GetStep("ship_order");
        Assert.Equal(OperationKind.Context, ship.Kind);

        // No operation ended in a failed state.
        Assert.Empty(result.GetStepsByStatus(OperationStatus.Failed));
    }

    /// <summary>
    /// Exercises the full callback round-trip through the two-call pattern:
    /// <c>StartAsync</c> launches and returns the ARN, <c>WaitForCallbackAsync</c>
    /// blocks until the workflow suspends and yields the callback id, a heartbeat
    /// keeps it alive, then <c>SendCallbackSuccessAsync</c> delivers the decision
    /// and <c>WaitForResultAsync</c> drives the workflow to completion.
    /// </summary>
    public static async Task ApproveAsync(
        IDurableTestRunner<ApprovalRequest, ApprovalResult> runner)
    {
        var arn = await runner.StartAsync(new ApprovalRequest { RequestId = "req-7" });
        Assert.False(string.IsNullOrEmpty(arn));

        var callbackId = await runner.WaitForCallbackAsync(arn, name: "approval");
        Assert.False(string.IsNullOrEmpty(callbackId));

        // Heartbeat must not throw or resolve the callback (no-op locally).
        await runner.SendCallbackHeartbeatAsync(callbackId);

        await runner.SendCallbackSuccessAsync(callbackId, new ApprovalDecision
        {
            Approved = true,
            Approver = "manager-1",
        });

        var result = await runner.WaitForResultAsync(arn);
        result.EnsureSucceeded();

        Assert.Equal("decided", result.Result!.Status);
        Assert.True(result.Result.Approved);
        Assert.Equal("manager-1", result.Result.Approver);
        Assert.Equal("ticket-req-7", result.Result.Ticket);
    }

    /// <summary>
    /// Delivers a callback failure via <c>SendCallbackFailureAsync</c>; the
    /// workflow catches the resulting <c>CallbackException</c> and returns a
    /// rejected result, so the execution itself still completes successfully.
    /// </summary>
    public static async Task RejectAsync(
        IDurableTestRunner<ApprovalRequest, ApprovalResult> runner)
    {
        var arn = await runner.StartAsync(new ApprovalRequest { RequestId = "req-9" });
        var callbackId = await runner.WaitForCallbackAsync(arn, name: "approval");

        await runner.SendCallbackFailureAsync(callbackId, new ErrorObject
        {
            ErrorType = "PolicyViolation",
            ErrorMessage = "amount exceeds limit",
        });

        var result = await runner.WaitForResultAsync(arn);
        result.EnsureSucceeded();

        Assert.Equal("rejected", result.Result!.Status);
        Assert.False(result.Result.Approved);
        Assert.Equal("ticket-req-9", result.Result.Ticket);
    }

    /// <summary>
    /// Drives the chained-invoke workflow, which durably invokes a downstream
    /// durable function via <c>InvokeAsync</c>. The caller wires the downstream
    /// (sibling registration locally, deployed function in the cloud) before
    /// invoking this scenario.
    /// </summary>
    public static async Task ChainedInvokeAsync(
        IDurableTestRunner<ChainedInvokeRequest, ChainedInvokeResult> runner)
    {
        var result = await runner.RunAsync(new ChainedInvokeRequest
        {
            Sku = "widget",
            Quantity = 4,
        });

        result.EnsureSucceeded();
        Assert.Equal("enriched", result.Result!.Status);
        Assert.Equal("widget", result.Result.Sku);
        Assert.Equal(100m, result.Result.LineTotal);   // 4 * 25
        Assert.Equal("wh-widget", result.Result.Warehouse);

        // The chained invoke is recorded as a step on the parent.
        var invoke = result.GetStep("enrich_order");
        Assert.Equal(OperationKind.ChainedInvoke, invoke.Kind);
        Assert.Equal(OperationStatus.Succeeded, invoke.Status);
    }

    /// <summary>
    /// Behavioral half of the retry scenario: a flaky step fails twice and
    /// succeeds on the third attempt. Asserts only what both backends can observe
    /// through <see cref="IDurableTestRunner{TInput, TOutput}"/> — the terminal
    /// result and the step's final attempt count. The service-only concerns
    /// (per-attempt failure events and the real inter-attempt retry delay) stay
    /// in the cloud-only integration test.
    /// </summary>
    public static async Task RetryAsync(
        IDurableTestRunner<RetryRequest, RetryResult> runner)
    {
        var result = await runner.RunAsync(new RetryRequest { OrderId = "retry-1" });

        result.EnsureSucceeded();
        Assert.Equal("completed", result.Result!.Status);
        Assert.Equal($"ok on attempt {RetryWorkflow.MaxAttempts}", result.Result.Data);

        // The step's recorded attempt count reflects the successful (final) attempt.
        var step = result.GetStep(RetryWorkflow.StepName);
        Assert.Equal(OperationKind.Step, step.Kind);
        Assert.Equal(OperationStatus.Succeeded, step.Status);
        Assert.Equal(RetryWorkflow.MaxAttempts, step.Attempt);
    }

    /// <summary>
    /// Behavioral half of the wait-only scenario: a workflow whose entire body is
    /// a single wait. Asserts the terminal result and that the wait operation was
    /// recorded. The service-only proof that the wait actually suspended and
    /// resumed (InvocationCompleted count, real duration) stays in the cloud test.
    /// </summary>
    public static async Task WaitOnlyAsync(
        IDurableTestRunner<WaitRequest, WaitResult> runner)
    {
        var result = await runner.RunAsync(new WaitRequest { OrderId = "wait-1" });

        result.EnsureSucceeded();
        Assert.Equal("completed", result.Result!.Status);
        Assert.Equal("wait_only", result.Result.Data);

        // The wait is recorded as a Wait operation; no step operations exist.
        var wait = result.GetStep(WaitOnlyWorkflow.WaitName);
        Assert.Equal(OperationKind.Wait, wait.Kind);
        Assert.Empty(result.Steps.Where(s => s.Kind == OperationKind.Step));
    }

    /// <summary>
    /// Behavioral half of the multi-step scenario: five sequential steps chain
    /// their outputs. Asserts the terminal result and each step's recorded result
    /// in order. The cloud-only test additionally verifies the exact StepStarted
    /// event count (no replay-induced duplicate executions).
    /// </summary>
    public static async Task MultipleStepsAsync(
        IDurableTestRunner<StepsRequest, StepsResult> runner)
    {
        var result = await runner.RunAsync(new StepsRequest { OrderId = "chain" });

        result.EnsureSucceeded();
        Assert.Equal("completed", result.Result!.Status);
        Assert.Equal("a-chain-b-c-d-e", result.Result.Data);

        // Each step is recorded with its chained result, in declaration order.
        Assert.Equal("a-chain",         result.GetStep("step_1").GetResult<string>());
        Assert.Equal("a-chain-b",       result.GetStep("step_2").GetResult<string>());
        Assert.Equal("a-chain-b-c",     result.GetStep("step_3").GetResult<string>());
        Assert.Equal("a-chain-b-c-d",   result.GetStep("step_4").GetResult<string>());
        Assert.Equal("a-chain-b-c-d-e", result.GetStep("step_5").GetResult<string>());

        // All five recorded operations are successful steps.
        Assert.Equal(5, result.Steps.Count(s => s.Kind == OperationKind.Step));
        Assert.Empty(result.GetStepsByStatus(OperationStatus.Failed));
    }

    /// <summary>
    /// Behavioral half of the step-failure scenario: a step throws and the
    /// workflow terminates Failed. Asserts the terminal status, the surfaced
    /// error type/message, and that the failing step is recorded as Failed. The
    /// cloud-only test additionally verifies the StepFailed history event detail.
    /// </summary>
    public static async Task StepFailsAsync(
        IDurableTestRunner<StepsRequest, StepsResult> runner)
    {
        var result = await runner.RunAsync(new StepsRequest { OrderId = "x" });

        Assert.True(result.IsFailed);
        Assert.Equal(InvocationStatus.Failed, result.Status);
        Assert.NotNull(result.Error);
        Assert.Equal("System.InvalidOperationException", result.Error!.ErrorType);
        Assert.Contains(StepFailsWorkflow.FailureMessage, result.Error.ErrorMessage);

        // The failing step is recorded as Failed; no step ever succeeded.
        var step = result.GetStep("fail_step");
        Assert.Equal(OperationStatus.Failed, step.Status);
        Assert.Empty(result.GetStepsByStatus(OperationStatus.Succeeded));
    }

    /// <summary>
    /// Behavioral half of the step-wait-step scenario: a value threads from the
    /// pre-wait step through the suspension to the post-wait step. Asserts both
    /// step results, the final output, and that the wait is recorded. The
    /// cloud-only test additionally verifies the real wait duration.
    /// </summary>
    public static async Task StepWaitStepAsync(
        IDurableTestRunner<StepsRequest, StepsResult> runner)
    {
        var result = await runner.RunAsync(new StepsRequest { OrderId = "integ-test-123" });

        result.EnsureSucceeded();
        Assert.Equal("completed", result.Result!.Status);
        Assert.Equal("processed-validated-integ-test-123", result.Result.Data);

        Assert.Equal("validated-integ-test-123", result.GetStep("validate").GetResult<string>());
        Assert.Equal("processed-validated-integ-test-123", result.GetStep("process").GetResult<string>());

        // The middle wait is recorded as a Wait operation.
        Assert.Equal(OperationKind.Wait, result.GetStep(StepWaitStepWorkflow.WaitName).Kind);
    }

    /// <summary>
    /// Behavioral half of the longer-wait scenario (step → 15s wait → step).
    /// Same observable contract as step-wait-step; the cloud-only test asserts
    /// the real 15s timer duration.
    /// </summary>
    public static async Task LongerWaitAsync(
        IDurableTestRunner<StepsRequest, StepsResult> runner)
    {
        var result = await runner.RunAsync(new StepsRequest { OrderId = "long-wait-test" });

        result.EnsureSucceeded();
        Assert.Equal("completed", result.Result!.Status);
        Assert.Equal("after_wait-started-long-wait-test", result.Result.Data);

        Assert.Equal("started-long-wait-test", result.GetStep("before_wait").GetResult<string>());
        Assert.Equal("after_wait-started-long-wait-test", result.GetStep("after_wait").GetResult<string>());
        Assert.Equal(OperationKind.Wait, result.GetStep(LongerWaitWorkflow.WaitName).Kind);
    }

    /// <summary>
    /// Behavioral half of the retry-exhaustion scenario: a step that always
    /// throws exhausts its retries and the workflow terminates Failed with the
    /// last attempt's exception. The cloud-only test additionally verifies the
    /// per-attempt StepFailed events and inter-attempt timing.
    /// </summary>
    public static async Task RetryExhaustionAsync(
        IDurableTestRunner<StepsRequest, StepsResult> runner)
    {
        var result = await runner.RunAsync(new StepsRequest { OrderId = "x" });

        Assert.True(result.IsFailed);
        Assert.NotNull(result.Error);
        Assert.Equal("System.InvalidOperationException", result.Error!.ErrorType);
        Assert.Contains($"attempt {RetryExhaustionWorkflow.MaxAttempts}", result.Error.ErrorMessage);

        // The step is recorded as Failed with the final attempt count.
        var step = result.GetStep(RetryExhaustionWorkflow.StepName);
        Assert.Equal(OperationStatus.Failed, step.Status);
        Assert.Equal(RetryExhaustionWorkflow.MaxAttempts, step.Attempt);
    }

    /// <summary>
    /// Behavioral half of the WaitForCondition happy path: the poll completes once
    /// its counter reaches 3. Asserts the terminal counter/attempt count. The
    /// cloud-only test additionally verifies the real inter-poll delays.
    /// </summary>
    public static async Task WaitForConditionHappyAsync(
        IDurableTestRunner<ConditionRequest, ConditionResult> runner)
    {
        var result = await runner.RunAsync(new ConditionRequest { OrderId = "cond-1" });

        result.EnsureSucceeded();
        Assert.Equal("completed", result.Result!.Status);
        Assert.Equal(3, result.Result.Counter);
        Assert.Equal(3, result.Result.AttemptsTaken);
    }

    /// <summary>
    /// Behavioral half of the WaitForCondition max-attempts scenario: the condition
    /// never satisfies, the strategy exhausts its attempts, and the workflow catches
    /// the resulting exception. The execution itself succeeds.
    /// </summary>
    public static async Task WaitForConditionMaxAttemptsAsync(
        IDurableTestRunner<ConditionRequest, ConditionResult> runner)
    {
        var result = await runner.RunAsync(new ConditionRequest { OrderId = "cond-2" });

        result.EnsureSucceeded();
        Assert.Equal("exhausted", result.Result!.Status);
        Assert.Equal(WaitForConditionMaxAttemptsWorkflow.MaxAttempts, result.Result.AttemptsExhausted);
    }

    /// <summary>
    /// Behavioral half of the WaitForCondition exponential scenario: the poll
    /// completes on attempt 3 under an exponential backoff strategy.
    /// </summary>
    public static async Task WaitForConditionExponentialAsync(
        IDurableTestRunner<ConditionRequest, ConditionResult> runner)
    {
        var result = await runner.RunAsync(new ConditionRequest { OrderId = "cond-3" });

        result.EnsureSucceeded();
        Assert.Equal("completed", result.Result!.Status);
        Assert.True(result.Result.Done);
        Assert.Equal(3, result.Result.AttemptsTaken);
    }

    /// <summary>
    /// Behavioral half of the WaitForCondition user-check-throws scenario: the check
    /// throws on attempt 2, surfaced as a StepException whose ErrorType is the
    /// original exception type. The workflow catches it and reports the captured error.
    /// </summary>
    public static async Task WaitForConditionUserCheckThrowsAsync(
        IDurableTestRunner<ConditionRequest, ConditionResult> runner)
    {
        var result = await runner.RunAsync(new ConditionRequest { OrderId = "cond-4" });

        result.EnsureSucceeded();
        Assert.Equal("caught_step_exception", result.Result!.Status);
        Assert.Equal("System.InvalidOperationException", result.Result.ErrorType);
        Assert.Contains("intentional check failure", result.Result.ErrorMessage);
    }

    // ---- Parallel family ----

    /// <summary>Behavioral half of parallel happy path: all branches succeed and join.</summary>
    public static async Task ParallelHappyAsync(IDurableTestRunner<BatchRequest, BatchResult> runner)
    {
        var result = await runner.RunAsync(new BatchRequest { OrderId = "p1" });

        result.EnsureSucceeded();
        Assert.Equal("completed", result.Result!.Status);
        Assert.Contains("alpha-p1", result.Result.Data);
        Assert.Contains("beta-p1", result.Result.Data);
        Assert.Contains("gamma-p1", result.Result.Data);
    }

    /// <summary>Behavioral half of parallel partial failure: AllCompleted tolerates one failure.</summary>
    public static async Task ParallelPartialFailureAsync(IDurableTestRunner<BatchRequest, BatchResult> runner)
    {
        var result = await runner.RunAsync(new BatchRequest { OrderId = "pf" });

        result.EnsureSucceeded();
        Assert.Equal(2, result.Result!.SuccessCount);
        Assert.Equal(1, result.Result.FailureCount);
        Assert.Contains("intentional partial failure", result.Result.ErrorSummary);
    }

    /// <summary>
    /// Behavioral half of parallel first-successful: a winning branch short-circuits.
    /// The winner's identity is timing-dependent (and the local runner skips waits),
    /// so we assert only that a valid winner exists and the completion was not a
    /// failure-tolerance breach — not which specific branch won.
    /// </summary>
    public static async Task ParallelFirstSuccessfulAsync(IDurableTestRunner<BatchRequest, BatchResult> runner)
    {
        var result = await runner.RunAsync(new BatchRequest { OrderId = "race" });

        result.EnsureSucceeded();
        Assert.True(result.Result!.SuccessCount >= 1);
        Assert.InRange(result.Result.WinnerIndex, 0, 3);
        Assert.NotNull(result.Result.WinnerName);
        Assert.NotEqual("FailureToleranceExceeded", result.Result.CompletionReason);
    }

    /// <summary>
    /// Behavioral half of parallel failure tolerance: exceeding tolerance resolves
    /// the batch with <c>FailureToleranceExceeded</c>. The operation does NOT throw
    /// (JS parity) — the workflow completes and reports the completion reason.
    /// </summary>
    public static async Task ParallelFailureToleranceAsync(IDurableTestRunner<BatchRequest, BatchResult> runner)
    {
        var result = await runner.RunAsync(new BatchRequest { OrderId = "tol" });

        result.EnsureSucceeded();
        Assert.Equal("FailureToleranceExceeded", result.Result!.CompletionReason);
        Assert.Equal(2, result.Result.FailureCount);
    }

    /// <summary>
    /// Behavioral half of parallel max-concurrency: all six branches complete.
    /// The wave timing is service-only (the local runner skips waits), so it stays
    /// in the cloud test; here we assert every branch succeeded.
    /// </summary>
    public static async Task ParallelMaxConcurrencyAsync(IDurableTestRunner<BatchRequest, BatchResult> runner)
    {
        var result = await runner.RunAsync(new BatchRequest { OrderId = "mc" });

        result.EnsureSucceeded();
        Assert.Equal(6, result.Result!.SuccessCount);
        Assert.Equal(6, result.Result.Timestamps!.Length);
    }

    // ---- Map family ----

    /// <summary>Behavioral half of map happy path: all items map and join.</summary>
    public static async Task MapHappyAsync(IDurableTestRunner<BatchRequest, BatchResult> runner)
    {
        var result = await runner.RunAsync(new BatchRequest { OrderId = "m1" });

        result.EnsureSucceeded();
        Assert.Equal("completed", result.Result!.Status);
        Assert.Contains("order-1-m1", result.Result.Data);
        Assert.Contains("order-2-m1", result.Result.Data);
        Assert.Contains("order-3-m1", result.Result.Data);
    }

    /// <summary>Behavioral half of map partial failure: default AllCompleted tolerates one failure.</summary>
    public static async Task MapPartialFailureAsync(IDurableTestRunner<BatchRequest, BatchResult> runner)
    {
        var result = await runner.RunAsync(new BatchRequest { OrderId = "mf" });

        result.EnsureSucceeded();
        Assert.Equal(2, result.Result!.SuccessCount);
        Assert.Equal(1, result.Result.FailureCount);
        Assert.Contains("intentional partial failure", result.Result.ErrorSummary);
    }

    /// <summary>Behavioral half of map first-successful: a winning item short-circuits (winner identity is timing-dependent).</summary>
    public static async Task MapFirstSuccessfulAsync(IDurableTestRunner<BatchRequest, BatchResult> runner)
    {
        var result = await runner.RunAsync(new BatchRequest { OrderId = "race" });

        result.EnsureSucceeded();
        Assert.True(result.Result!.SuccessCount >= 1);
        Assert.InRange(result.Result.WinnerIndex, 0, 3);
        Assert.NotNull(result.Result.WinnerName);
        Assert.NotEqual("FailureToleranceExceeded", result.Result.CompletionReason);
    }

    /// <summary>
    /// Behavioral half of map failure tolerance: exceeding tolerance resolves the
    /// batch with <c>FailureToleranceExceeded</c>. The operation does NOT throw
    /// (JS parity) — the workflow completes and reports the completion reason.
    /// </summary>
    public static async Task MapFailureToleranceAsync(IDurableTestRunner<BatchRequest, BatchResult> runner)
    {
        var result = await runner.RunAsync(new BatchRequest { OrderId = "tol" });

        result.EnsureSucceeded();
        Assert.Equal("FailureToleranceExceeded", result.Result!.CompletionReason);
        Assert.Equal(2, result.Result.FailureCount);
    }

    /// <summary>Behavioral half of map max-concurrency: all six items complete (wave timing is cloud-only).</summary>
    public static async Task MapMaxConcurrencyAsync(IDurableTestRunner<BatchRequest, BatchResult> runner)
    {
        var result = await runner.RunAsync(new BatchRequest { OrderId = "mc" });

        result.EnsureSucceeded();
        Assert.Equal(6, result.Result!.SuccessCount);
        Assert.Equal(6, result.Result.Timestamps!.Length);
    }

    // ---- ChildContext family ----

    /// <summary>
    /// Behavioral half of the child-context happy path: a child context running
    /// step → wait → step returns its result, checkpointed as one CONTEXT op.
    /// Asserts the terminal result and that the context op is recorded. The
    /// cloud-only test additionally verifies the inner ops re-parent to the context.
    /// </summary>
    public static async Task ChildContextHappyAsync(IDurableTestRunner<StepsRequest, StepsResult> runner)
    {
        var result = await runner.RunAsync(new StepsRequest { OrderId = "integ-test-456" });

        result.EnsureSucceeded();
        Assert.Equal("completed", result.Result!.Status);
        Assert.Equal("processed-validated-integ-test-456", result.Result.Data);

        // The phase is recorded as a Context operation.
        Assert.Equal(OperationKind.Context, result.GetStep("phase").Kind);
    }

    /// <summary>Behavioral half of child-context failure: a throw inside the child fails the workflow.</summary>
    public static async Task ChildContextFailsAsync(IDurableTestRunner<StepsRequest, StepsResult> runner)
    {
        var result = await runner.RunAsync(new StepsRequest { OrderId = "integ-test-fail" });

        Assert.True(result.IsFailed);
        Assert.NotNull(result.Error);
        Assert.Equal(typeof(System.InvalidOperationException).FullName, result.Error!.ErrorType);
        Assert.Contains("intentional child context failure", result.Error.ErrorMessage);
    }

    /// <summary>Behavioral half of child-context retry-exhaustion: retries exhaust inside the child and fail the workflow.</summary>
    public static async Task ChildContextRetryFailsAsync(IDurableTestRunner<StepsRequest, StepsResult> runner)
    {
        var result = await runner.RunAsync(new StepsRequest { OrderId = "integ-test-retryfail" });

        Assert.True(result.IsFailed);
        Assert.NotNull(result.Error);
        Assert.Contains("always-fails", result.Error!.ErrorMessage);
    }

    // ---- Chained-invoke / callback failure variants ----

    /// <summary>
    /// Behavioral half of the chained-invoke failure scenario: the downstream
    /// throws, the parent catches the surfaced InvokeException and returns
    /// normally, so the parent execution succeeds. Asserts the parent saw the
    /// underlying error type. The caller wires the failing downstream.
    /// </summary>
    public static async Task InvokeFailureAsync(IDurableTestRunner<ChainedInvokeRequest, ChainedInvokeResult> runner)
    {
        var result = await runner.RunAsync(new ChainedInvokeRequest { Sku = "widget", Quantity = 1 });

        result.EnsureSucceeded();
        Assert.Equal("completed", result.Result!.Status);
        Assert.Contains("InvalidOperationException", result.Result.Warehouse);

        // The chained invoke is recorded as a failed step on the parent.
        var invoke = result.GetStep("call_failing_child");
        Assert.Equal(OperationKind.ChainedInvoke, invoke.Kind);
        Assert.Equal(OperationStatus.Failed, invoke.Status);
    }

    /// <summary>
    /// Behavioral half of the WaitForCallback submitter-fails scenario: the
    /// submitter throws with no retries, so the operation fails terminally and
    /// the (uncaught) workflow fails. No external system is involved, so it runs
    /// on both backends.
    /// <para>
    /// The exact terminal error <em>type</em> is NOT asserted here because it
    /// diverges by backend: the local runner surfaces the underlying
    /// <c>InvalidOperationException</c>, while the service wraps it as
    /// <c>CallbackSubmitterException</c>. The wrapper-type assertion stays in the
    /// cloud-only <c>WaitForCallbackSubmitterFailsTest</c>.
    /// </para>
    /// </summary>
    public static async Task CallbackSubmitterFailsAsync(IDurableTestRunner<ApprovalRequest, ApprovalResult> runner)
    {
        var result = await runner.RunAsync(new ApprovalRequest { RequestId = "req-sub" });

        Assert.True(result.IsFailed);
        Assert.NotNull(result.Error);
    }
}
