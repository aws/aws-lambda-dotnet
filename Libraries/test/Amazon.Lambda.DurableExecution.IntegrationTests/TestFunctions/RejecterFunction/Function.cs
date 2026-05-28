using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace RejecterFunction;

/// <summary>
/// Plain Lambda that acts as the "external system" in the CallbackFailed
/// integration test. Receives a callback ID and resolves the durable execution
/// as failed by calling SendDurableExecutionCallbackFailure. Modeled after
/// ApproverFunction (its happy-path counterpart).
/// </summary>
public class Function
{
    private static readonly IAmazonLambda LambdaClient = new AmazonLambdaClient();

    public static async Task Main(string[] args)
    {
        var handler = new Function();
        var serializer = new DefaultLambdaJsonSerializer();
        using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<RejecterInput, object?>(handler.Handler, serializer);
        using var bootstrap = new LambdaBootstrap(handlerWrapper);
        await bootstrap.RunAsync();
    }

    public async Task<object?> Handler(RejecterInput input, ILambdaContext context)
    {
        if (string.IsNullOrEmpty(input.CallbackId))
            throw new ArgumentException("CallbackId is required");

        await LambdaClient.SendDurableExecutionCallbackFailureAsync(
            new SendDurableExecutionCallbackFailureRequest
            {
                CallbackId = input.CallbackId,
                Error = new ErrorObject
                {
                    ErrorType = "ApprovalRejected",
                    ErrorMessage = "external system rejected the request",
                }
            });
        return null;
    }
}

public class RejecterInput
{
    public string? CallbackId { get; set; }
    public string? OrderId { get; set; }
}
