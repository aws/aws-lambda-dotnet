// 3-5: Child context error caught - child with failing step is caught, recovery step returns input
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ChildErrorCaught;

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
        try
        {
            await context.RunInChildContextAsync(async (childContext, _ct) =>
            {
                await childContext.StepAsync<string>(
                    async (_, _ct) =>
                    {
                        await Task.CompletedTask;
                        throw new InvalidOperationException("step failed");
                    },
                    config: new StepConfig
                    {
                        RetryStrategy = RetryStrategy.None
                    });

                return "unreachable";
            }, config: new ChildContextConfig { SubType = "RunInChildContext" });
        }
        catch (Exception)
        {
            // Error caught, continue with recovery
        }

        var result = await context.StepAsync(
            async (_, _ct) =>
            {
                await Task.CompletedTask;
                return input;
            });

        return result;
    }
}
