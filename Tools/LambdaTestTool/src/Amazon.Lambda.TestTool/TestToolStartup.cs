using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

using Amazon.Lambda.TestTool.Runtime;

namespace Amazon.Lambda.TestTool
{
    public class TestToolStartup
    {
        public static void Startup(string productName, Action<LocalLambdaOptions, bool> uiStartup, string[] args)
        {
            try
            {
                Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", "AWS_DOTNET_LAMDBA_TEST_TOOL_3_1_" + Utils.DetermineToolVersion());

                Utils.PrintToolTitle(productName);

                var commandOptions = CommandLineOptions.Parse(args);

                var options = new LocalLambdaOptions()
                {
                    Port = commandOptions.Port
                };

                var path = commandOptions.Path ?? Directory.GetCurrentDirectory();

#if NETCORE_2_1
                var targetFramework = "netcoreapp2.1";
#elif NETCORE_3_1
                var targetFramework = "netcoreapp3.1";
#endif

                // Check to see if running in debug mode from this project's directory which means the test tool is being debugged.
                // To make debugging easier pick one of the test Lambda projects.
                if (path.EndsWith("Amazon.Lambda.TestTool.WebTester21"))
                {
                    path = Path.Combine(path, $"../../tests/LambdaFunctions/netcore21/S3EventFunction/bin/Debug/{targetFramework}");
                }
                else if (path.EndsWith("Amazon.Lambda.TestTool.WebTester31"))
                {
                    path = Path.Combine(path, $"../../tests/LambdaFunctions/netcore31/S3EventFunction/bin/Debug/{targetFramework}");
                }
                // If running in the project directory select the build directory so the deps.json file can be found.
                else if (Utils.IsProjectDirectory(path))
                {
                    path = Path.Combine(path, $"bin/Debug/{targetFramework}");
                }

                options.LambdaRuntime = LocalLambdaRuntime.Initialize(path);
                Console.WriteLine($"Loaded local Lambda runtime from project output {path}");

                // Look for aws-lambda-tools-defaults.json or other config files.
                options.LambdaConfigFiles = Utils.SearchForConfigFiles(path);

                // Start the test tool web server.
                uiStartup(options, !commandOptions.NoLaunchWindow);
            }
            catch (CommandLineParseException e)
            {
                Console.WriteLine($"Invalid command line arguments: {e.Message}");
                CommandLineOptions.PrintUsage();
                if (Debugger.IsAttached)
                {
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey();
                }
                System.Environment.Exit(-1);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Unknown error occurred causing process exit: {e.Message}");
                Console.Error.WriteLine(e.StackTrace);
                if (Debugger.IsAttached)
                {
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey();
                }
                Environment.Exit(-2);
            }
        }
    }
}
