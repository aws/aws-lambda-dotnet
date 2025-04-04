using Amazon.Lambda.TestTool.Commands;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.SQS;
using Spectre.Console.Cli;
using Xunit;
using Xunit.Abstractions;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SQS.Model;
using Amazon.Lambda.TestTool.Tests.Common;
using Amazon.Lambda.TestTool.Tests.Common.Retries;
using Amazon.Lambda.TestTool.Services;
using Moq;

namespace Amazon.Lambda.TestTool.IntegrationTests;

[Collection("SQSEventSourceTests")]
public class SQSEventSourceTests : BaseApiGatewayTest
{
    public SQSEventSourceTests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    [RetryFact]
    public async Task ProcessSingleMessage()
    {
        var cancellationSource = new CancellationTokenSource();

        var sqsClient = new AmazonSQSClient();
        var queueName = nameof(ProcessSingleMessage) + DateTime.Now.Ticks;
        var queueUrl = (await sqsClient.CreateQueueAsync(queueName)).QueueUrl;
        await Task.Delay(2000);
        var consoleError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);

            var lambdaPort = GetFreePort();
            var testToolTask = StartTestToolProcessAsync(lambdaPort, $"QueueUrl={queueUrl},FunctionName=SQSProcessor", cancellationSource);

            var listOfProcessedMessages = new List<SQSEvent.SQSMessage>();
            var handler = (SQSEvent evnt, ILambdaContext context) =>
            {
                TestOutputHelper.WriteLine($"Lambda handler called with {evnt.Records.Count} messages");
                foreach (var message in evnt.Records)
                {
                    listOfProcessedMessages.Add(message);
                }
            };

            var lambdaTask = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/SQSProcessor")
                .Build()
                .RunAsync(cancellationSource.Token);

            await sqsClient.SendMessageAsync(queueUrl, "TheBody");

            var startTime = DateTime.UtcNow;
            while (listOfProcessedMessages.Count == 0 && DateTime.UtcNow < startTime.AddMinutes(2))
            {
                Assert.False(lambdaTask.IsFaulted, "Lambda function failed: " + lambdaTask.Exception?.ToString());
                await Task.Delay(500);
            }

