// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.OpenTelemetry;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

namespace Amazon.Lambda.DurableExecution.ConformanceTests.Otel;

public static class Program
{
    // The collector layer is a Lambda extension, so spans must be handed to it before the environment freezes.
    private static readonly TracerProvider Tracer = Sdk.CreateTracerProviderBuilder()
        .AddAWSLambdaConfigurations()
        .AddSource(OtelPlugin.ActivitySourceName)
        .AddSource(Probes.SourceName)
        .AddOtlpExporter()
        .Build()!;

    private static readonly DurableExecutionOptions Options = new DurableExecutionOptions()
        .AddOpenTelemetry(
            Environment.GetEnvironmentVariable("OTEL_PLUGIN_MODE") == "execution" ? OtelView.Execution : OtelView.Invocation,
            new OtelPluginOptions
            {
                FlushAsync = (timeout, _) =>
                {
                    Tracer.ForceFlush((int)timeout.TotalMilliseconds);
                    return Task.CompletedTask;
                },
                FlushTimeout = TimeSpan.FromSeconds(5),
            });

    private static readonly Scenario Selected = Scenarios.Find(
        Environment.GetEnvironmentVariable("CONFORMANCE_SCENARIO")
        ?? throw new InvalidOperationException("CONFORMANCE_SCENARIO is not set"));

    public static async Task Main()
    {
        using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<DurableExecutionInvocationInput, DurableExecutionInvocationOutput>(
            Handler, new DefaultLambdaJsonSerializer());
        using var bootstrap = new LambdaBootstrap(handlerWrapper);
        await bootstrap.RunAsync();
    }

    // The wrapper starts the server span the plugin's Invocation and Workflow spans hang from, and flushes after it.
    public static Task<DurableExecutionInvocationOutput> Handler(DurableExecutionInvocationInput input, ILambdaContext context)
        => AWSLambdaWrapper.TraceAsync(Tracer, RunAsync, input, context);

    private static Task<DurableExecutionInvocationOutput> RunAsync(DurableExecutionInvocationInput input, ILambdaContext context)
        => DurableFunction.WrapAsync<JsonElement, object?>(
            (payload, durable) =>
            {
                Scenarios.RequireScenario(payload, Selected.PayloadScenario);
                return Selected.Run(payload, durable);
            },
            input, context, Options);
}
