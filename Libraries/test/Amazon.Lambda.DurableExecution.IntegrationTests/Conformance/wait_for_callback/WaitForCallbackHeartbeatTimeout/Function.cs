// 7-12: WaitForCallback heartbeat timeout (uncaught)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace WaitForCallbackHeartbeatTimeout;

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
        => DurableFunction.WrapAsync<string, string>(Workflow, input, context);

    private async Task<string> Workflow(string input, IDurableContext context)
    {
        // 5-second heartbeat timeout; no heartbeat sent. Do NOT catch.
        var result = await context.WaitForCallbackAsync<string>(
            async (callbackId, callbackContext, ct) =>
            {
                await Task.CompletedTask;
            },
            name: input,
            config: new WaitForCallbackConfig { HeartbeatTimeout = TimeSpan.FromSeconds(5) });

        return result;
    }
}
