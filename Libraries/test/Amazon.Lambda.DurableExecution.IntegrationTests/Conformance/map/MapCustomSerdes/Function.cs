// 9-14: Map with a custom per-item serdes (ItemSerializer)
// Each iteration returns the uppercased item directly; the custom per-item
// serializer wraps the value as `wrapped:<value>` on serialize (the checkpointed
// iteration payload) and unwraps it on deserialize, so the ordered results
// survive the round-trip. MaxConcurrency=1 gives a deterministic history.
using System.IO;
using System.Text;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace MapCustomSerdes;

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
        => DurableFunction.WrapAsync<object?, List<string>>(Workflow, input, context);

    private async Task<List<string>> Workflow(object? input, IDurableContext context)
    {
        var result = await context.MapAsync<string, string>(
            new List<string> { "x", "y" },
            async (ctx, item, index, all, ct) => item.ToUpperInvariant(),
            name: "serdes",
            config: new MapConfig<string>
            {
                MaxConcurrency = 1,
                ItemSerializer = new WrapSerializer(),
            });

        return result.GetResults().ToList();
    }
}

/// <summary>
/// A real (non-identity) per-item serializer: serializes a string value <c>v</c>
/// as the raw text <c>wrapped:v</c> and deserializes <c>wrapped:v</c> back to
/// <c>v</c>. Used only for per-item results, so it only handles strings.
/// </summary>
public sealed class WrapSerializer : ILambdaSerializer
{
    private const string Prefix = "wrapped:";

    public T Deserialize<T>(Stream requestStream)
    {
        using var reader = new StreamReader(requestStream);
        var text = reader.ReadToEnd();
        if (text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            text = text.Substring(Prefix.Length);
        }
        return (T)(object)text;
    }

    public void Serialize<T>(T response, Stream responseStream)
    {
        var text = response as string ?? response?.ToString() ?? string.Empty;
        var bytes = Encoding.UTF8.GetBytes(Prefix + text);
        responseStream.Write(bytes, 0, bytes.Length);
    }
}
