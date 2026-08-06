// 6-9: Wait-for-condition with complex object state
using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace WaitForConditionComplexObject;

public class PollState
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("attempts")]
    public int Attempts { get; set; }
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
        => DurableFunction.WrapAsync<object?, PollState>(Workflow, input, context);

    private async Task<PollState> Workflow(object? input, IDurableContext context)
    {
        var result = await context.WaitForConditionAsync(
            async (state, checkCtx, ct) =>
            {
                await Task.CompletedTask;
                var newAttempts = state.Attempts + 1;
                return new PollState
                {
                    Status = newAttempts >= 2 ? "DONE" : "PENDING",
                    Attempts = newAttempts
                };
            },
            new WaitForConditionConfig<PollState>
            {
                InitialState = new PollState { Status = "PENDING", Attempts = 0 },
                WaitStrategy = WaitStrategy.FromDelegate<PollState>((state, attempt) =>
                    state.Status == "DONE" ? WaitDecision.Stop() : WaitDecision.ContinueAfter(TimeSpan.FromSeconds(1)))
            });

        return result;
    }
}
