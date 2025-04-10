// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Model;
using Amazon.Lambda.SQSEvents;
using Amazon.Runtime;
using Amazon.SQS.Model;
using Amazon.SQS;
using System.Text.Json;
using Amazon.Lambda.TestTool.Services;

namespace Amazon.Lambda.TestTool.Processes.SQSEventSource;

/// <summary>
/// IHostedService that will run continually polling the SQS queue for messages and invoking the connected
/// Lambda function with the polled messages.
/// </summary>
public class SQSEventSourceBackgroundService : BackgroundService
{
    private static readonly List<string> DefaultAttributesToReceive = new List<string> { "All" };
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<SQSEventSourceProcess> _logger;
    private readonly IAmazonSQS _sqsClient;
    private readonly ILambdaClient _lambdaClient;
    private readonly SQSEventSourceBackgroundServiceConfig _config;

    /// <summary>
    /// Constructs instance of <see cref="SQSEventSourceBackgroundService"/>.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="sqsClient">The SQS client used to poll messages from a queue.</param>
    /// <param name="config">The config of the service</param>
    /// <param name="lambdaClient">The Lambda client that can use a different endpoint for each invoke request.</param>
    public SQSEventSourceBackgroundService(ILogger<SQSEventSourceProcess> logger, IAmazonSQS sqsClient, SQSEventSourceBackgroundServiceConfig config, ILambdaClient lambdaClient)
    {
        _logger = logger;
        _sqsClient = sqsClient;
        _config = config;
        _lambdaClient = lambdaClient;
    }

