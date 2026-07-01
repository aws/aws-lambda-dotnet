// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Testing;
using Amazon.Lambda.DurableExecution.Testing.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

/// <summary>
/// Runs the shared <see cref="PortableScenarios"/> against the deployed cloud
/// backend (<see cref="CloudDurableTestRunner{TInput, TOutput}"/>). These are the
/// real-AWS half of the portable-test pair: the local half lives in
/// <c>Amazon.Lambda.DurableExecution.Testing.Tests.PortableScenariosLocalTests</c>
/// and runs the very same scenario bodies in-process. Passing both proves that a
/// test coded to <see cref="IDurableTestRunner{TInput, TOutput}"/> runs unchanged
/// on either backend.
/// </summary>
/// <remarks>
/// Each test deploys its function(s) via <see cref="DurableFunctionDeployment"/>
/// (IAM role + zip on managed <c>dotnet10</c>), constructs a cloud runner against
/// the deployed ARN, runs the scenario, then tears everything down. The runner's
/// poll interval and timeout are widened because real durable waits and the
/// settlement poll fire genuine service-side timers.
/// </remarks>
public class PortableScenariosCloudTests
{
    private readonly ITestOutputHelper _output;
    public PortableScenariosCloudTests(ITestOutputHelper output) => _output = output;

    private static CloudTestRunnerOptions CloudOptions() => new()
    {
        PollInterval = TimeSpan.FromSeconds(2),
        DefaultTimeout = TimeSpan.FromMinutes(4),
    };

