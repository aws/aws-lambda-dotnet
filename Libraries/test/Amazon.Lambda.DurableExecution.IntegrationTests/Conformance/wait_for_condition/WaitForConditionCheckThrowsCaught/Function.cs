// 6-8: Wait-for-condition check throws, caught by handler (recovers)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace WaitForConditionCheckThrowsCaught;

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
        => DurableFunction.WrapAsync<object?, string>(Workflow, input, context);

    private async Task<string> Workflow(object? input, IDurableContext context)
    {
        try
        {
            await context.WaitForConditionAsync<int>(
                async (state, checkCtx, ct) =>
                {
                    await Task.CompletedTask;
                    throw new InvalidOperationException("check function error");
                },
                new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.FromDelegate<int>((state, attempt) =>
                        WaitDecision.ContinueAfter(TimeSpan.FromSeconds(1)))
                });
        }
        catch (Exception)
        {
            return "recovered";
        }

        return "unreachable";
    }
}
