// 1-6: Custom serdes (per-step) via StepConfig.Serializer
// The step returns the input unchanged; a custom per-step serializer transforms the
// result to uppercase on serialize. Thanks to the SDK's fresh-success round-trip
// (the result is deserialized from the just-written checkpoint before being returned),
// the uppercased value is what the workflow observes and returns — so the execution
// result is the uppercased input.
using System.IO;
using System.Text;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace StepCustomSerdes;

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
        var result = await context.StepAsync(
            async (_, _ct) =>
            {
                await Task.CompletedTask;
                return input;
            },
            config: new StepConfig { Serializer = new UppercaseSerializer() });

        return result;
    }
}

/// <summary>
/// A custom per-step serializer that uppercases the result on serialize (writing the
/// raw transformed text) and reads it back verbatim on deserialize.
/// </summary>
public sealed class UppercaseSerializer : ILambdaSerializer
{
    public T Deserialize<T>(Stream requestStream)
    {
        if (typeof(T) != typeof(string))
            throw new NotSupportedException(
                $"{nameof(UppercaseSerializer)} only supports string results; got {typeof(T)}.");
        using var reader = new StreamReader(requestStream, Encoding.UTF8);
        return (T)(object)reader.ReadToEnd();
    }

    public void Serialize<T>(T response, Stream responseStream)
    {
        var value = (response as string ?? response?.ToString() ?? string.Empty).ToUpperInvariant();
        var bytes = Encoding.UTF8.GetBytes(value);
        responseStream.Write(bytes, 0, bytes.Length);
    }
}
