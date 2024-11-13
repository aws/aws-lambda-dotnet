using Amazon.Lambda.TestTool;
using System.Diagnostics;

var lambdaOptions = CommandLineOptions.Parse(args);

if (lambdaOptions.ShowHelp)
{
    CommandLineOptions.PrintUsage();
    return;
}

var process = LambdaTestToolProcess.Startup(lambdaOptions);

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
        Console.Error.WriteLine($"Error launching browser: {e.Message}");
    }
}

await process.RunningTask;