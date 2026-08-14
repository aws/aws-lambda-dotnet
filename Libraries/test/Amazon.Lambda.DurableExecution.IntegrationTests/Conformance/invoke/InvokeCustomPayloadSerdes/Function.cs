// 5-15: Invoke with custom payload serdes (custom serializer for outgoing payload)
// Note: The .NET SDK does not have a per-invoke serdes API like the JS SDK.
// Instead, we achieve the same effect by transforming the payload before invoking,
// since the transformed payload is what gets sent to the target function.
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace InvokeCustomPayloadSerdes;

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
        => DurableFunction.WrapAsync<JsonElement, string>(Workflow, input, context);

    private async Task<string> Workflow(JsonElement input, IDurableContext context)
    {
        var targetFunctionName = Environment.GetEnvironmentVariable("TARGET_FUNCTION_NAME")!;

        // Custom payload serdes: transform the "data" field to uppercase before sending
        var data = input.GetProperty("data").GetString()!;
        var transformedPayload = data.ToUpperInvariant();

        var result = await context.InvokeAsync<string, string>(targetFunctionName, transformedPayload);
        return result;
    }
}
