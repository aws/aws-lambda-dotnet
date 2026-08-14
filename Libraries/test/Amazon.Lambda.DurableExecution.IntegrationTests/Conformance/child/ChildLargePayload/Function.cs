// 3-11: Child context large payload (ReplayChildren mode)
// The step returns a small value; the child context body builds a large
// (>256KB) result from it, triggering ReplayChildren mode. A wait after the
// child forces a suspend/replay cycle so the child body runs twice.
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.Logging;

namespace ChildLargePayload;

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
        => DurableFunction.WrapAsync<string, object>(Workflow, input, context);

    private async Task<object> Workflow(string input, IDurableContext context)
    {
        var result = await context.RunInChildContextAsync<string>(async (childContext, _ct) =>
        {
            // Log the input via the durable logger (records carry
            // durableExecutionArn — the conformance runner filters on that).
            // Disable replay-aware filtering so the ReplayChildren re-execution
            // also emits the line: the requirement expects it logged twice.
            childContext.ConfigureLogger(new LoggerConfig { ModeAware = false });
            childContext.Logger.LogInformation("{Input}", input);

            // Step returns a SMALL value.
            var stepResult = await childContext.StepAsync(
                async (_, _ct) =>
                {
                    await Task.CompletedTask;
                    return new string('A', 50 * 1024); // ~50KB seed
                });

            // Build a large result (>256KB) from the small step result.
            return string.Concat(Enumerable.Repeat(stepResult, 6)); // ~300KB
        }, name: "large-data-processor", config: new ChildContextConfig { SubType = "RunInChildContext" });

        // Wait after the child forces a suspend/replay cycle.
        await context.WaitAsync(TimeSpan.FromSeconds(2));

        return new { success = true, dataSize = result.Length };
    }
}
