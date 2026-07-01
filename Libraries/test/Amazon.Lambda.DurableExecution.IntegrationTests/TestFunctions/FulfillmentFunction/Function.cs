// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Testing.Shared;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

/// <summary>
/// Deployed entry point for <see cref="FulfillmentWorkflow"/>. The workflow body
/// is shared verbatim with the local test backend so the portable cloud scenario
/// asserts identically against either runner.
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
        => DurableFunction.WrapAsync<OrderRequest, FulfillmentResult>(
            FulfillmentWorkflow.RunAsync, input, context);
}
