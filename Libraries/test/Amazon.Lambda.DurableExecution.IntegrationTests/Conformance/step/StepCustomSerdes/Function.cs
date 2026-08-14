// 1-6: Custom serdes (per-step) - transforms string to uppercase
// Note: The .NET SDK does not have a per-step serdes API like the JS SDK.
// Instead, we achieve the same effect by transforming the value within the step
// function itself, since the step result is what gets checkpointed.
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace StepCustomSerdes;

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
            async (_, _ct) =>
            {
                await Task.CompletedTask;
                // Simulate custom serdes by transforming to uppercase
                return input.ToUpperInvariant();
            });

        return result;
    }
}
