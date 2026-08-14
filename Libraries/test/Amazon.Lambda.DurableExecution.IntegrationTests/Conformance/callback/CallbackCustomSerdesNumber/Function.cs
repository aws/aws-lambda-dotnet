// 4-16: Custom serdes (number to structured result)
using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace CallbackCustomSerdesNumber;

public class WorkflowResult
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("doubled")]
    public int Doubled { get; set; }
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
        => DurableFunction.WrapAsync<string, WorkflowResult>(Workflow, input, context);

    private async Task<WorkflowResult> Workflow(string input, IDurableContext context)
    {
        var callback = await context.CreateCallbackAsync<int>(name: input);
        var result = await callback.GetResultAsync();

        return new WorkflowResult
        {
            Count = result,
            Doubled = result * 2
        };
    }
}