            Assert.Single(listOfProcessedMessages);
            Assert.Equal("TheBody", listOfProcessedMessages[0].Body);
            Assert.Equal(0, await GetNumberOfMessagesInQueueAsync(sqsClient, queueUrl));
        }
        finally
        {
            _ = cancellationSource.CancelAsync();
            await sqsClient.DeleteQueueAsync(queueUrl);
            Console.SetError(consoleError);
        }
    }

    [RetryFact]
    public async Task SQSEventSourceComesFromEnvironmentVariable()
    {
        var cancellationSource = new CancellationTokenSource();

        var sqsClient = new AmazonSQSClient();
        var queueName = nameof(SQSEventSourceComesFromEnvironmentVariable) + DateTime.Now.Ticks;
        var queueUrl = (await sqsClient.CreateQueueAsync(queueName)).QueueUrl;
        await Task.Delay(2000);
        var consoleError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);

            var lambdaPort = GetFreePort();
            var testToolTask = StartTestToolProcessAsync(lambdaPort, $"env:SQS_CONFIG&QueueUrl={queueUrl},FunctionName=SQSProcessor", cancellationSource);

            var listOfProcessedMessages = new List<SQSEvent.SQSMessage>();
            var handler = (SQSEvent evnt, ILambdaContext context) =>
            {
                TestOutputHelper.WriteLine($"Lambda handler called with {evnt.Records.Count} messages");
                foreach (var message in evnt.Records)
                {
                    listOfProcessedMessages.Add(message);
                }
            };

            var lambdaTask = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/SQSProcessor")
                .Build()
                .RunAsync(cancellationSource.Token);

            await sqsClient.SendMessageAsync(queueUrl, "TheBody");

            var startTime = DateTime.UtcNow;
            while (listOfProcessedMessages.Count == 0 && DateTime.UtcNow < startTime.AddMinutes(2))
            {
                Assert.False(lambdaTask.IsFaulted, "Lambda function failed: " + lambdaTask.Exception?.ToString());
                await Task.Delay(500);
            }

            Assert.Single(listOfProcessedMessages);
            Assert.Equal("TheBody", listOfProcessedMessages[0].Body);
            Assert.Equal(0, await GetNumberOfMessagesInQueueAsync(sqsClient, queueUrl));
        }
        finally
        {
            _ = cancellationSource.CancelAsync();
            await sqsClient.DeleteQueueAsync(queueUrl);
            Console.SetError(consoleError);
        }
    }

    [RetryFact]
    public async Task ProcessMessagesFromMultipleEventSources()
    {
        var cancellationSource = new CancellationTokenSource();

        var sqsClient = new AmazonSQSClient();
        var queueName1 = nameof(ProcessMessagesFromMultipleEventSources) + "-1-" + DateTime.Now.Ticks;
        var queueUrl1 = (await sqsClient.CreateQueueAsync(queueName1)).QueueUrl;

        var queueName2 = nameof(ProcessMessagesFromMultipleEventSources) + "-2-" + DateTime.Now.Ticks;
        var queueUrl2 = (await sqsClient.CreateQueueAsync(queueName2)).QueueUrl;
        await Task.Delay(2000);

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
            var testToolTask = StartTestToolProcessAsync(lambdaPort, sqsEventSourceConfig, cancellationSource);

            var listOfProcessedMessages = new List<SQSEvent.SQSMessage>();
            var handler = (SQSEvent evnt, ILambdaContext context) =>
            {
                TestOutputHelper.WriteLine($"Lambda handler called with {evnt.Records.Count} messages");
                foreach (var message in evnt.Records)
                {
                    listOfProcessedMessages.Add(message);
                }
            };

            var lambdaTask = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/SQSProcessor")
                .Build()
                .RunAsync(cancellationSource.Token);

            await sqsClient.SendMessageAsync(queueUrl1, "MessageFromQueue1");
            await sqsClient.SendMessageAsync(queueUrl2, "MessageFromQueue2");

            var startTime = DateTime.UtcNow;
            while (listOfProcessedMessages.Count == 0 && DateTime.UtcNow < startTime.AddMinutes(2))
            {
                Assert.False(lambdaTask.IsFaulted, "Lambda function failed: " + lambdaTask.Exception?.ToString());
                await Task.Delay(500);
            }

            Assert.Equal(2, listOfProcessedMessages.Count);
            Assert.NotEqual(listOfProcessedMessages[0].EventSourceArn, listOfProcessedMessages[1].EventSourceArn);
        }
        finally
        {
            _ = cancellationSource.CancelAsync();
            await sqsClient.DeleteQueueAsync(queueUrl1);
            await sqsClient.DeleteQueueAsync(queueUrl2);
            Console.SetError(consoleError);
        }
    }

    [RetryFact]
    public async Task MessageNotDeleted()
    {
        var cancellationSource = new CancellationTokenSource();
        var sqsClient = new AmazonSQSClient();
        var queueName = nameof(MessageNotDeleted) + DateTime.Now.Ticks;
        var queueUrl = (await sqsClient.CreateQueueAsync(queueName)).QueueUrl;
        await Task.Delay(2000);
        var consoleError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);

            var lambdaPort = GetFreePort();
            var testToolTask = StartTestToolProcessAsync(lambdaPort, $"QueueUrl={queueUrl},FunctionName=SQSProcessor,DisableMessageDelete=true", cancellationSource);

            var listOfProcessedMessages = new List<SQSEvent.SQSMessage>();
            var handler = (SQSEvent evnt, ILambdaContext context) =>
            {
                TestOutputHelper.WriteLine($"Lambda handler called with {evnt.Records.Count} messages");
                foreach (var message in evnt.Records)
                {
                    listOfProcessedMessages.Add(message);
                }
            };

            var lambdaTask = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/SQSProcessor")
                .Build()
                .RunAsync(cancellationSource.Token);

            await sqsClient.SendMessageAsync(queueUrl, "TheBody");

            var startTime = DateTime.UtcNow;
            while (listOfProcessedMessages.Count == 0 && DateTime.UtcNow < startTime.AddMinutes(2))
            {
                Assert.False(lambdaTask.IsFaulted, "Lambda function failed: " + lambdaTask.Exception?.ToString());
                await Task.Delay(500);
            }

            Assert.Single(listOfProcessedMessages);
            Assert.Equal("TheBody", listOfProcessedMessages[0].Body);
            Assert.Equal(1, await GetNumberOfMessagesInQueueAsync(sqsClient, queueUrl));
        }
        finally
        {
            _ = cancellationSource.CancelAsync();
            await sqsClient.DeleteQueueAsync(queueUrl);
            Console.SetError(consoleError);
        }
    }

    [RetryFact]
    public async Task LambdaThrowsErrorAndMessageNotDeleted()
    {
        var cancellationSource = new CancellationTokenSource();
        var sqsClient = new AmazonSQSClient();
        var queueName = nameof(LambdaThrowsErrorAndMessageNotDeleted) + DateTime.Now.Ticks;
        var queueUrl = (await sqsClient.CreateQueueAsync(queueName)).QueueUrl;
        await Task.Delay(2000);
        var consoleError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);
            var lambdaPort = GetFreePort();
            var testToolTask = StartTestToolProcessAsync(lambdaPort, $"QueueUrl={queueUrl},FunctionName=SQSProcessor", cancellationSource);

            var listOfProcessedMessages = new List<SQSEvent.SQSMessage>();
            var handler = (SQSEvent evnt, ILambdaContext context) =>
            {
                TestOutputHelper.WriteLine($"Lambda handler called with {evnt.Records.Count} messages");
                foreach (var message in evnt.Records)
                {
                    listOfProcessedMessages.Add(message);
                }

                throw new Exception("Failed to process message");
            };

            var lambdaTask = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/SQSProcessor")
                .Build()
                .RunAsync(cancellationSource.Token);

            await sqsClient.SendMessageAsync(queueUrl, "TheBody");

            var startTime = DateTime.UtcNow;
            while (listOfProcessedMessages.Count == 0 && DateTime.UtcNow < startTime.AddMinutes(2))
            {
                Assert.False(lambdaTask.IsFaulted, "Lambda function failed: " + lambdaTask.Exception?.ToString());
                await Task.Delay(500);
            }

            Assert.Single(listOfProcessedMessages);
            Assert.Equal("TheBody", listOfProcessedMessages[0].Body);
            Assert.Equal(1, await GetNumberOfMessagesInQueueAsync(sqsClient, queueUrl));
        }
        finally
        {
            _ = cancellationSource.CancelAsync();
            await sqsClient.DeleteQueueAsync(queueUrl);
            Console.SetError(consoleError);
        }
    }

    [RetryFact]
    public async Task PartialFailureResponse()
    {
        var cancellationSource = new CancellationTokenSource();
        var sqsClient = new AmazonSQSClient();
        var queueName = nameof(PartialFailureResponse) + DateTime.Now.Ticks;
        var queueUrl = (await sqsClient.CreateQueueAsync(queueName)).QueueUrl;
        await Task.Delay(2000);
        var consoleError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);
            await sqsClient.SendMessageAsync(queueUrl, "Message1");

            var lambdaPort = GetFreePort();

            // Lower VisibilityTimeout to speed up receiving the message at the end to prove the message wasn't deleted.
            var testToolTask = StartTestToolProcessAsync(lambdaPort, $"QueueUrl={queueUrl},FunctionName=SQSProcessor,VisibilityTimeout=3", cancellationSource);

            var listOfProcessedMessages = new List<SQSEvent.SQSMessage>();
            var handler = (SQSEvent evnt, ILambdaContext context) =>
            {
                TestOutputHelper.WriteLine($"Lambda handler called with {evnt.Records.Count} messages");
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

            var lambdaTask = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/SQSProcessor")
                .Build()
                .RunAsync(cancellationSource.Token);

            await sqsClient.SendMessageAsync(queueUrl, "TheBody");

            var startTime = DateTime.UtcNow;
            while (listOfProcessedMessages.Count == 0 && DateTime.UtcNow < startTime.AddMinutes(2))
            {
                Assert.False(lambdaTask.IsFaulted, "Lambda function failed: " + lambdaTask.Exception?.ToString());
                await Task.Delay(500);
            }

            // Wait for message to be deleted.
            await Task.Delay(2000);

            // Since the message was never deleted by the event source it should still be eligibl for reading.
            var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest { QueueUrl = queueUrl, WaitTimeSeconds = 20 });
            Assert.Single(response.Messages);
        }
        finally
        {
            _ = cancellationSource.CancelAsync();
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

    // Do not use async/await so we can be sure to hand back the Task that created by RunCommand back to caller.
    private Task StartTestToolProcessAsync(int lambdaPort, string sqsEventSourceConfig, CancellationTokenSource cancellationTokenSource)
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
        Task testToolTask = command.ExecuteAsync(context, settings, cancellationTokenSource);

        var timeout = DateTime.UtcNow.AddMinutes(1);
        while (DateTime.UtcNow < timeout && command.LambdRuntimeApiTask == null)
        {
            Thread.Sleep(100);
        }

        Thread.Sleep(2000);

        Assert.NotNull(command.LambdRuntimeApiTask);
        Assert.False(command.LambdRuntimeApiTask.IsFaulted, "Task to start Lambda Runtime API failed: " + command.LambdRuntimeApiTask.Exception?.ToString());

        using var httpClient = new HttpClient();

        var healthCheckUrl = $"http://localhost:{lambdaPort}/lambda-runtime-api/healthcheck";
        TestOutputHelper.WriteLine($"Attempting health check url for Lambda runtime api: {healthCheckUrl}");

        try
        {
            var health = httpClient.GetStringAsync(healthCheckUrl).GetAwaiter().GetResult();
            TestOutputHelper.WriteLine($"Get successful health check: {health}");
        }
        catch(Exception ex)
        {
            Assert.Fail($"Failed to make healthcheck: {ex}");
        }

        Thread.Sleep(2000);
        return testToolTask;
    }
}
