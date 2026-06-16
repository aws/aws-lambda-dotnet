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
        var batch = await context.ParallelAsync(
            new[]
            {
                new DurableBranch<string>("alpha", async (_, _) => { await Task.CompletedTask; return $"alpha-{input.OrderId}"; }),
                new DurableBranch<string>("beta",  async (_, _) => { await Task.CompletedTask; return $"beta-{input.OrderId}"; }),
                new DurableBranch<string>("gamma", async (_, _) => { await Task.CompletedTask; return $"gamma-{input.OrderId}"; }),
            },
            name: "fanout");

        var joined = string.Join(",", batch.GetResults());
        return new TestResult { Status = "completed", Data = joined };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
