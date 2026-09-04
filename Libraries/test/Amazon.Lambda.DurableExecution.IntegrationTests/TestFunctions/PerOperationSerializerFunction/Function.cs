// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

/// <summary>
/// Deployed entry point exercising the per-operation serializer override.
///
/// The global serializer is <see cref="DefaultLambdaJsonSerializer"/> (AWS naming policy,
/// which preserves PascalCase property names). The workflow runs two steps that return the
/// same <see cref="Doc"/> record:
/// <list type="bullet">
///   <item><c>default_step</c> uses no config, so it is serialized with the global
///     serializer → checkpoint payload contains <c>"Message"</c> (PascalCase).</item>
///   <item><c>camel_step</c> overrides <see cref="StepConfig.Serializer"/> with
///     <see cref="CamelCaseLambdaJsonSerializer"/> → checkpoint payload contains
///     <c>"message"</c> (camelCase).</item>
/// </list>
/// The paired integration test asserts both payloads from the event history, proving the
/// per-step serializer is applied to exactly the configured step and nothing else.
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
        => DurableFunction.WrapAsync<object, Doc>(RunAsync, input, context);

    private static async Task<Doc> RunAsync(object input, IDurableContext ctx)
    {
        // Serialized with the global serializer (PascalCase "Message").
        _ = await ctx.StepAsync(
            async (_, _) => { await Task.CompletedTask; return new Doc("hi"); },
            name: "default_step");

        // Same result type, but this step overrides the serializer (camelCase "message").
        var viaCustom = await ctx.StepAsync(
            async (_, _) => { await Task.CompletedTask; return new Doc("hi"); },
            name: "camel_step",
            config: new StepConfig { Serializer = new CamelCaseLambdaJsonSerializer() });

        return viaCustom;
    }
}

public record Doc(string Message);
