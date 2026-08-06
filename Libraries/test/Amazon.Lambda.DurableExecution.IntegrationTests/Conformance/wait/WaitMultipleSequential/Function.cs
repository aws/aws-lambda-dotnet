// 2-3: Multiple sequential waits
using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace WaitMultipleSequential;

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
        => DurableFunction.WrapAsync<object?, WaitResult>(Workflow, input, context);

    private async Task<WaitResult> Workflow(object? input, IDurableContext context)
    {
        await context.WaitAsync(TimeSpan.FromSeconds(2), name: "wait-1");
        await context.WaitAsync(TimeSpan.FromSeconds(2), name: "wait-2");
        return new WaitResult { CompletedWaits = 2 };
    }
}

public class WaitResult
{
    [JsonPropertyName("completedWaits")]
    public int CompletedWaits { get; set; }
}
