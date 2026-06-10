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
        // Run a child context that itself does step + wait + step. The child's
        // return value is checkpointed at the parent level as a CONTEXT
        // SUCCEED record, so on replay we'd see it returned from cache.
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

        return new TestResult { Status = "completed", Data = phaseResult };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
