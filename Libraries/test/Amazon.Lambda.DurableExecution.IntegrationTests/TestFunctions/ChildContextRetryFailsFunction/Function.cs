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
        // A retry-then-exhaust step inside a child context: every retry
        // checkpoint should be parented under the child, and the child should
        // close as ContextFailed when retries are exhausted — proving the
        // child is a single retry/error boundary.
        await context.RunInChildContextAsync<string>(
            async (childCtx) =>
            {
                return await childCtx.StepAsync<string>(
                    async (ctx) =>
                    {
                        await Task.CompletedTask;
                        throw new InvalidOperationException(
                            $"always-fails on attempt {ctx.AttemptNumber} for {input.OrderId}");
                    },
                    name: "always_fails",
                    config: new StepConfig
                    {
                        RetryStrategy = RetryStrategy.Exponential(
                            maxAttempts: 3,
                            initialDelay: TimeSpan.FromSeconds(2),
                            maxDelay: TimeSpan.FromSeconds(10),
                            backoffRate: 2.0,
                            jitter: JitterStrategy.None)
                    });
            },
            name: "phase",
            config: new ChildContextConfig { SubType = "OrderProcessing" });

        return new TestResult { Status = "should_not_reach" };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
