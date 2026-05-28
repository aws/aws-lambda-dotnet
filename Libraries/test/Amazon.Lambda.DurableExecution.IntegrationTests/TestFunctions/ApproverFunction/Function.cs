using System.IO;
using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ApproverFunction;

/// <summary>
/// Plain Lambda that acts as the "external system" in the WaitForCallback
/// integration test. Receives a callback ID + payload bits, builds the result
/// JSON, and resolves the durable execution by calling
/// SendDurableExecutionCallbackSuccess. Modeled after the real-world pattern
/// where an out-of-band service signals workflow completion.
/// </summary>
public class Function
{
    private static readonly IAmazonLambda LambdaClient = new AmazonLambdaClient();

    public static async Task Main(string[] args)
    {
        var handler = new Function();
        var serializer = new DefaultLambdaJsonSerializer();
        using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<ApproverInput, object?>(handler.Handler, serializer);
        using var bootstrap = new LambdaBootstrap(handlerWrapper);
        await bootstrap.RunAsync();
    }

    public async Task<object?> Handler(ApproverInput input, ILambdaContext context)
    {
        if (string.IsNullOrEmpty(input.CallbackId))
            throw new ArgumentException("CallbackId is required");

        var resultJson = $$"""{"Status":"approved","ApprovedBy":"{{input.OrderId}}"}""";
        await LambdaClient.SendDurableExecutionCallbackSuccessAsync(
            new SendDurableExecutionCallbackSuccessRequest
            {
                CallbackId = input.CallbackId,
                Result = new MemoryStream(Encoding.UTF8.GetBytes(resultJson))
            });
        return null;
    }
}

public class ApproverInput
{
    public string? CallbackId { get; set; }
    public string? OrderId { get; set; }
}
