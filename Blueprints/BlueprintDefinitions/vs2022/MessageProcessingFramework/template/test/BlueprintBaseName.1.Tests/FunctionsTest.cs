using Amazon.Lambda.TestUtilities;
using AWS.Messaging;
using Xunit;

namespace BlueprintBaseName._1.Tests;

public class FunctionsTest
{
    /// <summary>
    /// Asserts that the GreetingMessage handler returns 
    /// successfully when handling a valid message
    /// </summary>
    [Fact]
    public async Task Handler_ValidMessage_Success()
    {
        var handler = new GreetingMessageHandler(new TestLambdaContext());

        var envelope = new MessageEnvelope<GreetingMessage>()
        {
            Message = new GreetingMessage
            {
                Greeting = "Hello",
                SenderName = "TestUser"
            }
        };
        
        var result = await handler.HandleAsync(envelope);

        Assert.Equal(MessageProcessStatus.Success(), result);
    }

    /// <summary>
    /// Asserts that the GreetingMessage handler returns
    /// a failure status when handling an invalid envelope
    /// </summary>
    [Fact]
    public async Task Hander_InvalidMesssage_Failed()
    {
        var handler = new GreetingMessageHandler(new TestLambdaContext());

        var envelope = new MessageEnvelope<GreetingMessage>()
        {
            Message = new GreetingMessage
            {
                Greeting = "Hello" // SenderName is required too
            }
        };

        var result = await handler.HandleAsync(envelope);

        Assert.Equal(MessageProcessStatus.Failed(), result);
    }
}
