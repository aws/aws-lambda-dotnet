// 5-8: Invoke with tenantId (tenant-isolated invocation)
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace InvokeWithTenantId;

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
        var tenantId = input.GetProperty("tenantId").GetString()!;
        var payload = input.GetProperty("payload").GetString()!;

        var result = await context.InvokeAsync<string, string>(
            targetFunctionName,
            payload,
            config: new InvokeConfig { TenantId = tenantId });

        return result;
    }
}
