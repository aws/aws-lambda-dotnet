// 1-10: Replay re-throws failed step
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.Logging;

namespace StepReplayRethrowsFailed;

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
        string errorMessage = "";

        try
        {
            await context.StepAsync<string>(
                async (stepContext, _ct) =>
                {
                    await Task.CompletedTask;
                    stepContext.Logger.LogInformation("step executed");
                    throw new InvalidOperationException("Something went wrong");
                },
                config: new StepConfig
                {
                    RetryStrategy = RetryStrategy.None
                });
        }
        catch (StepException ex)
        {
            errorMessage = ex.Message;
        }

        await context.WaitAsync(TimeSpan.FromSeconds(1));

        return $"caught: {errorMessage}";
    }
}
