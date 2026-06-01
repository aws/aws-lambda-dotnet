// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

/// <summary>
/// Five-failure retry chain: the step throws on attempts 1-5 and succeeds on
/// attempt 6. The result payload echoes ctx.AttemptNumber on each attempt so
/// the integration test can verify the SDK's user-facing attempt counter
/// matches the wire-format StepDetails.Attempt value across multiple
/// invocations.
/// </summary>
public class Function
{
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
        var result = await context.StepAsync<string>(
            async (ctx) =>
            {
                await Task.CompletedTask;
                if (ctx.AttemptNumber < 6)
                    throw new InvalidOperationException($"flake on attempt {ctx.AttemptNumber}");
                return $"ok on attempt {ctx.AttemptNumber}";
            },
            name: "long_retry_step",
            config: new StepConfig
            {
                // Short delays so the test wall time stays manageable: 1s, 2s, 3s, 4s, 5s.
                RetryStrategy = RetryStrategy.Exponential(
                    maxAttempts: 6,
                    initialDelay: TimeSpan.FromSeconds(1),
                    maxDelay: TimeSpan.FromSeconds(5),
                    backoffRate: 1.5,
                    jitter: JitterStrategy.None)
            });

        return new TestResult { Status = "completed", Data = result };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
