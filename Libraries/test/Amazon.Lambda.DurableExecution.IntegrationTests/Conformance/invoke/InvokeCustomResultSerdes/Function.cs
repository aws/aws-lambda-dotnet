// 5-16: Invoke with custom result serdes (custom deserializer for the returned result)
// The custom serializer serializes the OUTBOUND request payload normally on the initial
// execution (its Serialize delegates to the default JSON serializer, so the checkpointed
// input and the ChainedInvokeSucceeded result both stay "hello"). It uppercases the value
// when the result is deserialized — which, for a chained invoke, happens on the replay
// that observes the completed invocation — so the workflow returns the uppercased "HELLO".
using System.IO;
using System.Text;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace InvokeCustomResultSerdes;

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

        var result = await context.InvokeAsync<string, string>(
            targetFunctionName,
            input,
            config: new InvokeConfig { Serializer = new UppercaseResultSerializer() });

        return result;
    }
}

/// <summary>
/// Custom result serializer: serializes the outgoing payload normally, but uppercases
/// the raw serialized result on deserialize (the "custom result serdes" applied to the
/// chained invoke's serialized payload — e.g. <c>"hello"</c> becomes <c>"HELLO"</c>,
/// so the JSON-decoded execution result is the string <c>"HELLO"</c>).
/// </summary>
public sealed class UppercaseResultSerializer : ILambdaSerializer
{
    private readonly DefaultLambdaJsonSerializer _inner = new();

    public T Deserialize<T>(Stream requestStream)
    {
        if (typeof(T) != typeof(string))
            throw new NotSupportedException(
                $"{nameof(UppercaseResultSerializer)} only supports string results; got {typeof(T)}.");
        using var reader = new StreamReader(requestStream, Encoding.UTF8);
        var raw = reader.ReadToEnd();
        return (T)(object)raw.ToUpperInvariant();
    }

    public void Serialize<T>(T response, Stream responseStream)
        => _inner.Serialize(response, responseStream);
}
