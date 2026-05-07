// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.DynamoDBv2;
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
    private readonly IAmazonDynamoDB _ddbClient;
    private readonly IAmazonDynamoDBStreams _streamsClient;
    private readonly ILambdaClient _lambdaClient;
    private readonly DynamoDBStreamsEventSourceBackgroundServiceConfig _config;

    /// <summary>
    /// Constructs instance of <see cref="DynamoDBStreamsEventSourceBackgroundService"/>.
    /// </summary>
    public DynamoDBStreamsEventSourceBackgroundService(
        ILogger<DynamoDBStreamsEventSourceProcess> logger,
        IAmazonDynamoDB ddbClient,
        IAmazonDynamoDBStreams streamsClient,
        DynamoDBStreamsEventSourceBackgroundServiceConfig config,
        ILambdaClient lambdaClient)
    {
        _logger = logger;
        _ddbClient = ddbClient;
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
            _logger.LogInformation("Using provided stream ARN directly: {streamArn}", _config.TableName);
            return _config.TableName;
        }

        _logger.LogInformation("Looking up latest stream ARN for table {tableName}", _config.TableName);
        var response = await _ddbClient.DescribeTableAsync(_config.TableName, stoppingToken);
        _logger.LogInformation("Resolved stream ARN: {streamArn}", response.Table.LatestStreamArn);
        return response.Table.LatestStreamArn;
    }

    private async Task PollStream(string streamArn, CancellationToken stoppingToken)
    {
        // Shard polling strategy:
        //
        // Goal: Only deliver records to Lambda that were written AFTER the test tool started.
        //
        // 1. At startup, discover all shards. Open shards get a LATEST iterator (future records only).
        //    Closed shards are recorded in a "closed at startup" set and never polled — they contain
        //    only historical data from before the tool started.
        //
        // 2. Every 30 seconds (or immediately when a shard is exhausted), re-discover shards:
        //    - Shards already being polled: leave their iterator alone (preserves position).
        //    - Shards in the "closed at startup" set: skip (pre-existing historical data).
        //    - Any other shard (new since startup): poll with TRIM_HORIZON to read all its records,
        //      since the shard was created after the tool started and all its data is relevant.

        var closedAtStartup = new HashSet<string>();
        var shardIterators = await DiscoverInitialShards(streamArn, closedAtStartup, stoppingToken);

        _logger.LogInformation("Initial discovery: {openCount} open shard(s), {closedCount} closed shard(s) at startup",
            shardIterators.Count, closedAtStartup.Count);

        var lastDiscoveryTime = DateTime.UtcNow;
        const int ShardRediscoveryIntervalSeconds = 30;

        while (!stoppingToken.IsCancellationRequested)
        {
            // Poll all active shards concurrently
            var tasks = new List<Task<(string ShardId, GetRecordsResponse? Response)>>();
            foreach (var (shardId, iterator) in shardIterators)
            {
                if (iterator == null)
                    continue;
                tasks.Add(PollShard(shardId, iterator, stoppingToken));
            }

            var activeCount = tasks.Count;
            _logger.LogInformation("Polling {activeShardCount} active shard(s)", activeCount);

            if (activeCount == 0)
            {
                // No active shards — re-discover
                shardIterators = await DiscoverNewShards(streamArn, shardIterators, closedAtStartup, stoppingToken);
                lastDiscoveryTime = DateTime.UtcNow;
                if (shardIterators.Count == 0)
                {
                    await Task.Delay(1000, stoppingToken);
                }
                continue;
            }

            var results = await Task.WhenAll(tasks);

            var hasRecords = false;
            var shardExhausted = false;
            foreach (var (shardId, response) in results)
            {
                if (response == null)
                    continue;

                if (response.NextShardIterator == null)
                {
                    _logger.LogInformation("Shard {shardId} exhausted (closed), records in final batch: {count}",
                        shardId, response.Records.Count);
                    shardIterators.Remove(shardId);
                    shardExhausted = true;
                }
                else
                {
                    shardIterators[shardId] = response.NextShardIterator;
                }

                if (response.Records.Count == 0)
                    continue;

                hasRecords = true;
                _logger.LogInformation("Retrieved {recordCount} record(s) from shard {shardId}", response.Records.Count, shardId);
                var lambdaRecords = ConvertToLambdaRecords(response.Records, streamArn);

                var lambdaPayload = new DynamoDBEvent { Records = lambdaRecords };
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

            // Re-discover if a shard was exhausted or 30 seconds have elapsed
            var timeSinceDiscovery = (DateTime.UtcNow - lastDiscoveryTime).TotalSeconds;
            if (shardExhausted || timeSinceDiscovery >= ShardRediscoveryIntervalSeconds)
            {
                _logger.LogInformation("Re-discovering shards (exhausted={shardExhausted}, elapsed={elapsed}s)",
                    shardExhausted, (int)timeSinceDiscovery);
                shardIterators = await DiscoverNewShards(streamArn, shardIterators, closedAtStartup, stoppingToken);
                lastDiscoveryTime = DateTime.UtcNow;
                continue;
            }

            if (!hasRecords)
            {
                await Task.Delay(_config.PollingIntervalMs, stoppingToken);
            }
        }
    }

    private async Task<(string ShardId, GetRecordsResponse? Response)> PollShard(string shardId, string iterator, CancellationToken stoppingToken)
    {
        var response = await _streamsClient.GetRecordsAsync(new GetRecordsRequest
        {
            ShardIterator = iterator,
            Limit = _config.BatchSize
        }, stoppingToken);

        return (shardId, response);
    }

    /// <summary>
    /// Initial shard discovery at startup. Uses LATEST for open shards and records closed shard IDs.
    /// </summary>
    private async Task<Dictionary<string, string?>> DiscoverInitialShards(string streamArn, HashSet<string> closedAtStartup, CancellationToken stoppingToken)
    {
        var shards = await GetAllShards(streamArn, stoppingToken);
        var iterators = new Dictionary<string, string?>();

        foreach (var shard in shards)
        {
            var isClosed = shard.SequenceNumberRange?.EndingSequenceNumber != null;
            if (isClosed)
            {
                closedAtStartup.Add(shard.ShardId);
                continue;
            }

            // Open shard — use LATEST to only get records created after startup
            var iteratorResponse = await _streamsClient.GetShardIteratorAsync(new GetShardIteratorRequest
            {
                StreamArn = streamArn,
                ShardId = shard.ShardId,
                ShardIteratorType = ShardIteratorType.LATEST
            }, stoppingToken);

            _logger.LogInformation("Got LATEST iterator for startup shard {shardId}", shard.ShardId);
            iterators[shard.ShardId] = iteratorResponse.ShardIterator;
        }

        return iterators;
    }

    /// <summary>
    /// Ongoing shard discovery. Preserves existing iterators, skips shards closed at startup,
    /// and starts TRIM_HORIZON pollers for any new shards (even if closed).
    /// </summary>
    private async Task<Dictionary<string, string?>> DiscoverNewShards(string streamArn, Dictionary<string, string?> existingIterators, HashSet<string> closedAtStartup, CancellationToken stoppingToken)
    {
        var shards = await GetAllShards(streamArn, stoppingToken);
        var iterators = new Dictionary<string, string?>(existingIterators);

        foreach (var shard in shards)
        {
            // Already being polled — leave iterator alone
            if (iterators.ContainsKey(shard.ShardId))
                continue;

            // Was closed at startup — skip
            if (closedAtStartup.Contains(shard.ShardId))
                continue;

            // New shard discovered after startup — use TRIM_HORIZON to read all its records
            var iteratorResponse = await _streamsClient.GetShardIteratorAsync(new GetShardIteratorRequest
            {
                StreamArn = streamArn,
                ShardId = shard.ShardId,
                ShardIteratorType = ShardIteratorType.TRIM_HORIZON
            }, stoppingToken);

            _logger.LogInformation("Got TRIM_HORIZON iterator for new shard {shardId}", shard.ShardId);
            iterators[shard.ShardId] = iteratorResponse.ShardIterator;
        }

        return iterators;
    }

    private async Task<List<Shard>> GetAllShards(string streamArn, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Discovering shards for stream {streamArn}", streamArn);
        var shards = new List<Shard>();
        string? lastEvaluatedShardId = null;

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

        _logger.LogInformation("Discovered {shardCount} shard(s)", shards.Count);
        return shards;
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
