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
                Console.WriteLine("Use the --help option to learn about the possible command line arguments");
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
            Console.WriteLine("Executing Lambda function without web interface");
            var lambdaProjectDirectory = Utils.FindLambdaProjectDirectory(lambdaAssemblyDirectory);

            
            string configFile = null;
            if (string.IsNullOrEmpty(commandOptions.ConfigFile))
            {
                configFile = Utils.SearchForConfigFiles(lambdaAssemblyDirectory).FirstOrDefault(x => string.Equals(Utils.DEFAULT_CONFIG_FILE, Path.GetFileName(x), StringComparison.OrdinalIgnoreCase));
            }
            else if (Path.IsPathRooted(commandOptions.ConfigFile))
            {
                configFile = commandOptions.ConfigFile;
            }
            else if(File.Exists(Path.Combine(lambdaAssemblyDirectory, commandOptions.ConfigFile)))
            {
                configFile = Path.Combine(lambdaAssemblyDirectory, commandOptions.ConfigFile);
            }
            else if (lambdaProjectDirectory != null && File.Exists(Path.Combine(lambdaProjectDirectory, commandOptions.ConfigFile)))
            {
                configFile = Path.Combine(lambdaProjectDirectory, commandOptions.ConfigFile);
            }

            LambdaConfigInfo configInfo;
            if (configFile != null)
            {
                Console.WriteLine($"... Using config file {configFile}");
                configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(configFile);
            }
            else
            {
                // If no config files or function handler are set then we don't know what code to call and must give up.
                if(string.IsNullOrEmpty(commandOptions.FunctionHandler))
                {
                    throw new CommandLineParseException("No config file or function handler specified to test tool is unable to identify the Lambda code to execute.");
                }

                // No config files were found so create a temporary config file and use the function handler value that was set on the command line.
                configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(new LambdaConfigFile
                {
                    FunctionHandler = commandOptions.FunctionHandler,
                    ConfigFileLocation = Utils.FindLambdaProjectDirectory(lambdaAssemblyDirectory) ?? lambdaAssemblyDirectory
                });
            }

            // If no function handler was explicitly set and there is only one function defined in the config file then assume the user wants to debug that function.
            var functionHandler = commandOptions.FunctionHandler;
            if (string.IsNullOrEmpty(commandOptions.FunctionHandler))
            {
                if(configInfo.FunctionInfos.Count == 1)
                {
                    functionHandler = configInfo.FunctionInfos[0].Handler;
                }
                else
                {
                    throw new CommandLineParseException("Project has more then one Lambda function defined. Use the --function-handler switch to identify the Lambda code to execute.");
                }
            }

            LambdaFunction lambdaFunction;
            if(!localLambdaOptions.TryLoadLambdaFuntion(configInfo, functionHandler, out lambdaFunction))
            {
                // The user has explicitly set a function handler value that is not in the config file or CloudFormation template.
                // To support users testing add hoc methods create a temporary config object using explicit function handler value.
                Console.WriteLine($"... Info: function handler {functionHandler} is not defined in config file.");
                var temporaryConfigInfo = LambdaDefaultsConfigFileParser.LoadFromFile(new LambdaConfigFile
                {
                    FunctionHandler = functionHandler,
                    ConfigFileLocation = Utils.FindLambdaProjectDirectory(lambdaAssemblyDirectory) ?? lambdaAssemblyDirectory
                });

                temporaryConfigInfo.AWSProfile = configInfo.AWSProfile;
                temporaryConfigInfo.AWSRegion = configInfo.AWSRegion;
                configInfo = temporaryConfigInfo;
                lambdaFunction = localLambdaOptions.LoadLambdaFuntion(configInfo, functionHandler);
            }


            Console.WriteLine($"... Using function handler {functionHandler}");

            var payload = commandOptions.Payload;
            // Look to see if the payload value is a file in
            // * Directory with user Lambda assemblies.
            // * Lambda project directory
            // * Properties directory under the project directory. This is to make it easy to reconcile from the launchSettings.json file.
            var possiblePaths = new[] { Path.Combine(lambdaAssemblyDirectory, payload), Path.Combine(lambdaProjectDirectory, payload), Path.Combine(lambdaProjectDirectory, "Properties", payload) };
            bool payloadFileFound = false;
            foreach(var possiblePath in possiblePaths)
            {
                if(File.Exists(possiblePath))
                {
                    Console.WriteLine($"... Using payload with from the file {Path.GetFullPath(possiblePath)}");
                    payload = File.ReadAllText(possiblePath);
                    payloadFileFound = true;
                    break;
                }
            }

            if(!payloadFileFound)
            {
                if (!string.IsNullOrEmpty(payload))
                {
                    Console.WriteLine($"... Using payload with the value {payload}");
                }
                else
                {
                    Console.WriteLine("... No payload configured. If a payload is required set the --payload switch to a file path or a JSON document.");
                }
            }

            // Create the execution request that will be sent into the LocalLambdaRuntime.
            var request = new ExecutionRequest()
            {
                AWSProfile = commandOptions.AWSProfile ?? configInfo.AWSProfile,
                AWSRegion = commandOptions.AWSRegion ?? configInfo.AWSRegion,
                Payload = payload,
                Function = lambdaFunction
            };

            if(!string.IsNullOrEmpty(request.AWSProfile))
            {
                Console.WriteLine($"... Setting AWS_PROFILE environment variable to {request.AWSProfile}");
            }
            else
            {
                Console.WriteLine("... No profile choosen for AWS credentials. The --profile switch can be used to configure an AWS profile.");
            }

            if (!string.IsNullOrEmpty(request.AWSRegion))
            {
                Console.WriteLine($"... Setting AWS_REGION environment variable to {request.AWSRegion}.");
            }
            else
            {
                Console.WriteLine("... No default AWS region configured. The --region switch can be used to configure an AWS Region.");
            }


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
                Console.WriteLine(e.StackTrace);
            }

            if(commandOptions.PauseExit)
            {
                Console.WriteLine("Press any key to exist");
                Console.ReadKey();
            }
        }
    }
}
