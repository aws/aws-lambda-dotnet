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
        => DurableFunction.WrapAsync<TestEvent, MyResult>(Workflow, input, context);

    private async Task<MyResult> Workflow(TestEvent input, IDurableContext context)
    {
        // The submitter throws on every attempt. With RetryStrategy.None the
        // SDK should fail terminally on the first attempt and surface the
        // failure as CallbackSubmitterException. The workflow does not catch
        // it, so the durable execution surfaces FAILED with that exception.
        var result = await context.WaitForCallbackAsync<MyResult>(
            submitter: async (callbackId, cbCtx, _) =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException("submitter intentional failure");
            },
            name: "approve",
            config: new WaitForCallbackConfig { RetryStrategy = RetryStrategy.None });

        return result;
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class MyResult { public string? Status { get; set; } public string? ApprovedBy { get; set; } }
