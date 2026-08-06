// 1-16: Retry specific exception (non-retryable fails)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace StepRetryNonRetryable;

public class TransientError : Exception
{
    public TransientError(string message) : base(message) { }
}

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
                throw new TransientError("Temporary failure");
            },
            config: new StepConfig
            {
                // Only retry ArgumentException, not TransientError
                RetryStrategy = RetryStrategy.FromDelegate((error, attempts) =>
                {
                    if (error is ArgumentException && attempts < 3)
                        return RetryDecision.RetryAfter(TimeSpan.FromSeconds(1));
                    return RetryDecision.DoNotRetry();
                })
            });

        return result;
    }
}
