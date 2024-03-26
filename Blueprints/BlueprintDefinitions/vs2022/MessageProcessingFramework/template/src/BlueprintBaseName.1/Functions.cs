using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using AWS.Messaging;
using AWS.Messaging.Lambda;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace BlueprintBaseName._1;

public class Functions
{
    /// <summary>
    /// This is the component of the AWS Message Processing Framework for .NET that dispatches
    /// messages in the <see cref="SQSEvent"/> received from the Lambda service to the correct 
    /// handler for each message based on its type
    /// </summary>
    private ILambdaMessaging _lambdaMessaging;

    /// <summary>
    /// This constructor is used by Lambda to construct the instance.
    /// </summary>
    /// <param name="lambdaMessaging">Framework component that processes messages in Lambda</param>
    public Functions(ILambdaMessaging lambdaMessaging)
    {
        _lambdaMessaging = lambdaMessaging;
    }

    /// <summary>
    /// Lambda function that publishes a message to SQS using the AWS Message Processing Framework for .NET
    /// </summary>
    /// <param name="publisher">Generic message publisher, can send messages or publish events 
    /// to any of the destinations that are configured in Startup.cs</param>
    /// <param name="message">Message that is received as an input to the Lambda function then forwarded to SQS</param>
    /// <param name="context">Lambda execution context</param>
    [LambdaFunction(Policies = "AmazonSQSFullAccess")]
    public async Task Sender([FromServices] IMessagePublisher publisher, GreetingMessage message, ILambdaContext context)
    {
        if (message == null)
        {
            return;
        }

        context.Logger.LogLine($"Received {message.Greeting} from {message.SenderName}, will send to SQS");

        // Publish the message to the queue configured in Startup.cs
        await publisher.PublishAsync(message);
    }

    /// <summary>
    /// Lambda function that handles messages sent to SQS using the AWS Message Processing Framework for .NET
    /// </summary>
    /// <param name="evnt">SQS event</param>
    /// <param name="context">Lambda execution context</param>
    [LambdaFunction(Policies = "AWSLambdaSQSQueueExecutionRole")]
    public async Task Handler(SQSEvent evnt, ILambdaContext context)
    {
        // Pass the SQSEvent into the framework
        await _lambdaMessaging.ProcessLambdaEventAsync(evnt, context);
    }
}
