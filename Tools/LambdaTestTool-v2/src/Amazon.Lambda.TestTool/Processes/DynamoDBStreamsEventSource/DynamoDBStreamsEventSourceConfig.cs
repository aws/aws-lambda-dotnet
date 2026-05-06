// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Processes.DynamoDBStreamsEventSource;

/// <summary>
/// This class represents the input values from the user for DynamoDB Streams event source configuration.
/// </summary>
internal class DynamoDBStreamsEventSourceConfig
{
    /// <summary>
    /// The batch size to read from the stream and send to the Lambda function.
    /// </summary>
    public int? BatchSize { get; set; }

    /// <summary>
    /// The Lambda function to send the DynamoDB stream records to.
    /// If not set the default function will be used.
    /// </summary>
    public string? FunctionName { get; set; }

    /// <summary>
    /// The endpoint where the emulated Lambda runtime API is running.
    /// If not set the current Test Tool instance will be used.
    /// </summary>
    public string? LambdaRuntimeApi { get; set; }

    /// <summary>
    /// The AWS profile to use for credentials.
    /// </summary>
    public string? Profile { get; set; }

    /// <summary>
    /// The AWS region the DynamoDB table is in.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// The DynamoDB table name to read streams from.
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// The shard iterator type to use when reading from the stream.
    /// Valid values: LATEST, TRIM_HORIZON. Default is TRIM_HORIZON.
    /// </summary>
    public string? ShardIteratorType { get; set; }

    /// <summary>
    /// The polling interval in milliseconds between stream reads when no records are found.
    /// Default is 1000.
    /// </summary>
    public int? PollingIntervalMs { get; set; }
}
