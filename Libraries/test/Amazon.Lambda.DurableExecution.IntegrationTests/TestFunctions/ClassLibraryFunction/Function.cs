// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;

// Class-library programming model: no Main / LambdaBootstrap loop. The managed
// runtime invokes Handler directly via the Assembly::Type::Method handler string,
// and resolves the serializer from this assembly-level attribute. Proves durable
// functions work on the managed dotnet runtime without the executable model.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ClassLibraryFunction;

public class Function
{
    public Task<DurableExecutionInvocationOutput> Handler(
        DurableExecutionInvocationInput input, ILambdaContext context)
        => DurableFunction.WrapAsync<TestEvent, TestResult>(Workflow, input, context);

    private async Task<TestResult> Workflow(TestEvent input, IDurableContext context)
    {
        var step1 = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return $"a-{input.OrderId}"; },
            name: "step_1");

        var step2 = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return $"{step1}-b"; },
            name: "step_2");

        return new TestResult { Status = "completed", Data = step2 };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
