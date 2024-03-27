using Amazon.Lambda.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace BlueprintBaseName._1;

[LambdaStartup]
public class Startup
{
    /// <summary>
    /// Services for Lambda functions can be registered in the services dependency injection container in this method. 
    ///
    /// The services can be injected into the Lambda function through the containing type's constructor or as a
    /// parameter in the Lambda function using the FromService attribute. Services injected for the constructor have
    /// the lifetime of the Lambda compute container. Services injected as parameters are created within the scope
    /// of the function invocation.
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        // Here we'll configure the AWS Message Processing Framework for .NET.
        services.AddAWSMessageBus(builder =>
        {
            // Register that you'll publish messages of type "GreetingMessage" to the specified queue URL.
            //  1. When deployed, the QUEUE_URL variable will be set to the queue that is defined in serverless.template
            //  2. When testing locally using the Mock Lambda Test Tool, the queue URL is configured in launchSettings.json
            builder.AddSQSPublisher<GreetingMessage>(Environment.GetEnvironmentVariable("QUEUE_URL"));

            // You can register additional message types and destinations here as well.

            // Register that you'll process messages in a Lambda function, and that messages
            // of the GreetingMessage type will be processed by GreetingMessageHandler
            builder.AddLambdaMessageProcessor();
            builder.AddMessageHandler<GreetingMessageHandler, GreetingMessage>();

            // You can register additional message type and handler mappings here as well.
        });
    }
}

