// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;

// Lambda Annotations programming model for durable execution, CLASS-LIBRARY variant. There is NO
// GenerateMain and NO hand-written Main: the source generator emits only the durable handler wrapper
// (a single DurableFunction.WrapAsync delegation). The managed dotnet10 runtime hosts its own bootstrap,
// resolves the serializer from the assembly attribute below, surfaces it on ILambdaContext.Serializer
// (where DurableFunction.WrapAsync reads it), and invokes the generated wrapper directly via an
// Assembly::Type::Method handler string.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AnnotationsClassLibraryFunction;

public class Function
{
    // The generator turns this into a wrapper class Function_Workflow_Generated whose Workflow method
    // delegates to DurableFunction.WrapAsync<TestEvent, TestResult>(Workflow, __request__, __context__) —
    // the exact entry point the hand-written and executable test functions call, so the
    // checkpoint/replay/history behavior is identical.
    [LambdaFunction]
    [DurableExecution]
    public async Task<TestResult> Workflow(TestEvent input, IDurableContext context)
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
