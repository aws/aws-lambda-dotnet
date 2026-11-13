// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

/// <summary>
/// Deployed entry point exercising the per-item serializer on Map and Parallel.
///
/// Global serializer is <see cref="DefaultLambdaJsonSerializer"/> (PascalCase). A control
/// step (no config) serializes its <see cref="Doc"/> result with the global serializer
/// (payload contains <c>"Message"</c>). The map items (m-0, m-1) and the parallel branch
/// (p-0) each set <c>ItemSerializer = CamelCaseLambdaJsonSerializer</c>, so their per-item
/// child-context result payloads are camelCase (<c>"message"</c>). Each item/branch returns
/// a <see cref="Doc"/> directly (no inner step) so the only per-item serialization is the
/// item result itself. The paired integration test asserts this from event history.
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
        => DurableFunction.WrapAsync<object, IReadOnlyList<Doc>>(RunAsync, input, context);

    private static async Task<IReadOnlyList<Doc>> RunAsync(object input, IDurableContext ctx)
    {
        // Control: global serializer → PascalCase "Message".
        await ctx.StepAsync(
            async (_, _) => { await Task.CompletedTask; return new Doc("ctrl"); },
            name: "control_step");

        // Map: per-item camelCase serializer → each item result "message".
        var map = await ctx.MapAsync(
            new[] { "a", "b" },
            async (_, item, _, _, _) => { await Task.CompletedTask; return new Doc(item); },
            name: "map",
            config: new MapConfig<string>
            {
                ItemSerializer = new CamelCaseLambdaJsonSerializer(),
                ItemNamer = (item, idx) => $"m-{idx}",
            });

        // Parallel: per-branch camelCase serializer.
        await ctx.ParallelAsync(
            new[]
            {
                new DurableBranch<Doc>("p-0", async (_, _) => { await Task.CompletedTask; return new Doc("px"); }),
            },
            name: "par",
            config: new ParallelConfig { ItemSerializer = new CamelCaseLambdaJsonSerializer() });

        return map.GetResults();
    }
}

public record Doc(string Message);
