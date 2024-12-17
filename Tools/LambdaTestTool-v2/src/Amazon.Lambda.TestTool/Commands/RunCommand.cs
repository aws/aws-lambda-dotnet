// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Processes;
using Amazon.Lambda.TestTool.Services;
using Spectre.Console.Cli;

namespace Amazon.Lambda.TestTool.Commands;

/// <summary>
/// The default command of the application which is responsible for launching the Lambda Runtime API and the API Gateway Emulator.
/// </summary>
public sealed class RunCommand(
    IToolInteractiveService toolInteractiveService) : CancellableAsyncCommand<RunCommandSettings>
{
    /// <summary>
    /// The method responsible for executing the <see cref="RunCommand"/>.
    /// </summary>
    public override async Task<int> ExecuteAsync(CommandContext context, RunCommandSettings settings, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            var tasks = new List<Task>();

            var testToolProcess = TestToolProcess.Startup(settings, cancellationTokenSource.Token);
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

            if (settings.ApiGatewayEmulatorMode is not null)
            {
                var apiGatewayEmulatorProcess =
                    ApiGatewayEmulatorProcess.Startup(settings, cancellationTokenSource.Token);
                tasks.Add(apiGatewayEmulatorProcess.RunningTask);
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
}
