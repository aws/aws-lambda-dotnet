// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Processes.SQSEventSource;

/// <summary>
/// Configuration for the <see cref="SQSEventSourceBackgroundService"/> service.
/// </summary>
public class SQSEventSourceBackgroundServiceConfig
{
    /// <summary>
    /// The batch size to read and send to Lambda function. This is the upper bound of messages to read and send.
    /// SQS will return with less than batch size if there are not enough messages in the queue.
    /// </summary>
    public required int BatchSize { get; init; } = SQSEventSourceProcess.DefaultBatchSize;

    /// <summary>
    /// If true the <see cref="SQSEventSourceBackgroundService"/> will skip deleting messages from the queue after the Lambda function returns.
    /// </summary>
    public required bool DisableMessageDelete { get; init; }

    /// <summary>
    /// The Lambda function to send the SQS messages to delete to.
    /// </summary>
    public required string FunctionName { get; init; }

    /// <summary>
    /// The endpoint where the emulated Lambda runtime API is running. The Lambda function identified by FunctionName must be listening for events from this endpoint.
    /// </summary>
    public required string LambdaRuntimeApi { get; init; }

    /// <summary>
    /// The SQS queue url to poll for messages.
    /// </summary>
    public required string QueueUrl { get; init; }

    /// <summary>
    /// The visibility timeout used for messages read. This is the length the message will not be visible to be read
    /// again once it is returned in the receive call.
    /// </summary>
    public required int VisibilityTimeout { get; init; } = SQSEventSourceProcess.DefaultVisiblityTimeout;
}
