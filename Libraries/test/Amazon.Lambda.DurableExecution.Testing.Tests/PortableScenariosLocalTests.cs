// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Testing.Shared;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

/// <summary>
/// Runs the shared <see cref="PortableScenarios"/> against the in-process
/// <see cref="DurableTestRunner{TInput, TOutput}"/>. The identical scenarios run
/// against the deployed cloud backend in
/// <c>Amazon.Lambda.DurableExecution.IntegrationTests</c> — these are the fast,
/// no-AWS half of the portable-test pair, and prove the package's promise that a
/// test written against <see cref="IDurableTestRunner{TInput, TOutput}"/> runs
/// unchanged on either backend.
/// </summary>
public class PortableScenariosLocalTests
{
    [Fact]
    public async Task Fulfillment_AllOperationKinds_Succeeds()
    {
        await using var runner = new DurableTestRunner<OrderRequest, FulfillmentResult>(
            FulfillmentWorkflow.RunAsync);

        await PortableScenarios.RunFulfillmentAsync(runner);
    }

    [Fact]
    public async Task Approval_CallbackSuccessRoundTrip_Succeeds()
    {
        await using var runner = new DurableTestRunner<ApprovalRequest, ApprovalResult>(
            ApprovalWorkflow.RunAsync);

        await PortableScenarios.ApproveAsync(runner);
    }

    [Fact]
    public async Task Approval_CallbackFailure_RejectsButCompletes()
    {
        await using var runner = new DurableTestRunner<ApprovalRequest, ApprovalResult>(
            ApprovalWorkflow.RunAsync);

        await PortableScenarios.RejectAsync(runner);
    }

    [Fact]
    public async Task ChainedInvoke_DurableSibling_Succeeds()
    {
        await using var runner = new DurableTestRunner<ChainedInvokeRequest, ChainedInvokeResult>(
            ChainedInvokeWorkflow.RunAsync);

        // The cloud backend deploys the downstream as a separate function; locally
        // we register it as an in-process durable sibling under the same short name.
        runner.RegisterDurableFunction<ChildRequest, ChildResult>(
            ChainedInvokeWorkflow.DownstreamFunctionName,
            ChainedInvokeWorkflow.DownstreamAsync);

        await PortableScenarios.ChainedInvokeAsync(runner);
    }

    [Fact]
    public async Task Retry_FlakyStepSucceedsOnFinalAttempt()
    {
        await using var runner = new DurableTestRunner<RetryRequest, RetryResult>(
            RetryWorkflow.RunAsync);

        await PortableScenarios.RetryAsync(runner);
    }

    [Fact]
    public async Task WaitOnly_NoSteps_Succeeds()
    {
        await using var runner = new DurableTestRunner<WaitRequest, WaitResult>(
            WaitOnlyWorkflow.RunAsync);

        await PortableScenarios.WaitOnlyAsync(runner);
    }

    [Fact]
    public async Task MultipleSteps_AllCheckpointed_Succeeds()
    {
        await using var runner = new DurableTestRunner<StepsRequest, StepsResult>(
            MultipleStepsWorkflow.RunAsync);

        await PortableScenarios.MultipleStepsAsync(runner);
    }

    [Fact]
    public async Task StepFails_PropagatesAsFailed()
    {
        await using var runner = new DurableTestRunner<StepsRequest, StepsResult>(
            StepFailsWorkflow.RunAsync);

        await PortableScenarios.StepFailsAsync(runner);
    }

    [Fact]
    public async Task StepWaitStep_ThreadsValueAcrossWait()
    {
        await using var runner = new DurableTestRunner<StepsRequest, StepsResult>(
            StepWaitStepWorkflow.RunAsync);

        await PortableScenarios.StepWaitStepAsync(runner);
    }

    [Fact]
    public async Task LongerWait_ThreadsValueAcrossWait()
    {
        await using var runner = new DurableTestRunner<StepsRequest, StepsResult>(
            LongerWaitWorkflow.RunAsync);

        await PortableScenarios.LongerWaitAsync(runner);
    }

    [Fact]
    public async Task RetryExhaustion_FailsAfterAllAttempts()
    {
        await using var runner = new DurableTestRunner<StepsRequest, StepsResult>(
            RetryExhaustionWorkflow.RunAsync);

        await PortableScenarios.RetryExhaustionAsync(runner);
    }

    [Fact]
    public async Task WaitForCondition_HappyPath_Succeeds()
    {
        await using var runner = new DurableTestRunner<ConditionRequest, ConditionResult>(
            WaitForConditionHappyWorkflow.RunAsync);

        await PortableScenarios.WaitForConditionHappyAsync(runner);
    }

    [Fact]
    public async Task WaitForCondition_MaxAttempts_Exhausts()
    {
        await using var runner = new DurableTestRunner<ConditionRequest, ConditionResult>(
            WaitForConditionMaxAttemptsWorkflow.RunAsync);

        await PortableScenarios.WaitForConditionMaxAttemptsAsync(runner);
    }

    [Fact]
    public async Task WaitForCondition_Exponential_Succeeds()
    {
        await using var runner = new DurableTestRunner<ConditionRequest, ConditionResult>(
            WaitForConditionExponentialWorkflow.RunAsync);

        await PortableScenarios.WaitForConditionExponentialAsync(runner);
    }

