// 4-15: Custom serdes (JSON object with timestamp conversion)
using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace CallbackCustomSerdes;

public class CallbackPayload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;
}

public class ReceivedData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}

public class WorkflowResult
{
    [JsonPropertyName("received")]
    public ReceivedData Received { get; set; } = new();
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
        var callback = await context.CreateCallbackAsync<CallbackPayload>(name: input);
        var result = await callback.GetResultAsync();

        var epoch = DateTimeOffset.Parse(result.Timestamp).ToUnixTimeSeconds();

        return new WorkflowResult
        {
            Received = new ReceivedData
            {
                Id = result.Id,
                Message = result.Message,
                Timestamp = epoch
            }
        };
    }
}
