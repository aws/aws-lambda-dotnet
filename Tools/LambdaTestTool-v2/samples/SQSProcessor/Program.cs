using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;

// Processes a batch of SQS messages, logging each message body.
// Test it either with the built-in "sqs.json" sample event from the web UI,
// or by wiring a real queue with --sqs-eventsource-config (see this sample's README).
var handler = (SQSEvent evnt, ILambdaContext context) =>
{
    foreach (var message in evnt.Records)
    {
        context.Logger.LogLine($"Processing message {message.MessageId}: {message.Body}");
    }

    context.Logger.LogLine($"Processed {evnt.Records.Count} message(s).");
};

await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
    .Build()
    .RunAsync();
