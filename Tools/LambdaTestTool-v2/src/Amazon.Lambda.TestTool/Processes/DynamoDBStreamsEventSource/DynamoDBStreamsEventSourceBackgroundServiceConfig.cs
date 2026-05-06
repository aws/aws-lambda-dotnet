// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Processes.DynamoDBStreamsEventSource;

/// <summary>
/// Configuration for the <see cref="DynamoDBStreamsEventSourceBackgroundService"/> service.
/// </summary>
public class DynamoDBStreamsEventSourceBackgroundServiceConfig
{
    /// <summary>
    /// The batch size to read from the stream and send to the Lambda function.
    /// </summary>
    public required int BatchSize { get; init; } = DynamoDBStreamsEventSourceProcess.DefaultBatchSize;

    /// <summary>
    /// The Lambda function to send the DynamoDB stream records to.
    /// </summary>
    public required string FunctionName { get; init; }

    /// <summary>
    /// The endpoint where the emulated Lambda runtime API is running.
    /// </summary>
    public required string LambdaRuntimeApi { get; init; }

    /// <summary>
    /// The DynamoDB table name to read streams from.
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// The shard iterator type to use when reading from the stream.
    /// </summary>
    public required string ShardIteratorType { get; init; } = "LATEST";

    /// <summary>
    /// The polling interval in milliseconds between stream reads when no records are found.
    /// </summary>
    public required int PollingIntervalMs { get; init; } = DynamoDBStreamsEventSourceProcess.DefaultPollingIntervalMs;
}
