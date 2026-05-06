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
        // If the configured value is already a stream ARN, use it directly
        if (_config.TableName.StartsWith("arn:") && _config.TableName.Contains("/stream/"))
        {
            return _config.TableName;
        }

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
            // Poll all shards concurrently
            var tasks = new List<Task<(int Index, GetRecordsResponse? Response)>>();
            for (int i = 0; i < shardIterators.Count; i++)
            {
                if (shardIterators[i] == null)
                    continue;

                var index = i;
                var iterator = shardIterators[i]!;
                tasks.Add(PollShard(index, iterator, stoppingToken));
            }

            if (tasks.Count == 0)
            {
                // All iterators exhausted — re-discover shards
                shardIterators = await GetShardIterators(streamArn, stoppingToken);
                if (shardIterators.Count == 0)
                {
                    await Task.Delay(1000, stoppingToken);
                }
                continue;
            }

            var results = await Task.WhenAll(tasks);

            var hasRecords = false;
            foreach (var (index, response) in results)
            {
                if (response == null)
                    continue;

                shardIterators[index] = response.NextShardIterator;

                if (response.Records.Count == 0)
                    continue;

                hasRecords = true;
                var lambdaRecords = ConvertToLambdaRecords(response.Records, streamArn);

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

            // Re-discover shards when any iterator becomes null (shard closed/split)
            if (shardIterators.Any(s => s == null))
            {
                var newShards = await GetShardIterators(streamArn, stoppingToken);
                // Replace only null entries with new iterators, keep active ones
                for (int i = 0; i < shardIterators.Count; i++)
                {
                    if (shardIterators[i] == null && i < newShards.Count)
                    {
                        shardIterators[i] = newShards[i];
                    }
                }
                // Append any additional new shards discovered
                for (int i = shardIterators.Count; i < newShards.Count; i++)
                {
                    shardIterators.Add(newShards[i]);
                }
                // Remove remaining null entries (fully exhausted shards with no replacement)
                shardIterators = shardIterators.Where(s => s != null).ToList();
            }

            if (!hasRecords)
            {
                await Task.Delay(_config.PollingIntervalMs, stoppingToken);
            }
        }
    }

    private async Task<(int Index, GetRecordsResponse? Response)> PollShard(int index, string iterator, CancellationToken stoppingToken)
    {
        var response = await _streamsClient.GetRecordsAsync(new GetRecordsRequest
        {
            ShardIterator = iterator,
            Limit = _config.BatchSize
        }, stoppingToken);

        return (index, response);
    }

    private async Task<List<string?>> GetShardIterators(string streamArn, CancellationToken stoppingToken)
    {
        var shards = new List<Shard>();
        string? lastEvaluatedShardId = null;

        // Paginate through all shards
        do
        {
            var describeResponse = await _streamsClient.DescribeStreamAsync(new DescribeStreamRequest
            {
                StreamArn = streamArn,
                ExclusiveStartShardId = lastEvaluatedShardId
            }, stoppingToken);

            shards.AddRange(describeResponse.StreamDescription.Shards);
            lastEvaluatedShardId = describeResponse.StreamDescription.LastEvaluatedShardId;
        } while (lastEvaluatedShardId != null);

        var iterators = new List<string?>();

        foreach (var shard in shards)
        {
            var iteratorResponse = await _streamsClient.GetShardIteratorAsync(new GetShardIteratorRequest
            {
                StreamArn = streamArn,
                ShardId = shard.ShardId,
                ShardIteratorType = new ShardIteratorType(_config.ShardIteratorType)
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
        if (sdkValue.SS != null)
            lambdaValue.SS = sdkValue.SS;
        if (sdkValue.NS != null)
            lambdaValue.NS = sdkValue.NS;
        if (sdkValue.BS != null)
            lambdaValue.BS = sdkValue.BS;
        if (sdkValue.L != null)
            lambdaValue.L = sdkValue.L.Select(ConvertAttributeValue).ToList();
        if (sdkValue.M != null)
            lambdaValue.M = ConvertAttributeMap(sdkValue.M);

        return lambdaValue;
    }
}
