// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBStreams;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Services;
using System.Text.Json;

namespace Amazon.Lambda.TestTool.Processes.DynamoDBStreamsEventSource;

/// <summary>
/// Process for handling DynamoDB Streams event source for Lambda functions.
/// </summary>
public class DynamoDBStreamsEventSourceProcess
{
    internal const int DefaultBatchSize = 100;
    internal const int DefaultPollingIntervalMs = 1000;

    /// <summary>
    /// The Parent task for all the tasks started for each DynamoDB Streams event source.
    /// </summary>
    public required Task RunningTask { get; init; }

    /// <summary>
    /// Startup DynamoDB Streams event sources
    /// </summary>
    public static DynamoDBStreamsEventSourceProcess Startup(RunCommandSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(settings.DynamoDBStreamsEventSourceConfig))
        {
            throw new InvalidOperationException($"The {nameof(RunCommandSettings.DynamoDBStreamsEventSourceConfig)} can not be null when starting the DynamoDB Streams event source process");
        }

        var configs = LoadDynamoDBStreamsEventSourceConfig(settings.DynamoDBStreamsEventSourceConfig);

        var tasks = new List<Task>();

        foreach (var config in configs)
        {
            var builder = Host.CreateApplicationBuilder();

            var ddbConfig = new AmazonDynamoDBStreamsConfig();
            if (!string.IsNullOrEmpty(config.Profile))
            {
                ddbConfig.Profile = new Profile(config.Profile);
            }

            if (!string.IsNullOrEmpty(config.Region))
            {
                ddbConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(config.Region);
            }

            var streamsClient = new AmazonDynamoDBStreamsClient(ddbConfig);
            builder.Services.AddSingleton<IAmazonDynamoDBStreams>(streamsClient);

            var ddbClientConfig = new AmazonDynamoDBConfig();
            if (!string.IsNullOrEmpty(config.Profile))
            {
                ddbClientConfig.Profile = new Profile(config.Profile);
            }
            if (!string.IsNullOrEmpty(config.Region))
            {
                ddbClientConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(config.Region);
            }
            var ddbClient = new AmazonDynamoDBClient(ddbClientConfig);
            builder.Services.AddSingleton<IAmazonDynamoDB>(ddbClient);

            builder.Services.AddSingleton<ILambdaClient, LambdaClient>();

            var tableName = config.TableName;
            if (string.IsNullOrEmpty(tableName))
            {
                throw new InvalidOperationException("TableName is a required property for DynamoDB Streams event source config");
            }

            var lambdaRuntimeApi = config.LambdaRuntimeApi;
            if (string.IsNullOrEmpty(lambdaRuntimeApi))
            {
                if (!settings.LambdaEmulatorPort.HasValue)
                {
                    throw new InvalidOperationException("No Lambda runtime api endpoint was given as part of the DynamoDB Streams event source config and the current " +
                        "instance of the test tool is not running the Lambda runtime api. Either provide a Lambda runtime api endpoint or set a port for " +
                        "the lambda runtime api when starting the test tool.");
                }
                lambdaRuntimeApi = $"http://{settings.LambdaEmulatorHost}:{settings.LambdaEmulatorPort}/";
            }

            var backgroundServiceConfig = new DynamoDBStreamsEventSourceBackgroundServiceConfig
            {
                BatchSize = config.BatchSize ?? DefaultBatchSize,
                FunctionName = config.FunctionName ?? LambdaRuntimeApi.DefaultFunctionName,
                LambdaRuntimeApi = lambdaRuntimeApi,
                TableName = tableName,
                ShardIteratorType = config.ShardIteratorType ?? "LATEST",
                PollingIntervalMs = config.PollingIntervalMs ?? DefaultPollingIntervalMs
            };

            builder.Services.AddSingleton(backgroundServiceConfig);
            builder.Services.AddHostedService<DynamoDBStreamsEventSourceBackgroundService>();

            var app = builder.Build();
            var task = app.RunAsync(cancellationToken);
            tasks.Add(task);
        }

        return new DynamoDBStreamsEventSourceProcess
        {
            RunningTask = Task.WhenAll(tasks)
        };
    }

    /// <summary>
    /// Load the DynamoDB Streams event source configs. Supports JSON or comma-delimited key-value pair format.
    /// If the value points to a file that exists, the file contents will be read.
    /// </summary>
    internal static List<DynamoDBStreamsEventSourceConfig> LoadDynamoDBStreamsEventSourceConfig(string configString)
    {
        if (File.Exists(configString))
        {
            configString = File.ReadAllText(configString);
        }

        configString = configString.Trim();

        List<DynamoDBStreamsEventSourceConfig>? configs = null;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        if (configString.StartsWith('['))
        {
            try
            {
                configs = JsonSerializer.Deserialize<List<DynamoDBStreamsEventSourceConfig>>(configString, jsonOptions);
                if (configs == null)
                {
                    throw new InvalidOperationException("Failed to parse DynamoDB Streams event source JSON config: " + configString);
                }
            }
            catch (JsonException e)
            {
                throw new InvalidOperationException("Failed to parse DynamoDB Streams event source JSON config: " + configString, e);
            }
        }
        else if (configString.StartsWith('{'))
        {
            try
            {
                var config = JsonSerializer.Deserialize<DynamoDBStreamsEventSourceConfig>(configString, jsonOptions);
                if (config == null)
                {
                    throw new InvalidOperationException("Failed to parse DynamoDB Streams event source JSON config: " + configString);
                }

                configs = new List<DynamoDBStreamsEventSourceConfig> { config };
            }
            catch (JsonException e)
            {
                throw new InvalidOperationException("Failed to parse DynamoDB Streams event source JSON config: " + configString, e);
            }
        }
        else
        {
            var config = new DynamoDBStreamsEventSourceConfig();
            var tokens = configString.Split(',');
            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                var keyValuePair = token.Split('=');
                if (keyValuePair.Length != 2)
                {
                    throw new InvalidOperationException("Failed to parse DynamoDB Streams event source config. Format should be \"TableName=<value>,FunctionName=<value>,...\"");
                }

                switch (keyValuePair[0].ToLower().Trim())
                {
                    case "batchsize":
                        if (!int.TryParse(keyValuePair[1].Trim(), out var batchSize))
                        {
                            throw new InvalidOperationException("Value for batch size is not a formatted integer");
                        }
                        config.BatchSize = batchSize;
                        break;
                    case "functionname":
                        config.FunctionName = keyValuePair[1].Trim();
                        break;
                    case "lambdaruntimeapi":
                        config.LambdaRuntimeApi = keyValuePair[1].Trim();
                        break;
                    case "profile":
                        config.Profile = keyValuePair[1].Trim();
                        break;
                    case "region":
                        config.Region = keyValuePair[1].Trim();
                        break;
                    case "tablename":
                        config.TableName = keyValuePair[1].Trim();
                        break;
                    case "sharditeratortype":
                        config.ShardIteratorType = keyValuePair[1].Trim();
                        break;
                    case "pollingintervalms":
                        if (!int.TryParse(keyValuePair[1].Trim(), out var pollingInterval))
                        {
                            throw new InvalidOperationException("Value for polling interval is not a formatted integer");
                        }
                        config.PollingIntervalMs = pollingInterval;
                        break;
                }
            }

            configs = new List<DynamoDBStreamsEventSourceConfig> { config };
        }

        return configs;
    }
}
