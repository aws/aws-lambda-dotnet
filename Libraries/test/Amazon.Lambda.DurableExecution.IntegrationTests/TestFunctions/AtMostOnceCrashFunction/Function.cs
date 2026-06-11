// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

/// <summary>
/// Exercises the AtMostOncePerRetry crash-recovery path end-to-end.
///
/// On attempt 1 the step kills the Lambda process AFTER the START checkpoint
/// has been flushed but BEFORE any SUCCEED checkpoint can be written. The
/// service re-invokes us; replay sees STARTED with no terminal record, so the
/// SDK routes through the retry strategy with a synthesized
/// StepInterruptedException. Attempt 2 succeeds normally.
///
/// The per-attempt counter is read from the input payload — the durable
/// service preserves it across re-invokes so we can drive deterministic crash
/// behavior on attempt 1 only.
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
            async (ctx, _) =>
            {
                await Task.CompletedTask;
                if (ctx.AttemptNumber == 1)
                {
                    // Hard process exit AFTER the SDK has flushed the START
                    // checkpoint (sync flush is part of the AtMostOncePerRetry
                    // contract). The service will see a STARTED record with no
                    // terminal counterpart on the next invocation.
                    Environment.Exit(137);
                }
                return $"recovered on attempt {ctx.AttemptNumber}";
            },
            name: "crash_then_recover",
            config: new StepConfig
            {
                Semantics = StepSemantics.AtMostOncePerRetry,
                RetryStrategy = RetryStrategy.Exponential(
                    maxAttempts: 3,
                    initialDelay: TimeSpan.FromSeconds(2),
                    maxDelay: TimeSpan.FromSeconds(5),
                    backoffRate: 2.0,
                    jitter: JitterStrategy.None)
            });

        return new TestResult { Status = "completed", Data = result };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
