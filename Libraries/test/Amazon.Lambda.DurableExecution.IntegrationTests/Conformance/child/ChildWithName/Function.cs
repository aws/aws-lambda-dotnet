// 3-2: Child context with name - named child context
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ChildWithName;

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
        => DurableFunction.WrapAsync<JsonElement, string>(Workflow, input, context);

    private async Task<string> Workflow(JsonElement input, IDurableContext context)
    {
        var name = input.GetProperty("name").GetString()!;
        var value = input.GetProperty("value").GetString()!;

        var result = await context.RunInChildContextAsync(async (childContext, _ct) =>
        {
            var stepResult = await childContext.StepAsync(
                async (_, _ct) =>
                {
                    await Task.CompletedTask;
                    return value;
                });

            return stepResult;
        }, name: name, config: new ChildContextConfig { SubType = "RunInChildContext" });

        return result;
    }
}
