// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Instrumentation;

// Durable execution via the Lambda Annotations programming model, class-library variant. The source
// generator emits the durable handler wrapper (Function_Workflow_Generated) that delegates to
// DurableFunction.WrapAsync; the managed dotnet10 runtime resolves the serializer from this assembly
// attribute and invokes the wrapper via an Assembly::Type::Method handler string.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TestDurableServerlessApp;

public class Function
{
    // [LambdaFunction] + [DurableExecution] => the generator emits DurableConfig (with the required
    // ExecutionTimeout) and the checkpoint-API IAM policy into serverless.template, and a wrapper that
    // calls DurableFunction.WrapAsync<TestEvent, TestResult>. Two chained steps exercise checkpointing.
    //
    // Tracing registers the OpenTelemetry plugin and PluginProvider adds more plugin factories, so the generated
    // wrapper is built against the real plugin API on every build. With no listener on the activity source the
    // plugin does nothing, so the workflow behaves as it did without them.
    [LambdaFunction]
    [DurableExecution(executionTimeout: 300, Tracing = DurableTracing.Execution, PluginProvider = nameof(GetPlugins))]
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

    internal static IEnumerable<IDurableInstrumentationPluginFactory> GetPlugins() => new IDurableInstrumentationPluginFactory[] { new PassThroughPluginFactory() };

    // A plugin factory that opts out of every invocation. It only proves a provider's factories are registered.
    private sealed class PassThroughPluginFactory : IDurableInstrumentationPluginFactory
    {
        public IDurableInstrumentationPlugin? CreatePlugin(InvocationInfo info) => null;
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
