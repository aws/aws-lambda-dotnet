// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Processes;
using Amazon.Lambda.TestTool.Processes.SQSEventSource;
using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.TestTool.Services.IO;
using Spectre.Console.Cli;

namespace Amazon.Lambda.TestTool.Commands;

/// <summary>
/// The default command of the application which is responsible for launching the Lambda Runtime API and the API Gateway Emulator.
/// </summary>
public sealed class RunCommand(
    IToolInteractiveService toolInteractiveService, IEnvironmentManager environmentManager) : CancellableAsyncCommand<RunCommandSettings>
{
    public const string LAMBDA_RUNTIME_API_PORT = "LAMBDA_RUNTIME_API_PORT";
    public const string API_GATEWAY_EMULATOR_PORT = "API_GATEWAY_EMULATOR_PORT";

    /// <summary>
    /// The method responsible for executing the <see cref="RunCommand"/>.
    /// </summary>
    public override async Task<int> ExecuteAsync(CommandContext context, RunCommandSettings settings, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            EvaluateEnvironmentVariables(settings);

            if (!settings.LambdaEmulatorPort.HasValue && !settings.ApiGatewayEmulatorPort.HasValue && string.IsNullOrEmpty(settings.SQSEventSourceConfig))
            {
                throw new ArgumentException("At least one of the following parameters must be set: " +
                                            "--lambda-emulator-port, --api-gateway-emulator-port or --sqs-eventsource-config");
            }

            var tasks = new List<Task>();

            if (settings.LambdaEmulatorPort.HasValue)
            {
                var testToolProcess = TestToolProcess.Startup(settings, toolInteractiveService, cancellationTokenSource.Token);
                tasks.Add(testToolProcess.RunningTask);

                if (!settings.NoLaunchWindow)
                {
                    try
                    {
                        var info = new ProcessStartInfo
                        {
                            UseShellExecute = true,
                            FileName = testToolProcess.ServiceUrl
                        };
                        Process.Start(info);
                    }
                    catch (Exception e)
                    {
                        toolInteractiveService.WriteErrorLine($"Error launching browser: {e.Message}");
                    }
                }
            }

            if (settings.ApiGatewayEmulatorPort.HasValue)
            {
                if (settings.ApiGatewayEmulatorMode is null)
                {
                    throw new ArgumentException("When --api-gateway-emulator-port is set the --api-gateway-emulator-mode must be set to configure the mode for the API Gateway emulator.");
                }

                var apiGatewayEmulatorProcess =
                    ApiGatewayEmulatorProcess.Startup(settings, toolInteractiveService, cancellationTokenSource.Token);
                tasks.Add(apiGatewayEmulatorProcess.RunningTask);
            }

            if (!string.IsNullOrEmpty(settings.SQSEventSourceConfig))
            {
                var sqsEventSourceProcess = SQSEventSourceProcess.Startup(settings, cancellationTokenSource.Token);
                tasks.Add(sqsEventSourceProcess.RunningTask);
            }

            await Task.WhenAny(tasks);

            return CommandReturnCodes.Success;
        }
        catch (Exception e) when (e.IsExpectedException())
        {
            toolInteractiveService.WriteErrorLine(string.Empty);
            toolInteractiveService.WriteErrorLine(e.Message);

            return CommandReturnCodes.UserError;
        }
        catch (Exception e)
        {
            // This is a bug
            toolInteractiveService.WriteErrorLine(
                $"Unhandled exception.{Environment.NewLine}" +
                $"This is a bug.{Environment.NewLine}" +
                $"Please copy the stack trace below and file a bug at {Constants.LinkGithubRepo}. " +
                e.PrettyPrint());

            return CommandReturnCodes.UnhandledException;
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
        }
    }

    private void EvaluateEnvironmentVariables(RunCommandSettings settings)
    {
        var environmentVariables = environmentManager.GetEnvironmentVariables();
        if (environmentVariables == null)
            return;

        if (environmentVariables.Contains(LAMBDA_RUNTIME_API_PORT))
        {
            var envValue = environmentVariables[LAMBDA_RUNTIME_API_PORT]?.ToString();
            if (int.TryParse(envValue, out var port))
            {
                settings.LambdaEmulatorPort = port;
            }
            else
            {
                throw new ArgumentException($"Value for {LAMBDA_RUNTIME_API_PORT} environment variable was not a valid port number");
            }
        }
        if (environmentVariables.Contains(API_GATEWAY_EMULATOR_PORT))
        {
            var envValue = environmentVariables[API_GATEWAY_EMULATOR_PORT]?.ToString();
            if (int.TryParse(envValue, out var port))
            {
                settings.ApiGatewayEmulatorPort = port;
            }
            else
            {
                throw new ArgumentException($"Value for {API_GATEWAY_EMULATOR_PORT} environment variable was not a valid port number");
            }
        }

        if (settings.SQSEventSourceConfig != null && settings.SQSEventSourceConfig.StartsWith(Constants.ArgumentEnvironmentVariablePrefix, StringComparison.CurrentCultureIgnoreCase))
        {
            var envVariable = settings.SQSEventSourceConfig.Substring(Constants.ArgumentEnvironmentVariablePrefix.Length);
            if (!environmentVariables.Contains(envVariable))
            {
                throw new InvalidOperationException($"Environment variable {envVariable} for the SQS event source config was empty");
            }
            settings.SQSEventSourceConfig = environmentVariables[envVariable]?.ToString();
        }
    }
}
