// 4-8: CreateCallback (5s timeout) then step then await - times out
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace CallbackTimeoutAfterStep;

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
        var callback = await context.CreateCallbackAsync<string>(
            name: input,
            config: new CallbackConfig { Timeout = TimeSpan.FromSeconds(5) });

        var stepResult = await context.StepAsync(
            async (_, _ct) =>
            {
                await Task.CompletedTask;
                return "step_done";
            });

        var callbackResult = await callback.GetResultAsync();
        return callbackResult;
    }
}
