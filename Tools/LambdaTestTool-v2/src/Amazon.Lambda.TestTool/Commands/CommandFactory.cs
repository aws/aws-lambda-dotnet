using System.CommandLine;
using System.Diagnostics;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Processes;
using Amazon.Lambda.TestTool.Services;

namespace Amazon.Lambda.TestTool;

public interface ICommandFactory
{
    Command BuildRootCommand();
}

public class CommandFactory(
    IToolInteractiveService toolInteractiveService) : ICommandFactory
{
    private static readonly object RootCommandLock = new();
    
    public Command BuildRootCommand()
    {
        // Name is important to set here to show correctly in the CLI usage help.
        var rootCommand = new RootCommand
        {
            Name = "lambda-test-tool",
            Description = Constants.ProductName,
        };
        
        Option<string> hostOption = new("--host", () => Constants.DefaultHost, "The hostname or IP address used for the test tool's web interface. Any host other than an explicit IP address or localhost (e.g. '*', '+' or 'example.com') binds to all public IPv4 and IPv6 addresses.");
        Option<int> portOption = new("--port", () => Constants.DefaultPort,"The port number used for the test tool's web interface.");
        Option<bool> noLaunchWindowOption = new("--no-launch-window","Disable auto launching the test tool's web interface in a browser.");
        Option<bool> pauseExitOption = new("--pause-exit",() => true, "If set to true the test tool will pause waiting for a key input before exiting. The is useful when executing from an IDE so you can avoid having the output window immediately disappear after executing the Lambda code. The default value is true.");
        Option<bool> disableLogsOption = new("--disable-logs",() => false);

        lock (RootCommandLock)
        {
            rootCommand.Add(hostOption);
            rootCommand.Add(portOption);
            rootCommand.Add(noLaunchWindowOption);
            rootCommand.Add(pauseExitOption);
            rootCommand.Add(disableLogsOption);
        }
        
        rootCommand.SetHandler(async (context) =>
        {
            try
            {
                var lambdaOptions = new ApplicationOptions
                {
                    Host = context.ParseResult.GetValueForOption(hostOption) ?? Constants.DefaultHost,
                    Port = context.ParseResult.GetValueForOption(portOption),
                    NoLaunchWindow = context.ParseResult.GetValueForOption(noLaunchWindowOption),
                    PauseExit = context.ParseResult.GetValueForOption(pauseExitOption),
                    DisableLogs = context.ParseResult.GetValueForOption(disableLogsOption)
                };
                
                var process = TestToolProcess.Startup(lambdaOptions);
                
                if (!lambdaOptions.NoLaunchWindow)
                {
                    try
                    {
                        var info = new ProcessStartInfo
                        {
                            UseShellExecute = true,
                            FileName = process.ServiceUrl
                        };
                        Process.Start(info);
                    }
                    catch (Exception e)
                    {
                        toolInteractiveService.WriteErrorLine($"Error launching browser: {e.Message}");
                    }
                }
                
                await process.RunningTask;
                
                context.ExitCode = CommandReturnCodes.Success;
            }
            catch (Exception e) when (e.IsExpectedException())
            {
                toolInteractiveService.WriteErrorLine(string.Empty);
                toolInteractiveService.WriteErrorLine(e.Message);
                    
                context.ExitCode = CommandReturnCodes.UserError;
            }
            catch (Exception e)
            {
                // This is a bug
                toolInteractiveService.WriteErrorLine(
                    $"Unhandled exception.{Environment.NewLine}" +
                    $"This is a bug.{Environment.NewLine}" +
                    $"Please copy the stack trace below and file a bug at {Constants.LinkGithubRepo}. " +
                    e.PrettyPrint());
                    
                context.ExitCode = CommandReturnCodes.UnhandledException;
            }
        });
        
        return rootCommand;
    }
}