// 3-6: Nested child contexts - outer child has step + inner child, inner child has step
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ChildNested;

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
        var result = await context.RunInChildContextAsync(async (outerChild, _ct1) =>
        {
            var outerStep = await outerChild.StepAsync(
                async (_, _ct) =>
                {
                    await Task.CompletedTask;
                    return input;
                });

            var innerResult = await outerChild.RunInChildContextAsync(async (innerChild, _ct2) =>
            {
                var innerStep = await innerChild.StepAsync(
                    async (_, _ct) =>
                    {
                        await Task.CompletedTask;
                        return outerStep;
                    });

                return innerStep;
            }, name: "inner", config: new ChildContextConfig { SubType = "RunInChildContext" });

            return innerResult;
        }, name: "outer", config: new ChildContextConfig { SubType = "RunInChildContext" });

        return result;
    }
}
