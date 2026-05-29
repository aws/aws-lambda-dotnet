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
        => DurableFunction.WrapAsync<int, string>(Workflow, input, context);

    private async Task<string> Workflow(int input, IDurableContext context)
    {
        // Throw inside a step so the workflow records a step-failed event AND
        // surfaces a FAILED execution status. The parent's InvokeAsync sees a
        // FAILED chained invocation and raises InvokeFailedException with the
        // step's error type (System.InvalidOperationException) attached.
        await context.StepAsync<string>(
            async (_) =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException("intentional child failure");
            },
            name: "fail_step");

        return "unreachable";
    }
}
