// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Processes.SQSEventSource;

/// <summary>
/// This class represents the input values from the user.
/// </summary>
internal class SQSEventSourceConfig
{
    /// <summary>
    /// The batch size to read and send to Lambda function. This is the upper bound of messages to read and send.
    /// SQS will return with less then batch size if there are not enough messages in the queue.
    /// </summary>
    public int? BatchSize { get; set; }

    /// <summary>
    /// If true the SQSEventSourceBackgroundService will skip deleting messages from the queue after the Lambda function returns.
    /// </summary>
    public bool? DisableMessageDelete { get; set; }

    /// <summary>
    /// The Lambda function to send the SQS messages to delete to.
    /// If not set the default function will be used.
    /// </summary>
    public string? FunctionName { get; set; }

    /// <summary>
    /// The endpoint where the emulated Lambda runtime API is running. The Lambda function identified by FunctionName must be listening for events from this endpoint.
    /// If not set the current Test Tool instance will be used assuming it is running a Lambda runtime api emulator.
    /// </summary>
    public string? LambdaRuntimeApi { get; set; }
    /// <summary>
    /// The AWS profile to use for credentials for fetching messages from the queue.
    /// </summary>
    public string? Profile { get; set; }

    /// <summary>
    /// The queue url where messages should be polled from.
    /// </summary>
    public string? QueueUrl { get; set; }

    /// <summary>
    /// The AWS region the queue is in.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// The visibility timeout used for messages read. This is the length the message will not be visible to be read
    /// again once it is returned in the receive call.
    /// </summary>
    public int? VisibilityTimeout { get; set; }
}
