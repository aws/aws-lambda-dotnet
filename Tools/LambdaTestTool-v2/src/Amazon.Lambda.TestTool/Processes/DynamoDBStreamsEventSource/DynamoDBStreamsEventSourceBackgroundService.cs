// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Services;
using Amazon.Runtime;
using System.Text.Json;

namespace Amazon.Lambda.TestTool.Processes.DynamoDBStreamsEventSource;

/// <summary>
/// IHostedService that will run continually polling a DynamoDB Stream for records and invoking the connected
/// Lambda function with the polled records.
/// </summary>
public class DynamoDBStreamsEventSourceBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<DynamoDBStreamsEventSourceProcess> _logger;
    private readonly IAmazonDynamoDBStreams _streamsClient;
    private readonly ILambdaClient _lambdaClient;
    private readonly DynamoDBStreamsEventSourceBackgroundServiceConfig _config;

    /// <summary>
    /// Constructs instance of <see cref="DynamoDBStreamsEventSourceBackgroundService"/>.
    /// </summary>
    public DynamoDBStreamsEventSourceBackgroundService(
        ILogger<DynamoDBStreamsEventSourceProcess> logger,
        IAmazonDynamoDBStreams streamsClient,
        DynamoDBStreamsEventSourceBackgroundServiceConfig config,
        ILambdaClient lambdaClient)
    {
        _logger = logger;
        _streamsClient = streamsClient;
        _config = config;
        _lambdaClient = lambdaClient;
    }

    /// <summary>
    /// Execute the DynamoDBStreamsEventSourceBackgroundService.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting DynamoDB Streams poller for table: {tableName}", _config.TableName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var streamArn = await GetStreamArnForTable(stoppingToken);
                if (streamArn == null)
                {
                    _logger.LogWarning("No stream found for table {tableName}. Retrying in 5 seconds.", _config.TableName);
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                await PollStream(streamArn, stoppingToken);
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
                _logger.LogWarning(e, "Exception occurred in DynamoDB Streams poller for {tableName}: {message}", _config.TableName, e.Message);
                await Task.Delay(3000, stoppingToken);
            }
        }
    }

    private async Task<string?> GetStreamArnForTable(CancellationToken stoppingToken)
    {
        var response = await _streamsClient.ListStreamsAsync(new ListStreamsRequest
        {
            TableName = _config.TableName
        }, stoppingToken);

        // Use the first active stream for the table
        return response.Streams.FirstOrDefault()?.StreamArn;
    }

    private async Task PollStream(string streamArn, CancellationToken stoppingToken)
    {
        var shardIterators = await GetShardIterators(streamArn, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var hasRecords = false;

            for (int i = 0; i < shardIterators.Count; i++)
            {
                if (stoppingToken.IsCancellationRequested)
                    return;

                var iterator = shardIterators[i];
                if (iterator == null)
                    continue;

                var getRecordsResponse = await _streamsClient.GetRecordsAsync(new GetRecordsRequest
                {
                    ShardIterator = iterator,
                    Limit = _config.BatchSize
                }, stoppingToken);

                shardIterators[i] = getRecordsResponse.NextShardIterator;

                if (getRecordsResponse.Records.Count == 0)
                    continue;

                hasRecords = true;
                var lambdaRecords = ConvertToLambdaRecords(getRecordsResponse.Records, streamArn);

                var lambdaPayload = new DynamoDBEvent
                {
                    Records = lambdaRecords
                };

                var invokeRequest = new InvokeRequest
                {
                    InvocationType = InvocationType.RequestResponse,
                    FunctionName = _config.FunctionName,
                    Payload = JsonSerializer.Serialize(lambdaPayload, _jsonOptions)
                };

                _logger.LogInformation("Invoking Lambda function {functionName} with {recordCount} DynamoDB stream records",
                    _config.FunctionName, lambdaRecords.Count);

                var lambdaResponse = await _lambdaClient.InvokeAsync(invokeRequest, _config.LambdaRuntimeApi);

                if (lambdaResponse.FunctionError != null)
                {
                    _logger.LogError("Invoking Lambda {function} with {recordCount} DynamoDB stream records failed with error {errorMessage}",
                        _config.FunctionName, lambdaRecords.Count, lambdaResponse.FunctionError);
                }
            }

            // Check for new shards periodically
            if (shardIterators.All(s => s == null))
            {
                shardIterators = await GetShardIterators(streamArn, stoppingToken);
                if (shardIterators.Count == 0)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            else if (!hasRecords)
            {
                // No records found, wait before polling again
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task<List<string?>> GetShardIterators(string streamArn, CancellationToken stoppingToken)
    {
        var describeResponse = await _streamsClient.DescribeStreamAsync(new DescribeStreamRequest
        {
            StreamArn = streamArn
        }, stoppingToken);

        var iterators = new List<string?>();

        foreach (var shard in describeResponse.StreamDescription.Shards)
        {
            var iteratorResponse = await _streamsClient.GetShardIteratorAsync(new GetShardIteratorRequest
            {
                StreamArn = streamArn,
                ShardId = shard.ShardId,
                ShardIteratorType = ShardIteratorType.LATEST
            }, stoppingToken);

            iterators.Add(iteratorResponse.ShardIterator);
        }

        return iterators;
    }

    /// <summary>
    /// Convert from the SDK's DynamoDB Streams records to the Lambda event's DynamoDB record type.
    /// </summary>
    internal static IList<DynamoDBEvent.DynamodbStreamRecord> ConvertToLambdaRecords(List<Record> records, string streamArn)
    {
        return records.Select(r => ConvertToLambdaRecord(r, streamArn)).ToList();
    }

    /// <summary>
    /// Convert a single SDK stream record to the Lambda event record type.
    /// </summary>
    internal static DynamoDBEvent.DynamodbStreamRecord ConvertToLambdaRecord(Record record, string streamArn)
    {
        var lambdaRecord = new DynamoDBEvent.DynamodbStreamRecord
        {
            EventID = record.EventID,
            EventName = record.EventName?.Value,
            EventSource = "aws:dynamodb",
            EventSourceArn = streamArn,
            EventVersion = record.EventVersion,
            AwsRegion = Arn.Parse(streamArn).Region
        };

        if (record.Dynamodb != null)
        {
            lambdaRecord.Dynamodb = new DynamoDBEvent.StreamRecord
            {
                ApproximateCreationDateTime = record.Dynamodb.ApproximateCreationDateTime ?? DateTime.MinValue,
                SequenceNumber = record.Dynamodb.SequenceNumber,
                SizeBytes = record.Dynamodb.SizeBytes ?? 0,
                StreamViewType = record.Dynamodb.StreamViewType?.Value
            };

            if (record.Dynamodb.Keys != null)
            {
                lambdaRecord.Dynamodb.Keys = ConvertAttributeMap(record.Dynamodb.Keys);
            }

            if (record.Dynamodb.NewImage != null)
            {
                lambdaRecord.Dynamodb.NewImage = ConvertAttributeMap(record.Dynamodb.NewImage);
            }

            if (record.Dynamodb.OldImage != null)
            {
                lambdaRecord.Dynamodb.OldImage = ConvertAttributeMap(record.Dynamodb.OldImage);
            }
        }

        if (record.UserIdentity != null)
        {
            lambdaRecord.UserIdentity = new DynamoDBEvent.Identity
            {
                PrincipalId = record.UserIdentity.PrincipalId,
                Type = record.UserIdentity.Type
            };
        }

        return lambdaRecord;
    }

    /// <summary>
    /// Convert SDK AttributeValue dictionary to Lambda event AttributeValue dictionary.
    /// </summary>
    internal static Dictionary<string, DynamoDBEvent.AttributeValue> ConvertAttributeMap(Dictionary<string, AttributeValue> sdkMap)
    {
        var result = new Dictionary<string, DynamoDBEvent.AttributeValue>();
        foreach (var kvp in sdkMap)
        {
            result[kvp.Key] = ConvertAttributeValue(kvp.Value);
        }
        return result;
    }

    /// <summary>
    /// Convert a single SDK AttributeValue to the Lambda event AttributeValue.
    /// </summary>
    internal static DynamoDBEvent.AttributeValue ConvertAttributeValue(AttributeValue sdkValue)
    {
        var lambdaValue = new DynamoDBEvent.AttributeValue();

        if (sdkValue.S != null)
            lambdaValue.S = sdkValue.S;
        if (sdkValue.N != null)
            lambdaValue.N = sdkValue.N;
        if (sdkValue.B != null)
            lambdaValue.B = sdkValue.B;
        if (sdkValue.BOOL != null)
            lambdaValue.BOOL = sdkValue.BOOL;
        if (sdkValue.NULL != null)
            lambdaValue.NULL = sdkValue.NULL;
        if (sdkValue.SS?.Count > 0)
            lambdaValue.SS = sdkValue.SS;
        if (sdkValue.NS?.Count > 0)
            lambdaValue.NS = sdkValue.NS;
        if (sdkValue.BS?.Count > 0)
            lambdaValue.BS = sdkValue.BS;
        if (sdkValue.L?.Count > 0)
            lambdaValue.L = sdkValue.L.Select(ConvertAttributeValue).ToList();
        if (sdkValue.M?.Count > 0)
            lambdaValue.M = ConvertAttributeMap(sdkValue.M);

        return lambdaValue;
    }
}
