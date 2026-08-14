// 1-4: Returning complex object
using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace StepComplexObject;

public class InputEvent
{
    public string Name { get; set; } = "";
    public List<string> Tags { get; set; } = new();
}

public class UserInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

public class OutputResult
{
    [JsonPropertyName("user")]
    public UserInfo User { get; set; } = new();

    [JsonPropertyName("count")]
    public int Count { get; set; }
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
        => DurableFunction.WrapAsync<InputEvent, OutputResult>(Workflow, input, context);

    private async Task<OutputResult> Workflow(InputEvent input, IDurableContext context)
    {
        var result = await context.StepAsync(
            async (_, _ct) =>
            {
                await Task.CompletedTask;
                return new OutputResult
                {
                    User = new UserInfo
                    {
                        Name = input.Name,
                        Tags = input.Tags
                    },
                    Count = input.Tags.Count
                };
            });

        return result;
    }
}
