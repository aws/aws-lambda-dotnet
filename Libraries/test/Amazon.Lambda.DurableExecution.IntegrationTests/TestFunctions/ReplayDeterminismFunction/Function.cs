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
        // Step 1 generates a fresh GUID. On replay, this MUST return the cached value.
        var generatedId = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return Guid.NewGuid().ToString(); },
            name: "generate_id");

        // Force a suspend/resume cycle to trigger replay
        await context.WaitAsync(TimeSpan.FromSeconds(3), name: "boundary_wait");

        // Step 2 echoes the GUID. After replay, it should see the SAME GUID from step 1.
        var echoed = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return $"echo:{generatedId}"; },
            name: "echo_id");

        return new TestResult { Status = "completed", Data = echoed };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
