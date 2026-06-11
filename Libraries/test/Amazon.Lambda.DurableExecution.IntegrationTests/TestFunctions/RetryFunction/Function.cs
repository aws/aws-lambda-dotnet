// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

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
            async (ctx, _) =>
            {
                await Task.CompletedTask;
                if (ctx.AttemptNumber < 3)
                    throw new InvalidOperationException($"flake on attempt {ctx.AttemptNumber}");
                return $"ok on attempt {ctx.AttemptNumber}";
            },
            name: "flaky_step",
            config: new StepConfig
            {
                RetryStrategy = RetryStrategy.Exponential(
                    maxAttempts: 3,
                    initialDelay: TimeSpan.FromSeconds(2),
                    maxDelay: TimeSpan.FromSeconds(10),
                    backoffRate: 2.0,
                    jitter: JitterStrategy.None)
            });

        return new TestResult { Status = "completed", Data = result };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
