// 1-17: Step with AtMostOncePerRetry semantics (interrupted, no retry)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.Logging;

namespace StepAtMostOnceNoRetry;

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
        var result = await context.StepAsync(
            async (stepContext, _ct) =>
            {
                await Task.CompletedTask;
                // Log input via durable step logger (records carry durableExecutionArn
                // — the conformance runner filters on that structured field).
                stepContext.Logger.LogInformation("{Input}", input);
                // Simulate Lambda crash
                Environment.Exit(1);
                return "unreachable";
            },
            name: "at_most_once_flaky_step",
            config: new StepConfig
            {
                Semantics = StepSemantics.AtMostOncePerRetry,
                RetryStrategy = RetryStrategy.None
            });

        return result;
    }
}
