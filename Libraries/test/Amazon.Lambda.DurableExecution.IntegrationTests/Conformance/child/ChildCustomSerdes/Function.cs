// 3-14: Child context with custom serdes (succeed)
// A child context whose custom serializer uppercases the child result on serialize.
// The inner step returns the input via the global serializer (so its checkpoint stays
// "hello child"); the child context serializes its result with the custom serdes
// (raw uppercased "HELLO CHILD"). Thanks to the SDK's fresh-success round-trip, the
// child context returns the uppercased value, so the execution result is uppercased.
using System.IO;
using System.Text;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ChildCustomSerdes;

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
        var result = await context.RunInChildContextAsync(
            async (childContext, _ct) =>
            {
                var stepResult = await childContext.StepAsync(
                    async (_, _s) =>
                    {
                        await Task.CompletedTask;
                        return input;
                    });

                return stepResult;
            },
            name: "child-serdes",
            config: new ChildContextConfig
            {
                SubType = "RunInChildContext",
                Serializer = new UppercaseSerializer(),
            });

        return result;
    }
}

/// <summary>
/// A custom serializer that uppercases the result on serialize (writing the raw
/// transformed text) and reads it back verbatim on deserialize.
/// </summary>
public sealed class UppercaseSerializer : ILambdaSerializer
{
    public T Deserialize<T>(Stream requestStream)
    {
        using var reader = new StreamReader(requestStream);
        return (T)(object)reader.ReadToEnd();
    }

    public void Serialize<T>(T response, Stream responseStream)
    {
        var value = (response as string ?? response?.ToString() ?? string.Empty).ToUpperInvariant();
        var bytes = Encoding.UTF8.GetBytes(value);
        responseStream.Write(bytes, 0, bytes.Length);
    }
}