    [Fact]
    public async Task WaitForCondition_UserCheckThrows_Caught()
    {
        await using var runner = new DurableTestRunner<ConditionRequest, ConditionResult>(
            WaitForConditionUserCheckThrowsWorkflow.RunAsync);

        await PortableScenarios.WaitForConditionUserCheckThrowsAsync(runner);
    }

    [Fact]
    public async Task Parallel_HappyPath_Succeeds()
    {
        await using var runner = new DurableTestRunner<BatchRequest, BatchResult>(ParallelWorkflows.HappyAsync);
        await PortableScenarios.ParallelHappyAsync(runner);
    }

    [Fact]
    public async Task Parallel_PartialFailure_Tolerated()
    {
        await using var runner = new DurableTestRunner<BatchRequest, BatchResult>(ParallelWorkflows.PartialFailureAsync);
        await PortableScenarios.ParallelPartialFailureAsync(runner);
    }

    [Fact]
    public async Task Parallel_FirstSuccessful_ShortCircuits()
    {
        await using var runner = new DurableTestRunner<BatchRequest, BatchResult>(ParallelWorkflows.FirstSuccessfulAsync);
        await PortableScenarios.ParallelFirstSuccessfulAsync(runner);
    }

    [Fact]
    public async Task Parallel_FailureTolerance_Exceeded_Fails()
    {
        await using var runner = new DurableTestRunner<BatchRequest, BatchResult>(ParallelWorkflows.FailureToleranceAsync);
        await PortableScenarios.ParallelFailureToleranceAsync(runner);
    }

    [Fact]
    public async Task Parallel_MaxConcurrency_AllComplete()
    {
        await using var runner = new DurableTestRunner<BatchRequest, BatchResult>(ParallelWorkflows.MaxConcurrencyAsync);
        await PortableScenarios.ParallelMaxConcurrencyAsync(runner);
    }

    [Fact]
    public async Task Map_HappyPath_Succeeds()
    {
        await using var runner = new DurableTestRunner<BatchRequest, BatchResult>(MapWorkflows.HappyAsync);
        await PortableScenarios.MapHappyAsync(runner);
    }

    [Fact]
    public async Task Map_PartialFailure_Tolerated()
    {
        await using var runner = new DurableTestRunner<BatchRequest, BatchResult>(MapWorkflows.PartialFailureAsync);
        await PortableScenarios.MapPartialFailureAsync(runner);
    }

    [Fact]
    public async Task Map_FirstSuccessful_ShortCircuits()
    {
        await using var runner = new DurableTestRunner<BatchRequest, BatchResult>(MapWorkflows.FirstSuccessfulAsync);
        await PortableScenarios.MapFirstSuccessfulAsync(runner);
    }

    [Fact]
    public async Task Map_FailureTolerance_Exceeded_Fails()
    {
        await using var runner = new DurableTestRunner<BatchRequest, BatchResult>(MapWorkflows.FailureToleranceAsync);
        await PortableScenarios.MapFailureToleranceAsync(runner);
    }

    [Fact]
    public async Task Map_MaxConcurrency_AllComplete()
    {
        await using var runner = new DurableTestRunner<BatchRequest, BatchResult>(MapWorkflows.MaxConcurrencyAsync);
        await PortableScenarios.MapMaxConcurrencyAsync(runner);
    }

    [Fact]
    public async Task ChildContext_HappyPath_Succeeds()
    {
        await using var runner = new DurableTestRunner<StepsRequest, StepsResult>(ChildContextWorkflows.HappyAsync);
        await PortableScenarios.ChildContextHappyAsync(runner);
    }

    [Fact]
    public async Task ChildContext_Fails_PropagatesFailure()
    {
        await using var runner = new DurableTestRunner<StepsRequest, StepsResult>(ChildContextWorkflows.FailsAsync);
        await PortableScenarios.ChildContextFailsAsync(runner);
    }

    [Fact]
    public async Task ChildContext_RetryFails_PropagatesFailure()
    {
        await using var runner = new DurableTestRunner<StepsRequest, StepsResult>(ChildContextWorkflows.RetryFailsAsync);
        await PortableScenarios.ChildContextRetryFailsAsync(runner);
    }

    [Fact]
    public async Task InvokeFailure_ParentCatchesAndSucceeds()
    {
        await using var runner = new DurableTestRunner<ChainedInvokeRequest, ChainedInvokeResult>(
            InvokeFailureWorkflow.RunAsync);

        // The cloud backend deploys the failing downstream separately; locally we
        // register it as an in-process durable sibling under the same short name.
        runner.RegisterDurableFunction<ChildRequest, ChildResult>(
            InvokeFailureWorkflow.DownstreamFunctionName,
            InvokeFailureWorkflow.DownstreamAsync);

        await PortableScenarios.InvokeFailureAsync(runner);
    }

    [Fact]
    public async Task CallbackSubmitterFails_PropagatesFailure()
    {
        await using var runner = new DurableTestRunner<ApprovalRequest, ApprovalResult>(
            CallbackSubmitterFailsWorkflow.RunAsync);

        await PortableScenarios.CallbackSubmitterFailsAsync(runner);
    }
}
