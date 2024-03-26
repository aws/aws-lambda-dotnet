using Amazon.Lambda.Core;
using AWS.Messaging;

namespace BlueprintBaseName._1;

/// <summary>
/// Business logic for processing <see cref="GreetingMessage"/> messages
/// </summary>
public class GreetingMessageHandler : IMessageHandler<GreetingMessage>
{
    public ILambdaContext _context;

    /// <summary>
    /// Constructor that resolves the <see cref="ILambdaContext"/> that was 
    /// registered in the DI by the framework
    /// </summary>
    /// <param name="context">Lambda execution context</param>
    public GreetingMessageHandler(ILambdaContext context)
    {
        _context = context;
    }
    /// <summary>
    /// This handler will be invoked once for each <see cref="GreetingMessage"/> that is received
    /// </summary>
    /// <param name="messageEnvelope">Envelope that wraps the actual message with metadata used by the framework</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>The appropriate <see cref="MessageProcessStatus"> based on whether the message was processed successfully</returns>
    public Task<MessageProcessStatus> HandleAsync(MessageEnvelope<GreetingMessage> messageEnvelope, CancellationToken token = default)
    {
        // The outer envelope contains metadata, and its Message property contains the actual message content
        var greetingMessage = messageEnvelope.Message;

        if (string.IsNullOrEmpty(greetingMessage.Greeting) || string.IsNullOrEmpty(greetingMessage.SenderName))
        {
            _context.Logger.LogError($"Received a message that was missing the {nameof(GreetingMessage.Greeting)} " +
                $"and/or the {nameof(GreetingMessage.SenderName)} from message {messageEnvelope.Id}");

            return Task.FromResult(MessageProcessStatus.Failed());
        }

        _context.Logger.LogInformation($"Received '{greetingMessage.Greeting}' from '{greetingMessage.SenderName}'");

        return Task.FromResult(MessageProcessStatus.Success());
    }
}