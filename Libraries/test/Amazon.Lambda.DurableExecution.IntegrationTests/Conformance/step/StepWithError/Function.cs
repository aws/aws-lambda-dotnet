// 1-19: Step with error (fails permanently)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace StepWithError;

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
        var result = await context.StepAsync<string>(
            async (_, _ct) =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException("Something went wrong");
            },
            config: new StepConfig
            {
                RetryStrategy = RetryStrategy.None
            });

        return result;
    }
}
