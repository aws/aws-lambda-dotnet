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
    /// The Parent task for all of the tasks started for each list SQS event source.
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

        // Create a separate SQSEventSourceBackgroundService for each SQS event source config listed in the SQSEventSourceConfig
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

        return new SQSEventSourceProcess
        {
            RunningTask = Task.WhenAll(tasks)
        };
    }

    /// <summary>
    /// Load the SQS event source configs. The format of the config can be either JSON or comma delimited key pairs.
    /// With the JSON format it is possible to configure multiple event sources but special care is required
    /// escaping the quotes. The JSON format also provides consistency with the API Gateway configuration.
    ///
    /// The comma delimited key pairs allows users to configure a single SQS event source without having
    /// to deal with escaping quotes.
    ///
    /// If the value of sqsEventSourceConfigString points to a file that exists the contents of the file
    /// will be read and sued for the value for SQS event source config.
    ///
    /// If the value of sqsEventSourceConfigString starts with "env:" then it assume the suffix of the value
    /// is an environment variable containing the config. This is used by the .NET Aspire integration because
    /// the values required for the config are resolved after command line arguments are setup in Aspire.
    /// </summary>
    /// <param name="sqsEventSourceConfigString"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    internal static List<SQSEventSourceConfig> LoadSQSEventSourceConfig(string sqsEventSourceConfigString)
    {
        if (sqsEventSourceConfigString.StartsWith(Constants.ARGUMENT_ENVIRONMENT_VARIABLE_PREFIX, StringComparison.CurrentCultureIgnoreCase))
        {
            var envVariable = sqsEventSourceConfigString.Substring(Constants.ARGUMENT_ENVIRONMENT_VARIABLE_PREFIX.Length);            
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVariable)))
            {
                throw new InvalidOperationException($"Environment variable {envVariable} for the SQS event source config was empty");
            }
            sqsEventSourceConfigString = Environment.GetEnvironmentVariable(envVariable)!;
        }
        else if (File.Exists(sqsEventSourceConfigString))
        {
            sqsEventSourceConfigString = File.ReadAllText(sqsEventSourceConfigString);
        }

        sqsEventSourceConfigString = sqsEventSourceConfigString.Trim();

        List<SQSEventSourceConfig>? configs = null;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Check to see if the config is in JSON array format.
        // The JSON format provides consistency with the API Gateway config style.
        if (sqsEventSourceConfigString.StartsWith('['))
        {
            try
            {
                configs = JsonSerializer.Deserialize<List<SQSEventSourceConfig>>(sqsEventSourceConfigString, jsonOptions);
                if (configs == null)
                {
                    throw new InvalidOperationException("Failed to parse SQS event source JSON config: " + sqsEventSourceConfigString);
                }
            }
            catch(JsonException e)
            {
                throw new InvalidOperationException("Failed to parse SQS event source JSON config: " + sqsEventSourceConfigString, e);
            }
        }
        // Config is a single object JSON document.
        // The JSON format provides consistency with the API Gateway config style.
        else if (sqsEventSourceConfigString.StartsWith('{'))
        {
            try
            {
                var config = JsonSerializer.Deserialize<SQSEventSourceConfig>(sqsEventSourceConfigString, jsonOptions);
                if (config == null)
                {
                    throw new InvalidOperationException("Failed to parse SQS event source JSON config: " + sqsEventSourceConfigString);
                }

                configs = new List<SQSEventSourceConfig> { config };
            }
            catch (JsonException e)
            {
                throw new InvalidOperationException("Failed to parse SQS event source JSON config: " + sqsEventSourceConfigString, e);
            }
        }
        // Config is a QueueUrl only. The current test tool instance will be assumed the Lambda runtime api and the
        // messages will be sent to the default function. Support this format allows for an
        // simple CLI experience of just providing a single value for the default scenario.
        else if (Uri.TryCreate(sqsEventSourceConfigString, UriKind.Absolute, out _))
        {
            configs = new List<SQSEventSourceConfig> { new SQSEventSourceConfig { QueueUrl = sqsEventSourceConfigString } };
        }
        // Config is in comma delimited key value pair format. This format allows setting all the parameters without having
        // to deal with escaping quotes like the JSON format.
        else
        {
            var config = new SQSEventSourceConfig();
            var tokens = sqsEventSourceConfigString.Split(',');
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
