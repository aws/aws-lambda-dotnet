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
        var downstreamArn = System.Environment.GetEnvironmentVariable("DOWNSTREAM_FUNCTION_ARN")
            ?? throw new InvalidOperationException("DOWNSTREAM_FUNCTION_ARN env var is not set.");

        try
        {
            await context.InvokeAsync<int, string>(
                downstreamArn,
                payload: 1,
                name: "call_failing_child");

            // Should not reach — the child throws and the parent surfaces
            // InvokeFailedException on the resume.
            return new TestResult { Status = "unexpected_success", Data = null };
        }
        catch (InvokeFailedException ex)
        {
            // The parent catches and converts the exception into a normal result —
            // the workflow itself succeeds, even though the chained invoke failed.
            return new TestResult
            {
                Status = "completed",
                Data = $"parent-saw-{ex.ErrorType ?? "unknown"}"
            };
        }
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
