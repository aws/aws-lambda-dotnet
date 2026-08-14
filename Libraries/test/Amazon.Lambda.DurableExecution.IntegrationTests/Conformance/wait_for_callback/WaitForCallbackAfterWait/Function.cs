// 7-10: WaitForCallback after wait and step
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace WaitForCallbackAfterWait;

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
        await context.WaitAsync(TimeSpan.FromSeconds(1));

        await context.StepAsync(
            async (_, _ct) =>
            {
                await Task.CompletedTask;
                return "step-data";
            });

        var result = await context.WaitForCallbackAsync<string>(
            async (callbackId, callbackContext, ct) =>
            {
                await Task.CompletedTask;
            },
            name: input);

        return result;
    }
}
