using Amazon.Lambda.TestTool.Commands;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services.IO;
using Amazon.Lambda.TestTool.Services;
using Amazon.SQS;
using Moq;
using Spectre.Console.Cli;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SQS.Model;
using Amazon.Lambda.TestTool.Tests.Common;

namespace Amazon.Lambda.TestTool.IntegrationTests;

public class SQSEventSourceTests : BaseApiGatewayTest
{
    public SQSEventSourceTests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    [Fact]
    public async Task ProcessSingleMessage()
    {
        var sqsClient = new AmazonSQSClient();
        var queueName = nameof(ProcessSingleMessage) + DateTime.Now.Ticks;
        var queueUrl = (await sqsClient.CreateQueueAsync(queueName)).QueueUrl;
        var consoleError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);

            var lambdaPort = GetFreePort();
            var testToolTask = StartTestToolProcessAsync(lambdaPort, $"QueueUrl={queueUrl},FunctionName=SQSProcessor", CancellationTokenSource);

            var listOfProcessedMessages = new List<SQSEvent.SQSMessage>();
            var handler = (SQSEvent evnt, ILambdaContext context) =>
            {
                foreach(var message in evnt.Records)
                {
                    listOfProcessedMessages.Add(message);
                }
            };

            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/SQSProcessor")
                .Build()
                .RunAsync(CancellationTokenSource.Token);

            await sqsClient.SendMessageAsync(queueUrl, "TheBody");

            var startTime = DateTime.UtcNow;
            while (listOfProcessedMessages.Count == 0 && DateTime.UtcNow < startTime.AddMinutes(2))
            {
                await Task.Delay(500);
            }

