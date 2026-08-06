// 7-13: WaitForCallback heartbeat keeps callback alive
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace WaitForCallbackHeartbeatAlive;

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
        // 10-second heartbeat timeout; external sends heartbeat then success.
        var result = await context.WaitForCallbackAsync<string>(
            async (callbackId, callbackContext, ct) =>
            {
                await Task.CompletedTask;
            },
            name: input,
            config: new WaitForCallbackConfig { HeartbeatTimeout = TimeSpan.FromSeconds(10) });

        return result;
    }
}