    private async Task<string> GetQueueArn(CancellationToken stoppingToken)
    {
        var response = await _sqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = _config.QueueUrl,
            AttributeNames = new List<string> { "QueueArn" }
        }, stoppingToken);

        return response.QueueARN;
    }

    /// <summary>
    /// Execute the SQSEventSourceBackgroundService.
    /// </summary>
    /// <param name="stoppingToken">CancellationToken used to end the service.</param>
    /// <returns>Task for the background service.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The queue arn is needed for creating the Lambda event.
        var queueArn = await GetQueueArn(stoppingToken);
        _logger.LogInformation("Starting polling for messages on SQS queue: {queueArn}", queueArn);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Polling {queueUrl} for messages", _config.QueueUrl);
                // Read a message from the queue using the ExternalCommands console application.
                var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = _config.QueueUrl,
                    WaitTimeSeconds = 20,
                    MessageAttributeNames = DefaultAttributesToReceive,
                    MessageSystemAttributeNames = DefaultAttributesToReceive,
                    MaxNumberOfMessages = _config.BatchSize,
                    VisibilityTimeout = _config.VisibilityTimeout,
                }, stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                if (response.Messages == null || response.Messages.Count == 0)
                {
                    _logger.LogDebug("No messages received from while polling SQS");
                    // Since there are no messages, sleep a bit to wait for messages to come.
                    await Task.Delay(200);
                    continue;
                }

                var lambdaPayload = new
                {
                    Records = ConvertToLambdaMessages(response.Messages, _sqsClient.Config.RegionEndpoint.SystemName, queueArn)
                };

                var invokeRequest = new InvokeRequest
                {
                    InvocationType = InvocationType.RequestResponse,
                    FunctionName = _config.FunctionName,
                    Payload = JsonSerializer.Serialize(lambdaPayload, _jsonOptions)
                };

                _logger.LogInformation("Invoking Lambda function {functionName} function with {messageCount} messages", _config.FunctionName, lambdaPayload.Records.Count);
                var lambdaResponse = await _lambdaClient.InvokeAsync(invokeRequest, _config.LambdaRuntimeApi);

                if (lambdaResponse.FunctionError != null)
                {
                    _logger.LogError("Invoking Lambda {function} function with {messageCount} failed with error {errorMessage}", _config.FunctionName, response.Messages.Count, lambdaResponse.FunctionError);
                    continue;
                }

                if (!_config.DisableMessageDelete)
                {
                    List<Message> messagesToDelete;
                    if (lambdaResponse.Payload != null && lambdaResponse.Payload.Length > 0)
                    {
                        var partialResponse = JsonSerializer.Deserialize<SQSBatchResponse>(lambdaResponse.Payload);
                        if (partialResponse == null)
                        {
                            lambdaResponse.Payload.Position = 0;
                            using var reader = new StreamReader(lambdaResponse.Payload);
                            var payloadString = reader.ReadToEnd();
                            _logger.LogError("Failed to deserialize response from Lambda function into SQSBatchResponse. Response payload:\n{payload}", payloadString);
                            continue;
                        }

                        if (partialResponse.BatchItemFailures == null || partialResponse.BatchItemFailures.Count == 0)
                        {
                            _logger.LogDebug("Partial SQS response received with no failures");
                            messagesToDelete = response.Messages;
                        }
                        else
                        {
                            _logger.LogDebug("Partial SQS response received with {count} failures", partialResponse.BatchItemFailures.Count);
                            messagesToDelete = new List<Message>();
                            foreach (var message in response.Messages)
                            {
                                if (!partialResponse.BatchItemFailures.Any(x => string.Equals(x.ItemIdentifier, message.MessageId)))
                                {
                                    messagesToDelete.Add(message);
                                }
                            }
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No partial response received. All messages eligible for deletion");
                        messagesToDelete = response.Messages;
                    }

                    if (messagesToDelete.Count > 0)
                    {
                        var deleteRequest = new DeleteMessageBatchRequest
                        {
                            QueueUrl = _config.QueueUrl,
                            Entries = messagesToDelete.Select(m => new DeleteMessageBatchRequestEntry { Id = m.MessageId, ReceiptHandle = m.ReceiptHandle }).ToList()
                        };

                        _logger.LogDebug("Deleting {messageCount} messages from queue", deleteRequest.Entries.Count);
                        await _sqsClient.DeleteMessageBatchAsync(deleteRequest, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Exception occurred in SQS poller for {queueUrl}: {message}", _config.QueueUrl, e.Message);

                // Add a delay before restarting loop in case the exception was a transient error that needs a little time to reset.
                await Task.Delay(3000);
            }
        }
    }

    /// <summary>
    /// Convert from the SDK's list of messages to the Lambda event's SQS message type.
    /// </summary>
    /// <param name="messages">List of messages using the SDK's .NET type</param>
    /// <param name="awsRegion">The aws region the messages came from.</param>
    /// <param name="queueArn">The SQS queue arn the messages came from.</param>
    /// <returns>List of messages using the Lambda event's .NET type.</returns>
    internal static List<SQSEvent.SQSMessage> ConvertToLambdaMessages(List<Message> messages, string awsRegion, string queueArn)
    {
        return messages.Select(m => ConvertToLambdaMessage(m, awsRegion, queueArn)).ToList();
    }

    /// <summary>
    /// Convert from the SDK's SQS message to the Lambda event's SQS message type.
    /// </summary>
    /// <param name="message">Message using the SDK's .NET type</param>
    /// <param name="awsRegion">The aws region the message came from.</param>
    /// <param name="queueArn">The SQS queue arn the message came from.</param>
    /// <returns>Messages using the Lambda event's .NET type.</returns>
    internal static SQSEvent.SQSMessage ConvertToLambdaMessage(Message message, string awsRegion, string queueArn)
    {
        var lambdaMessage = new SQSEvent.SQSMessage
        {
            AwsRegion = awsRegion,
            Body = message.Body,
            EventSource = "aws:sqs",
            EventSourceArn = queueArn,
            Md5OfBody = message.MD5OfBody,
            Md5OfMessageAttributes = message.MD5OfMessageAttributes,
            MessageId = message.MessageId,
            ReceiptHandle = message.ReceiptHandle,
        };

        if (message.MessageAttributes != null && message.MessageAttributes.Count > 0)
        {
            lambdaMessage.MessageAttributes = new Dictionary<string, SQSEvent.MessageAttribute>();
            foreach (var kvp in message.MessageAttributes)
            {
                var lambdaAttribute = new SQSEvent.MessageAttribute
                {
                    DataType = kvp.Value.DataType,
                    StringValue = kvp.Value.StringValue,
                    BinaryValue = kvp.Value.BinaryValue
                };

                lambdaMessage.MessageAttributes.Add(kvp.Key, lambdaAttribute);
            }
        }

        if (message.Attributes != null && message.Attributes.Count > 0)
        {
            lambdaMessage.Attributes = message.Attributes;
        }

        return lambdaMessage;
    }
}