            Assert.Single(listOfProcessedMessages);
            Assert.Equal("TheBody", listOfProcessedMessages[0].Body);
            Assert.Equal(0, await GetNumberOfMessagesInQueueAsync(sqsClient, queueUrl));
        }
        finally
        {
            await CancellationTokenSource.CancelAsync();
            await sqsClient.DeleteQueueAsync(queueUrl);
            Console.SetError(consoleError);
        }
    }

    [Fact]
    public async Task SQSEventSourceComesFromEnvironmentVariable()
    {
        var sqsClient = new AmazonSQSClient();
        var queueName = nameof(ProcessSingleMessage) + DateTime.Now.Ticks;
        var queueUrl = (await sqsClient.CreateQueueAsync(queueName)).QueueUrl;
        var consoleError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);

            var lambdaPort = GetFreePort();
            var testToolTask = StartTestToolProcessAsync(lambdaPort, $"env:SQS_CONFIG&QueueUrl={queueUrl},FunctionName=SQSProcessor", CancellationTokenSource);

            var listOfProcessedMessages = new List<SQSEvent.SQSMessage>();
            var handler = (SQSEvent evnt, ILambdaContext context) =>
            {
                foreach (var message in evnt.Records)
                {
                    listOfProcessedMessages.Add(message);
                }
            };

            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/SQSProcessor")
                .Build()
                .RunAsync(CancellationTokenSource.Token);

            await sqsClient.SendMessageAsync(queueUrl, "TheBody");

            var startTime = DateTime.UtcNow;
            while (listOfProcessedMessages.Count == 0 && DateTime.UtcNow < startTime.AddMinutes(2))
            {
                await Task.Delay(500);
            }

            Assert.Single(listOfProcessedMessages);
            Assert.Equal("TheBody", listOfProcessedMessages[0].Body);
            Assert.Equal(0, await GetNumberOfMessagesInQueueAsync(sqsClient, queueUrl));
        }
        finally
        {
            await CancellationTokenSource.CancelAsync();
            await sqsClient.DeleteQueueAsync(queueUrl);
            Console.SetError(consoleError);
        }
    }

    [Fact]
    public async Task ProcessMessagesFromMultipleEventSources()
    {
        var sqsClient = new AmazonSQSClient();
        var queueName1 = nameof(ProcessMessagesFromMultipleEventSources) + "-1-" + DateTime.Now.Ticks;
        var queueUrl1 = (await sqsClient.CreateQueueAsync(queueName1)).QueueUrl;

        var queueName2 = nameof(ProcessMessagesFromMultipleEventSources) + "-2-" + DateTime.Now.Ticks;
        var queueUrl2 = (await sqsClient.CreateQueueAsync(queueName2)).QueueUrl;

        var consoleError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);

            var sqsEventSourceConfig = """
[
    {
        "QueueUrl" : "queueUrl1",
        "FunctionName" : "SQSProcessor"
    },
    {
        "QueueUrl" : "queueUrl2",
        "FunctionName" : "SQSProcessor"
    }
]
""".Replace("queueUrl1", queueUrl1).Replace("queueUrl2", queueUrl2);

            var lambdaPort = GetFreePort();
            var testToolTask = StartTestToolProcessAsync(lambdaPort, sqsEventSourceConfig, CancellationTokenSource);

            var listOfProcessedMessages = new List<SQSEvent.SQSMessage>();
            var handler = (SQSEvent evnt, ILambdaContext context) =>
            {
                foreach (var message in evnt.Records)
                {
                    listOfProcessedMessages.Add(message);
                }
            };

            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/SQSProcessor")
                .Build()
                .RunAsync(CancellationTokenSource.Token);

            await sqsClient.SendMessageAsync(queueUrl1, "MessageFromQueue1");
            await sqsClient.SendMessageAsync(queueUrl2, "MessageFromQueue2");

            var startTime = DateTime.UtcNow;
            while (listOfProcessedMessages.Count == 0 && DateTime.UtcNow < startTime.AddMinutes(2))
            {
                await Task.Delay(500);
            }

            Assert.Equal(2, listOfProcessedMessages.Count);
            Assert.NotEqual(listOfProcessedMessages[0].EventSourceArn, listOfProcessedMessages[1].EventSourceArn);
        }
        finally
        {
            await CancellationTokenSource.CancelAsync();
            await sqsClient.DeleteQueueAsync(queueUrl1);
            await sqsClient.DeleteQueueAsync(queueUrl2);
            Console.SetError(consoleError);
        }
    }

    [Fact]
    public async Task MessageNotDeleted()
    {
        var sqsClient = new AmazonSQSClient();
        var queueName = nameof(MessageNotDeleted) + DateTime.Now.Ticks;
        var queueUrl = (await sqsClient.CreateQueueAsync(queueName)).QueueUrl;
        var consoleError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);

            var lambdaPort = GetFreePort();
            var testToolTask = StartTestToolProcessAsync(lambdaPort, $"QueueUrl={queueUrl},FunctionName=SQSProcessor,DisableMessageDelete=true", CancellationTokenSource);

            var listOfProcessedMessages = new List<SQSEvent.SQSMessage>();
            var handler = (SQSEvent evnt, ILambdaContext context) =>
            {
                foreach (var message in evnt.Records)
                {
                    listOfProcessedMessages.Add(message);
                }
            };

            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/SQSProcessor")
                .Build()
                .RunAsync(CancellationTokenSource.Token);

            await sqsClient.SendMessageAsync(queueUrl, "TheBody");

            var startTime = DateTime.UtcNow;
            while (listOfProcessedMessages.Count == 0 && DateTime.UtcNow < startTime.AddMinutes(2))
            {
                await Task.Delay(500);
            }

            Assert.Single(listOfProcessedMessages);
            Assert.Equal("TheBody", listOfProcessedMessages[0].Body);
            Assert.Equal(1, await GetNumberOfMessagesInQueueAsync(sqsClient, queueUrl));
        }
        finally
        {
            await CancellationTokenSource.CancelAsync();
            await sqsClient.DeleteQueueAsync(queueUrl);
            Console.SetError(consoleError);
        }
    }

    [Fact]
    public async Task LambdaThrowsErrorAndMessageNotDeleted()
    {
        var sqsClient = new AmazonSQSClient();
        var queueName = nameof(LambdaThrowsErrorAndMessageNotDeleted) + DateTime.Now.Ticks;
        var queueUrl = (await sqsClient.CreateQueueAsync(queueName)).QueueUrl;
        var consoleError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);

            var lambdaPort = GetFreePort();
            var testToolTask = StartTestToolProcessAsync(lambdaPort, $"QueueUrl={queueUrl},FunctionName=SQSProcessor", CancellationTokenSource);

            var listOfProcessedMessages = new List<SQSEvent.SQSMessage>();
            var handler = (SQSEvent evnt, ILambdaContext context) =>
            {
                foreach (var message in evnt.Records)
                {
                    listOfProcessedMessages.Add(message);
                }

                throw new Exception("Failed to process message");
            };

            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/SQSProcessor")
                .Build()
                .RunAsync(CancellationTokenSource.Token);

            await sqsClient.SendMessageAsync(queueUrl, "TheBody");

            var startTime = DateTime.UtcNow;
            while (listOfProcessedMessages.Count == 0 && DateTime.UtcNow < startTime.AddMinutes(2))
            {
                await Task.Delay(500);
            }

            Assert.Single(listOfProcessedMessages);
            Assert.Equal("TheBody", listOfProcessedMessages[0].Body);
            Assert.Equal(1, await GetNumberOfMessagesInQueueAsync(sqsClient, queueUrl));
        }
        finally
        {
            await CancellationTokenSource.CancelAsync();
            await sqsClient.DeleteQueueAsync(queueUrl);
            Console.SetError(consoleError);
        }
    }

    [Fact]
    public async Task PartialFailureResponse()
    {
        var sqsClient = new AmazonSQSClient();
        var queueName = nameof(PartialFailureResponse) + DateTime.Now.Ticks;
        var queueUrl = (await sqsClient.CreateQueueAsync(queueName)).QueueUrl;
        var consoleError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);
            await sqsClient.SendMessageAsync(queueUrl, "Message1");

            var lambdaPort = GetFreePort();

            // Lower VisibilityTimeout to speed up receiving the message at the end to prove the message wasn't deleted.
            var testToolTask = StartTestToolProcessAsync(lambdaPort, $"QueueUrl={queueUrl},FunctionName=SQSProcessor,VisibilityTimeout=3", CancellationTokenSource);

            var listOfProcessedMessages = new List<SQSEvent.SQSMessage>();
            var handler = (SQSEvent evnt, ILambdaContext context) =>
            {
                foreach (var message in evnt.Records)
                {
                    listOfProcessedMessages.Add(message);
                }

                var sqsResponse = new SQSBatchResponse();
                sqsResponse.BatchItemFailures = new List<SQSBatchResponse.BatchItemFailure>
                {
                    new SQSBatchResponse.BatchItemFailure
                    {
                        ItemIdentifier = evnt.Records[0].MessageId
                    }
                };

                return sqsResponse;
            };

            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/SQSProcessor")
                .Build()
                .RunAsync(CancellationTokenSource.Token);

            await sqsClient.SendMessageAsync(queueUrl, "TheBody");

            var startTime = DateTime.UtcNow;
            while (listOfProcessedMessages.Count == 0 && DateTime.UtcNow < startTime.AddMinutes(2))
            {
                await Task.Delay(500);
            }

            // Since the message was never deleted by the event source it should still be eligibl for reading.
            var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest { QueueUrl = queueUrl, WaitTimeSeconds = 20 });
            Assert.Single(response.Messages);
        }
        finally
        {
            await CancellationTokenSource.CancelAsync();
            await sqsClient.DeleteQueueAsync(queueUrl);
            Console.SetError(consoleError);
        }
    }


    private async Task<int> GetNumberOfMessagesInQueueAsync(IAmazonSQS sqsClient, string queueUrl)
    {
        // Add a delay to handle SQS eventual consistency.
        await Task.Delay(5000); 
        var response = await sqsClient.GetQueueAttributesAsync(queueUrl, new List<string> { "All" });
        return response.ApproximateNumberOfMessages + response.ApproximateNumberOfMessagesNotVisible;
    }

    private async Task StartTestToolProcessAsync(int lambdaPort, string sqsEventSourceConfig, CancellationTokenSource cancellationTokenSource)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        var environmentVariables = new Dictionary<string, string> { };

        if (sqsEventSourceConfig.StartsWith(Constants.ArgumentEnvironmentVariablePrefix))
        {
            var tokens = sqsEventSourceConfig.Split('&');
            if (tokens.Length == 2)
            {
                sqsEventSourceConfig = tokens[0];
                var envName = tokens[0].Replace(Constants.ArgumentEnvironmentVariablePrefix, "");
                var envValue = tokens[1];
                environmentVariables[envName] = envValue;
            }
        }

        var settings = new RunCommandSettings
        {
            LambdaEmulatorPort = lambdaPort,
            NoLaunchWindow = true,
            SQSEventSourceConfig = sqsEventSourceConfig
        };

        var command = new RunCommand(MockInteractiveService.Object, new TestEnvironmentManager(environmentVariables));
        var context = new CommandContext(new List<string>(), MockRemainingArgs.Object, "run", null);
        _ = command.ExecuteAsync(context, settings, cancellationTokenSource);

        await Task.Delay(2000, cancellationTokenSource.Token);
    }
}
