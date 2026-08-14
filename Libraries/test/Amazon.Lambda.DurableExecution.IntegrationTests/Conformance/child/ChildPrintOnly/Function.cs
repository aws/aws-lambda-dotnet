// 3-17: Child context with durable logger only (verify no re-execution on replay)
// Child logs via the replay-aware durable logger and returns input (no durable
// ops), followed by a wait. Replay-aware filtering suppresses the line on the
// replay pass, so the input is logged exactly once.
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.Logging;

namespace ChildPrintOnly;

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
        var result = await context.RunInChildContextAsync(async (childContext, _ct) =>
        {
            await Task.CompletedTask;
            // Durable logger (records carry durableExecutionArn — the conformance
            // runner filters on that). Default replay-aware filtering suppresses
            // the line on replay, so the input is logged exactly once.
            childContext.Logger.LogInformation("{Input}", input);
            return input;
        }, name: "print-only", config: new ChildContextConfig { SubType = "RunInChildContext" });

        await context.WaitAsync(TimeSpan.FromSeconds(2));

        return result;
    }
}
