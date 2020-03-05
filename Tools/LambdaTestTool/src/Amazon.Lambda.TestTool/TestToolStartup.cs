using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                Utils.PrintToolTitle(productName);

                var commandOptions = CommandLineOptions.Parse(args);
                if (commandOptions.ShowHelp)
                {
                    CommandLineOptions.PrintUsage();
                    return;
                }

                var localLambdaOptions = new LocalLambdaOptions()
                {
                    Port = commandOptions.Port
                };

                var lambdaAssemblyDirectory = commandOptions.Path ?? Directory.GetCurrentDirectory();

#if NETCORE_2_1
                var targetFramework = "netcoreapp2.1";
#elif NETCORE_3_1
                var targetFramework = "netcoreapp3.1";
#endif

                // Check to see if running in debug mode from this project's directory which means the test tool is being debugged.
                // To make debugging easier pick one of the test Lambda projects.
                if (lambdaAssemblyDirectory.EndsWith("Amazon.Lambda.TestTool.WebTester21"))
                {
                    lambdaAssemblyDirectory = Path.Combine(lambdaAssemblyDirectory, $"../../tests/LambdaFunctions/netcore21/S3EventFunction/bin/Debug/{targetFramework}");
                }
                else if (lambdaAssemblyDirectory.EndsWith("Amazon.Lambda.TestTool.WebTester31"))
                {
                    lambdaAssemblyDirectory = Path.Combine(lambdaAssemblyDirectory, $"../../tests/LambdaFunctions/netcore31/S3EventFunction/bin/Debug/{targetFramework}");
                }
                // If running in the project directory select the build directory so the deps.json file can be found.
                else if (Utils.IsProjectDirectory(lambdaAssemblyDirectory))
                {
                    lambdaAssemblyDirectory = Path.Combine(lambdaAssemblyDirectory, $"bin/Debug/{targetFramework}");
                }

                localLambdaOptions.LambdaRuntime = LocalLambdaRuntime.Initialize(lambdaAssemblyDirectory);
                Console.WriteLine($"Loaded local Lambda runtime from project output {lambdaAssemblyDirectory}");

                if (commandOptions.NoUI)
                {
                    ExecuteWithNoUi(localLambdaOptions, commandOptions, lambdaAssemblyDirectory);
                }
                else
                {
                    // Look for aws-lambda-tools-defaults.json or other config files.
                    localLambdaOptions.LambdaConfigFiles = Utils.SearchForConfigFiles(lambdaAssemblyDirectory);

                    // Start the test tool web server.
                    uiStartup(localLambdaOptions, !commandOptions.NoLaunchWindow);
                }
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
        
        public static void ExecuteWithNoUi(LocalLambdaOptions localLambdaOptions, CommandLineOptions commandOptions, string lambdaAssemblyDirectory)
        {
            string configFile = null;
            if (string.IsNullOrEmpty(commandOptions.ConfigFile))
            {
                configFile = Utils.SearchForConfigFiles(lambdaAssemblyDirectory).FirstOrDefault(x => string.Equals("aws-lambda-tools-defaults.json", Path.GetFileName(x), StringComparison.OrdinalIgnoreCase));
            }
            else if (Path.IsPathRooted(commandOptions.ConfigFile))
            {
                configFile = commandOptions.ConfigFile;
            }
            else if(File.Exists(Path.Combine(lambdaAssemblyDirectory, commandOptions.ConfigFile)))
            {
                configFile = Path.Combine(lambdaAssemblyDirectory, commandOptions.ConfigFile);
            }
            else if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), commandOptions.ConfigFile)))
            {
                configFile = Path.Combine(Directory.GetCurrentDirectory(), commandOptions.ConfigFile);
            }

            LambdaConfigInfo configInfo;
            if (configFile != null)
            {
                configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(configFile);
            }
            else
            {
                configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(new LambdaConfigFile
                {
                    FunctionHandler = commandOptions.FunctionHandler,
                    ConfigFileLocation = Utils.FindLambdaProjectDirectory(lambdaAssemblyDirectory) ?? lambdaAssemblyDirectory
                });
            }

            var functionHandler = commandOptions.FunctionHandler;
            if (string.IsNullOrEmpty(commandOptions.FunctionHandler))
            {
                if(configInfo.FunctionInfos.Count == 1)
                {
                    functionHandler = configInfo.FunctionInfos[0].Handler;
                }
                else
                {
                    throw new CommandLineParseException("Project has more then one Lambda function defined. Use the --function-handler switch to identify the function to execute.");
                }
            }

            var request = new ExecutionRequest()
            {
                AWSProfile = commandOptions.AWSProfile ?? configInfo.AWSProfile,
                AWSRegion = commandOptions.AWSRegion ?? configInfo.AWSRegion,
                Payload = commandOptions.Payload,
                Function = localLambdaOptions.LoadLambdaFuntion(configInfo, functionHandler)
            };

            try
            {
                var response = localLambdaOptions.LambdaRuntime.ExecuteLambdaFunction(request);
                
                Console.WriteLine("Captured Log information:");
                Console.WriteLine(response.Logs);
                
                if (response.IsSuccess)
                {
                    Console.WriteLine("Request executed successfully");
                    Console.WriteLine("Response:");
                    Console.WriteLine(response.Response);
                }
                else
                {
                    Console.WriteLine("Request failed to execute");
                    Console.WriteLine($"Error:");
                    Console.WriteLine(response.Error);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Unknown error occurred in the Lambda test tool while executing request.");
                Console.WriteLine($"Error Message: {e.Message}");
            }
            
        }
    }
}
