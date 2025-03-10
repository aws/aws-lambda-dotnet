// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.SQS;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Services;
using System.Text.Json;

namespace Amazon.Lambda.TestTool.Processes.SQSEventSource;

/// <summary>
/// Process for handling SQS event source for Lambda functions.
/// </summary>
public class SQSEventSourceProcess
{
    internal const int DefaultBatchSize = 10;
    internal const int DefaultVisiblityTimeout = 30;

    /// <summary>
    /// The API Gateway emulator task that was started.
    /// </summary>
    public required Task RunningTask { get; init; }

    /// <summary>
    /// Startup SQS event sources
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static SQSEventSourceProcess Startup(RunCommandSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(settings.SQSEventSourceConfig))
        {
            throw new InvalidOperationException($"The {nameof(RunCommandSettings.SQSEventSourceConfig)} can not be null when starting the SQS event source process");
        }

        var sqsEventSourceConfigs = LoadSQSEventSourceConfig(settings.SQSEventSourceConfig);

        var tasks = new List<Task>();

        // Spin up a separate SQSEventSourceBackgroundService for each SQS event source config listed in the SQSEventSourceConfig
        foreach (var sqsEventSourceConfig in sqsEventSourceConfigs)
        {
            var builder = Host.CreateApplicationBuilder();

            var sqsConfig = new AmazonSQSConfig();
            if (!string.IsNullOrEmpty(sqsEventSourceConfig.Profile))
            {
                sqsConfig.Profile = new Profile(sqsEventSourceConfig.Profile);
            }

            if (!string.IsNullOrEmpty(sqsEventSourceConfig.Region))
            {
                sqsConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(sqsEventSourceConfig.Region);
            }

            var sqsClient = new AmazonSQSClient(sqsConfig);
            builder.Services.AddSingleton<IAmazonSQS>(sqsClient);

            var queueUrl = sqsEventSourceConfig.QueueUrl;
            if (string.IsNullOrEmpty(queueUrl))
            {
                throw new InvalidOperationException("QueueUrl is a required property for SQS event source config");
            }

            var lambdaRuntimeApi = sqsEventSourceConfig.LambdaRuntimeApi;
            if (string.IsNullOrEmpty(lambdaRuntimeApi))
            {
                if (!settings.LambdaEmulatorPort.HasValue)
                {
                    throw new InvalidOperationException("No Lambda runtime api endpoint was given as part of the SQS event source config and the current " +
                        "instance of the test tool is not running the Lambda runtime api. Either provide a Lambda runtime api endpoint or set a port for " +
                        "the lambda runtime api when starting the test tool.");
                }
                lambdaRuntimeApi = $"http://{settings.LambdaEmulatorHost}:{settings.LambdaEmulatorPort}/";
            }

            var backgroundServiceConfig = new SQSEventSourceBackgroundServiceConfig
            {
                BatchSize = sqsEventSourceConfig.BatchSize ?? DefaultBatchSize,
                DisableMessageDelete = sqsEventSourceConfig.DisableMessageDelete ?? false,
                FunctionName = sqsEventSourceConfig.FunctionName ?? LambdaRuntimeApi.DefaultFunctionName,
                LambdaRuntimeApi = lambdaRuntimeApi,
                QueueUrl = queueUrl,
                VisibilityTimeout = sqsEventSourceConfig.VisibilityTimeout ?? DefaultVisiblityTimeout
            };

            builder.Services.AddSingleton(backgroundServiceConfig);
            builder.Services.AddHostedService<SQSEventSourceBackgroundService>();

            var app = builder.Build();
            var task = app.RunAsync(cancellationToken);
            tasks.Add(task);
        }

        var combinedTask = Task.WhenAll(tasks);


        return new SQSEventSourceProcess
        {
            RunningTask = combinedTask
        };
    }

    internal static List<SQSEventSourceConfig> LoadSQSEventSourceConfig(string sqsEventSourceConfigJson)
    {
        if (File.Exists(sqsEventSourceConfigJson))
        {
            sqsEventSourceConfigJson = File.ReadAllText(sqsEventSourceConfigJson);
        }

        sqsEventSourceConfigJson = sqsEventSourceConfigJson.Trim();

        List<SQSEventSourceConfig>? configs = null;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };


        if (sqsEventSourceConfigJson.StartsWith('['))
        {
            configs = JsonSerializer.Deserialize<List<SQSEventSourceConfig>>(sqsEventSourceConfigJson, jsonOptions);
            if (configs == null)
            {
                throw new InvalidOperationException("Failed to parse SQS event source JSON config: " + sqsEventSourceConfigJson);
            }
        }
        else if (sqsEventSourceConfigJson.StartsWith('{'))
        {
            var config = JsonSerializer.Deserialize<SQSEventSourceConfig>(sqsEventSourceConfigJson, jsonOptions);
            if (config == null)
            {
                throw new InvalidOperationException("Failed to parse SQS event source JSON config: " + sqsEventSourceConfigJson);
            }

            configs = new List<SQSEventSourceConfig> { config };
        }
        else if (Uri.TryCreate(sqsEventSourceConfigJson, UriKind.Absolute, out _))
        {
            configs = new List<SQSEventSourceConfig> { new SQSEventSourceConfig { QueueUrl = sqsEventSourceConfigJson } };
        }
        else
        {
            var config = new SQSEventSourceConfig();
            var tokens = sqsEventSourceConfigJson.Split(',');            
            foreach(var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                var keyValuePair = token.Split('=');
                if (keyValuePair.Length != 2)
                {
                    throw new InvalidOperationException("Failed to parse SQS event source config. Format should be \"QueueUrl=<value>,FunctionName=<value>,...\"");
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
                    case "disablemessagedelete":
                        if (!bool.TryParse(keyValuePair[1].Trim(), out var disableMessageDelete))
                        {
                            throw new InvalidOperationException("Value for disable message delete is not a formatted boolean");
                        }
                        config.DisableMessageDelete = disableMessageDelete;
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
                    case "queueurl":
                        config.QueueUrl = keyValuePair[1].Trim();
                        break;
                    case "region":
                        config.Region = keyValuePair[1].Trim();
                        break;
                    case "visibilitytimeout":
                        if (!int.TryParse(keyValuePair[1].Trim(), out var visibilityTimeout))
                        {
                            throw new InvalidOperationException("Value for visibility timeout is not a formatted integer");
                        }
                        config.VisibilityTimeout = visibilityTimeout;
                        break;
                }
            }

            configs = new List<SQSEventSourceConfig> { config };
        }

        return configs;
    }
}
