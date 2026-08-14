// 5-1: Invoke basic (target function succeeds)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace InvokeBasic;

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
        var targetFunctionName = Environment.GetEnvironmentVariable("TARGET_FUNCTION_NAME")!;
        var result = await context.InvokeAsync<string, string>(targetFunctionName, input);
        return result;
    }
}