    [Fact]
    public async Task Fulfillment_AllOperationKinds_Succeeds()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("FulfillmentFunction"),
            "portable-fulfill", _output);

        await using var runner = new CloudDurableTestRunner<OrderRequest, FulfillmentResult>(
            deployment.FunctionArn + ":$LATEST",
            deployment.LambdaClient,
            CloudOptions());

        await PortableScenarios.RunFulfillmentAsync(runner);
    }

    [Fact]
    public async Task Approval_CallbackSuccessRoundTrip_Succeeds()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ApprovalFunction"),
            "portable-approve", _output,
            // The callback is delivered out-of-band by the test process, which
            // adds history-poll latency on top of deploy time — give the
            // execution generous headroom so it doesn't time out before delivery.
            executionTimeoutSeconds: 300);

        await using var runner = new CloudDurableTestRunner<ApprovalRequest, ApprovalResult>(
            deployment.FunctionArn + ":$LATEST",
            deployment.LambdaClient,
            CloudOptions());

        await PortableScenarios.ApproveAsync(runner);
    }

    [Fact]
    public async Task Approval_CallbackFailure_RejectsButCompletes()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ApprovalFunction"),
            "portable-reject", _output,
            executionTimeoutSeconds: 300);

        await using var runner = new CloudDurableTestRunner<ApprovalRequest, ApprovalResult>(
            deployment.FunctionArn + ":$LATEST",
            deployment.LambdaClient,
            CloudOptions());

        await PortableScenarios.RejectAsync(runner);
    }

    [Fact]
    public async Task Retry_FlakyStepSucceedsOnFinalAttempt()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("RetryFunction"),
            "portable-retry", _output);

        await using var runner = new CloudDurableTestRunner<RetryRequest, RetryResult>(
            deployment.FunctionArn + ":$LATEST",
            deployment.LambdaClient,
            CloudOptions());

        await PortableScenarios.RetryAsync(runner);
    }

    [Fact]
    public async Task WaitOnly_NoSteps_Succeeds()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitOnlyFunction"),
            "portable-waitonly", _output);

        await using var runner = new CloudDurableTestRunner<WaitRequest, WaitResult>(
            deployment.FunctionArn + ":$LATEST",
            deployment.LambdaClient,
            CloudOptions());

        await PortableScenarios.WaitOnlyAsync(runner);
    }

    [Fact]
    public async Task MultipleSteps_AllCheckpointed_Succeeds()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MultipleStepsFunction"),
            "portable-multi", _output);

        await using var runner = new CloudDurableTestRunner<StepsRequest, StepsResult>(
            deployment.FunctionArn + ":$LATEST",
            deployment.LambdaClient,
            CloudOptions());

        await PortableScenarios.MultipleStepsAsync(runner);
    }

    [Fact]
    public async Task StepFails_PropagatesAsFailed()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("StepFailsFunction"),
            "portable-stepfail", _output);

        await using var runner = new CloudDurableTestRunner<StepsRequest, StepsResult>(
            deployment.FunctionArn + ":$LATEST",
            deployment.LambdaClient,
            CloudOptions());

        await PortableScenarios.StepFailsAsync(runner);
    }

    [Fact]
    public async Task StepWaitStep_ThreadsValueAcrossWait()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("StepWaitStepFunction"),
            "portable-stepwait", _output);

        await using var runner = new CloudDurableTestRunner<StepsRequest, StepsResult>(
            deployment.FunctionArn + ":$LATEST",
            deployment.LambdaClient,
            CloudOptions());

        await PortableScenarios.StepWaitStepAsync(runner);
    }

    [Fact]
    public async Task LongerWait_ThreadsValueAcrossWait()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("LongerWaitFunction"),
            "portable-longwait", _output);

        await using var runner = new CloudDurableTestRunner<StepsRequest, StepsResult>(
            deployment.FunctionArn + ":$LATEST",
            deployment.LambdaClient,
            CloudOptions());

        await PortableScenarios.LongerWaitAsync(runner);
    }

    [Fact]
    public async Task RetryExhaustion_FailsAfterAllAttempts()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("RetryExhaustionFunction"),
            "portable-retryexh", _output);

        await using var runner = new CloudDurableTestRunner<StepsRequest, StepsResult>(
            deployment.FunctionArn + ":$LATEST",
            deployment.LambdaClient,
            CloudOptions());

        await PortableScenarios.RetryExhaustionAsync(runner);
    }

    [Fact]
    public async Task WaitForCondition_HappyPath_Succeeds()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitForConditionHappyPathFunction"),
            "portable-condhappy", _output);

        await using var runner = new CloudDurableTestRunner<ConditionRequest, ConditionResult>(
            deployment.FunctionArn + ":$LATEST",
            deployment.LambdaClient,
            CloudOptions());

        await PortableScenarios.WaitForConditionHappyAsync(runner);
    }

    [Fact]
    public async Task WaitForCondition_MaxAttempts_Exhausts()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitForConditionMaxAttemptsFunction"),
            "portable-condmax", _output);

        await using var runner = new CloudDurableTestRunner<ConditionRequest, ConditionResult>(
            deployment.FunctionArn + ":$LATEST",
            deployment.LambdaClient,
            CloudOptions());

        await PortableScenarios.WaitForConditionMaxAttemptsAsync(runner);
    }

    [Fact]
    public async Task WaitForCondition_Exponential_Succeeds()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitForConditionExponentialFunction"),
            "portable-condexp", _output);

        await using var runner = new CloudDurableTestRunner<ConditionRequest, ConditionResult>(
            deployment.FunctionArn + ":$LATEST",
            deployment.LambdaClient,
            CloudOptions());

        await PortableScenarios.WaitForConditionExponentialAsync(runner);
    }

    [Fact]
    public async Task WaitForCondition_UserCheckThrows_Caught()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitForConditionUserCheckThrowsFunction"),
            "portable-condthrow", _output);

        await using var runner = new CloudDurableTestRunner<ConditionRequest, ConditionResult>(
            deployment.FunctionArn + ":$LATEST",
            deployment.LambdaClient,
            CloudOptions());

        await PortableScenarios.WaitForConditionUserCheckThrowsAsync(runner);
    }

    [Fact]
    public async Task Parallel_HappyPath_Succeeds()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ParallelHappyPathFunction"),
            "portable-phappy", _output);
        await using var runner = new CloudDurableTestRunner<BatchRequest, BatchResult>(
            deployment.FunctionArn + ":$LATEST", deployment.LambdaClient, CloudOptions());
        await PortableScenarios.ParallelHappyAsync(runner);
    }

    [Fact]
    public async Task Parallel_PartialFailure_Tolerated()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ParallelPartialFailureFunction"),
            "portable-ppartial", _output);
        await using var runner = new CloudDurableTestRunner<BatchRequest, BatchResult>(
            deployment.FunctionArn + ":$LATEST", deployment.LambdaClient, CloudOptions());
        await PortableScenarios.ParallelPartialFailureAsync(runner);
    }

    [Fact]
    public async Task Parallel_FirstSuccessful_ShortCircuits()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ParallelFirstSuccessfulFunction"),
            "portable-pfirst", _output);
        await using var runner = new CloudDurableTestRunner<BatchRequest, BatchResult>(
            deployment.FunctionArn + ":$LATEST", deployment.LambdaClient, CloudOptions());
        await PortableScenarios.ParallelFirstSuccessfulAsync(runner);
    }

    [Fact]
    public async Task Parallel_FailureTolerance_Exceeded_Fails()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ParallelFailureToleranceFunction"),
            "portable-ptol", _output);
        await using var runner = new CloudDurableTestRunner<BatchRequest, BatchResult>(
            deployment.FunctionArn + ":$LATEST", deployment.LambdaClient, CloudOptions());
        await PortableScenarios.ParallelFailureToleranceAsync(runner);
    }

    [Fact]
    public async Task Parallel_MaxConcurrency_AllComplete()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ParallelMaxConcurrencyFunction"),
            "portable-pmax", _output);
        await using var runner = new CloudDurableTestRunner<BatchRequest, BatchResult>(
            deployment.FunctionArn + ":$LATEST", deployment.LambdaClient, CloudOptions());
        await PortableScenarios.ParallelMaxConcurrencyAsync(runner);
    }

    [Fact]
    public async Task Map_HappyPath_Succeeds()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MapHappyPathFunction"),
            "portable-mhappy", _output);
        await using var runner = new CloudDurableTestRunner<BatchRequest, BatchResult>(
            deployment.FunctionArn + ":$LATEST", deployment.LambdaClient, CloudOptions());
        await PortableScenarios.MapHappyAsync(runner);
    }

    [Fact]
    public async Task Map_PartialFailure_Tolerated()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MapPartialFailureFunction"),
            "portable-mpartial", _output);
        await using var runner = new CloudDurableTestRunner<BatchRequest, BatchResult>(
            deployment.FunctionArn + ":$LATEST", deployment.LambdaClient, CloudOptions());
        await PortableScenarios.MapPartialFailureAsync(runner);
    }

    [Fact]
    public async Task Map_FirstSuccessful_ShortCircuits()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MapFirstSuccessfulFunction"),
            "portable-mfirst", _output);
        await using var runner = new CloudDurableTestRunner<BatchRequest, BatchResult>(
            deployment.FunctionArn + ":$LATEST", deployment.LambdaClient, CloudOptions());
        await PortableScenarios.MapFirstSuccessfulAsync(runner);
    }

    [Fact]
    public async Task Map_FailureTolerance_Exceeded_Fails()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MapFailureToleranceFunction"),
            "portable-mtol", _output);
        await using var runner = new CloudDurableTestRunner<BatchRequest, BatchResult>(
            deployment.FunctionArn + ":$LATEST", deployment.LambdaClient, CloudOptions());
        await PortableScenarios.MapFailureToleranceAsync(runner);
    }

    [Fact]
    public async Task Map_MaxConcurrency_AllComplete()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MapMaxConcurrencyFunction"),
            "portable-mmax", _output);
        await using var runner = new CloudDurableTestRunner<BatchRequest, BatchResult>(
            deployment.FunctionArn + ":$LATEST", deployment.LambdaClient, CloudOptions());
        await PortableScenarios.MapMaxConcurrencyAsync(runner);
    }

    [Fact]
    public async Task ChildContext_HappyPath_Succeeds()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ChildContextFunction"),
            "portable-cchappy", _output);
        await using var runner = new CloudDurableTestRunner<StepsRequest, StepsResult>(
            deployment.FunctionArn + ":$LATEST", deployment.LambdaClient, CloudOptions());
        await PortableScenarios.ChildContextHappyAsync(runner);
    }

    [Fact]
    public async Task ChildContext_Fails_PropagatesFailure()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ChildContextFailsFunction"),
            "portable-ccfail", _output);
        await using var runner = new CloudDurableTestRunner<StepsRequest, StepsResult>(
            deployment.FunctionArn + ":$LATEST", deployment.LambdaClient, CloudOptions());
        await PortableScenarios.ChildContextFailsAsync(runner);
    }

    [Fact]
    public async Task ChildContext_RetryFails_PropagatesFailure()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ChildContextRetryFailsFunction"),
            "portable-ccretryfail", _output);
        await using var runner = new CloudDurableTestRunner<StepsRequest, StepsResult>(
            deployment.FunctionArn + ":$LATEST", deployment.LambdaClient, CloudOptions());
        await PortableScenarios.ChildContextRetryFailsAsync(runner);
    }

    [Fact]
    public async Task InvokeFailure_ParentCatchesAndSucceeds()
    {
        var (parent, downstream) = await DurableFunctionDeployment.CreateWithDownstreamAsync(
            parentTestFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("InvokeFailureParentFunction"),
            downstreamTestFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("InvokeFailureChildFunction"),
            scenarioSuffix: "portable-invfail",
            output: _output);

        await using (downstream)
        await using (parent)
        {
            await using var runner = new CloudDurableTestRunner<ChainedInvokeRequest, ChainedInvokeResult>(
                parent.FunctionArn + ":$LATEST", parent.LambdaClient, CloudOptions());
            await PortableScenarios.InvokeFailureAsync(runner);
        }
    }

    [Fact]
    public async Task CallbackSubmitterFails_PropagatesFailure()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitForCallbackSubmitterFailsFunction"),
            "portable-subfail", _output);
        await using var runner = new CloudDurableTestRunner<ApprovalRequest, ApprovalResult>(
            deployment.FunctionArn + ":$LATEST", deployment.LambdaClient, CloudOptions());
        await PortableScenarios.CallbackSubmitterFailsAsync(runner);
    }

    [Fact]
    public async Task ChainedInvoke_DeployedDownstream_Succeeds()
    {
        var (parent, downstream) = await DurableFunctionDeployment.CreateWithDownstreamAsync(
            parentTestFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("ChainedInvokeParentFunction"),
            downstreamTestFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("ChainedInvokeChildFunction"),
            scenarioSuffix: "portable-chain",
            output: _output);

        await using (downstream)
        await using (parent)
        {
            await using var runner = new CloudDurableTestRunner<ChainedInvokeRequest, ChainedInvokeResult>(
                parent.FunctionArn + ":$LATEST",
                parent.LambdaClient,
                CloudOptions());

            await PortableScenarios.ChainedInvokeAsync(runner);
        }
    }
}
