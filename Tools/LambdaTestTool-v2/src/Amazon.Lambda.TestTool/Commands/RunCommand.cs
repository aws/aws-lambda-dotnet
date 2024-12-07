using System.ComponentModel;
using System.Diagnostics;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Processes;
using Amazon.Lambda.TestTool.Services;
using Spectre.Console.Cli;

namespace Amazon.Lambda.TestTool.Commands;

public sealed class RunCommand(
    IToolInteractiveService toolInteractiveService) : AsyncCommand<RunCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--host <HOST>")]
        [Description(
            "The hostname or IP address used for the test tool's web interface. Any host other than an explicit IP address or localhost (e.g. '*', '+' or 'example.com') binds to all public IPv4 and IPv6 addresses.")]
        [DefaultValue(Constants.DefaultHost)]
        public string Host { get; set; } = Constants.DefaultHost;

        [CommandOption("-p|--port <PORT>")]
        [Description("The port number used for the test tool's web interface.")]
        [DefaultValue(Constants.DefaultPort)]
        public int Port { get; set; } = Constants.DefaultPort;
        
        [CommandOption("--no-launch-window")]
        [Description("Disable auto launching the test tool's web interface in a browser.")]
        public bool NoLaunchWindow { get; set; }
        
        [CommandOption("--pause-exit")]
        [Description("If set to true the test tool will pause waiting for a key input before exiting. The is useful when executing from an IDE so you can avoid having the output window immediately disappear after executing the Lambda code. The default value is true.")]
        public bool PauseExit { get; set; }
        
        [CommandOption("--disable-logs")]
        [Description("Disables logging in the application")]
        public bool DisableLogs { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var process = TestToolProcess.Startup(settings);
            
            if (!settings.NoLaunchWindow)
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
    }
}