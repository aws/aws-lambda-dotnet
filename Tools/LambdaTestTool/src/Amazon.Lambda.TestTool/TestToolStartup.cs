using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using Amazon.Lambda.TestTool.Runtime;
using Amazon.Lambda.TestTool.SampleRequests;

namespace Amazon.Lambda.TestTool
{
    public class TestToolStartup
    {
        public class RunConfiguration
        {
            public enum RunMode { Normal, Test};

            /// <summary>
            /// If this is set to Test then that disables any interactive activity or any calls to Environment.Exit which wouldn't work well during a test run.
            /// </summary>
            public RunMode Mode { get; set; } = RunMode.Normal;

            /// <summary>
            /// Allows you to capture the output for tests to example instead of just writing to the console windows.
            /// </summary>
            public TextWriter OutputWriter { get; set; } = Console.Out;
        }

        public static void Startup(string productName, Action<LocalLambdaOptions, bool> uiStartup, string[] args)
        {
            Startup(productName, uiStartup, args, new RunConfiguration());
        }

        public static void Startup(string productName, Action<LocalLambdaOptions, bool> uiStartup, string[] args, RunConfiguration runConfiguration)
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

#if NETCOREAPP3_1
                var targetFramework = "netcoreapp3.1";
#elif NET5_0
                var targetFramework = "net5.0";
#elif NET6_0
                var targetFramework = "net6.0";
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
                runConfiguration.OutputWriter.WriteLine($"Loaded local Lambda runtime from project output {lambdaAssemblyDirectory}");

                if (commandOptions.NoUI)
                {
                    ExecuteWithNoUi(localLambdaOptions, commandOptions, lambdaAssemblyDirectory, runConfiguration);
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
                runConfiguration.OutputWriter.WriteLine($"Invalid command line arguments: {e.Message}");
                runConfiguration.OutputWriter.WriteLine("Use the --help option to learn about the possible command line arguments");
                if (runConfiguration.Mode == RunConfiguration.RunMode.Normal)
                {
                    if (Debugger.IsAttached)
                    {
                        Console.WriteLine("Press any key to exit");
                        Console.ReadKey();
                    }
                    System.Environment.Exit(-1);
                }
            }
            catch (Exception e)
            {
                runConfiguration.OutputWriter.WriteLine($"Unknown error occurred causing process exit: {e.Message}");
                runConfiguration.OutputWriter.WriteLine(e.StackTrace);
                if (runConfiguration.Mode == RunConfiguration.RunMode.Normal)
                {
                    if (Debugger.IsAttached)
                    {
                        Console.WriteLine("Press any key to exit");
                        Console.ReadKey();
                    }
                    System.Environment.Exit(-2);
                }
            }
        }

        
        public static void ExecuteWithNoUi(LocalLambdaOptions localLambdaOptions, CommandLineOptions commandOptions, string lambdaAssemblyDirectory, RunConfiguration runConfiguration)
        {
            runConfiguration.OutputWriter.WriteLine("Executing Lambda function without web interface");
            var lambdaProjectDirectory = Utils.FindLambdaProjectDirectory(lambdaAssemblyDirectory);
            
            string configFile = DetermineConfigFile(commandOptions, lambdaAssemblyDirectory: lambdaAssemblyDirectory, lambdaProjectDirectory: lambdaProjectDirectory);
            LambdaConfigInfo configInfo = LoadLambdaConfigInfo(configFile, commandOptions, lambdaAssemblyDirectory: lambdaAssemblyDirectory, lambdaProjectDirectory: lambdaProjectDirectory, runConfiguration);
            LambdaFunction lambdaFunction = LoadLambdaFunction(configInfo, localLambdaOptions, commandOptions, lambdaAssemblyDirectory: lambdaAssemblyDirectory, lambdaProjectDirectory: lambdaProjectDirectory, runConfiguration);

            string payload = DeterminePayload(localLambdaOptions, commandOptions, lambdaAssemblyDirectory: lambdaAssemblyDirectory, lambdaProjectDirectory: lambdaProjectDirectory, runConfiguration);

            var awsProfile = commandOptions.AWSProfile ?? configInfo.AWSProfile;
            if (!string.IsNullOrEmpty(awsProfile))
            {
                if (new Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain().TryGetProfile(awsProfile, out _))
                {
                    runConfiguration.OutputWriter.WriteLine($"... Setting AWS_PROFILE environment variable to {awsProfile}.");
                }
                else
                {
                    runConfiguration.OutputWriter.WriteLine($"... Warning: Profile {awsProfile} not found in the aws credential store.");
                    awsProfile = null;
                }
            }
            else
            {
                runConfiguration.OutputWriter.WriteLine("... No profile choosen for AWS credentials. The --profile switch can be used to configure an AWS profile.");
            }

            var awsRegion = commandOptions.AWSRegion ?? configInfo.AWSRegion;
            if (!string.IsNullOrEmpty(awsRegion))
            {
                runConfiguration.OutputWriter.WriteLine($"... Setting AWS_REGION environment variable to {awsRegion}.");
            }
            else
            {
                runConfiguration.OutputWriter.WriteLine("... No default AWS region configured. The --region switch can be used to configure an AWS Region.");
            }

            // Create the execution request that will be sent into the LocalLambdaRuntime.
            var request = new ExecutionRequest()
            {
                AWSProfile = awsProfile,
                AWSRegion = awsRegion,
                Payload = payload,
                Function = lambdaFunction
            };

            ExecuteRequest(request, localLambdaOptions, runConfiguration);


            if (runConfiguration.Mode == RunConfiguration.RunMode.Normal && commandOptions.PauseExit)
            {
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
        }

        private static string DetermineConfigFile(CommandLineOptions commandOptions, string lambdaAssemblyDirectory, string lambdaProjectDirectory)
        {
            string configFile = null;
            if (string.IsNullOrEmpty(commandOptions.ConfigFile))
            {
                configFile = Utils.SearchForConfigFiles(lambdaAssemblyDirectory).FirstOrDefault(x => string.Equals(Utils.DEFAULT_CONFIG_FILE, Path.GetFileName(x), StringComparison.OrdinalIgnoreCase));
            }
            else if (Path.IsPathRooted(commandOptions.ConfigFile))
            {
                configFile = commandOptions.ConfigFile;
            }
            else if (File.Exists(Path.Combine(lambdaAssemblyDirectory, commandOptions.ConfigFile)))
            {
                configFile = Path.Combine(lambdaAssemblyDirectory, commandOptions.ConfigFile);
            }
            else if (lambdaProjectDirectory != null && File.Exists(Path.Combine(lambdaProjectDirectory, commandOptions.ConfigFile)))
            {
                configFile = Path.Combine(lambdaProjectDirectory, commandOptions.ConfigFile);
            }

            return configFile;
        }

        private static LambdaConfigInfo LoadLambdaConfigInfo(string configFile, CommandLineOptions commandOptions, string lambdaAssemblyDirectory, string lambdaProjectDirectory, RunConfiguration runConfiguration)
        {
            LambdaConfigInfo configInfo;
            if (configFile != null)
            {
                runConfiguration.OutputWriter.WriteLine($"... Using config file {configFile}");
                configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(configFile);
            }
            else
            {
                // If no config files or function handler are set then we don't know what code to call and must give up.
                if (string.IsNullOrEmpty(commandOptions.FunctionHandler))
                {
                    throw new CommandLineParseException("No config file or function handler specified to test tool is unable to identify the Lambda code to execute.");
                }

                // No config files were found so create a temporary config file and use the function handler value that was set on the command line.
                configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(new LambdaConfigFile
                {
                    FunctionHandler = commandOptions.FunctionHandler,
                    ConfigFileLocation = lambdaProjectDirectory ?? lambdaAssemblyDirectory
                });
            }

            return configInfo;
        }

        private static LambdaFunction LoadLambdaFunction(LambdaConfigInfo configInfo, LocalLambdaOptions localLambdaOptions, CommandLineOptions commandOptions, string lambdaAssemblyDirectory, string lambdaProjectDirectory, RunConfiguration runConfiguration)
        {
            // If no function handler was explicitly set and there is only one function defined in the config file then assume the user wants to debug that function.
            var functionHandler = commandOptions.FunctionHandler;
            if (string.IsNullOrEmpty(commandOptions.FunctionHandler))
            {
                if (configInfo.FunctionInfos.Count == 1)
                {
                    functionHandler = configInfo.FunctionInfos[0].Handler;
                }
                else
                {
                    throw new CommandLineParseException("Project has more then one Lambda function defined. Use the --function-handler switch to identify the Lambda code to execute.");
                }
            }

            LambdaFunction lambdaFunction;
            if (!localLambdaOptions.TryLoadLambdaFuntion(configInfo, functionHandler, out lambdaFunction))
            {
                // The user has explicitly set a function handler value that is not in the config file or CloudFormation template.
                // To support users testing add hoc methods create a temporary config object using explicit function handler value.
                runConfiguration.OutputWriter.WriteLine($"... Info: function handler {functionHandler} is not defined in config file.");
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

            runConfiguration.OutputWriter.WriteLine($"... Using function handler {functionHandler}");
            return lambdaFunction;
        }

        private static string DeterminePayload(LocalLambdaOptions localLambdaOptions, CommandLineOptions commandOptions, string lambdaAssemblyDirectory, string lambdaProjectDirectory, RunConfiguration runConfiguration)
        {
            var payload = commandOptions.Payload;

            bool payloadFileFound = false;
            if (!string.IsNullOrEmpty(payload))
            {
                if (Path.IsPathFullyQualified(payload) && File.Exists(payload))
                {
                    runConfiguration.OutputWriter.WriteLine($"... Using payload with from the file {payload}");
                    payload = File.ReadAllText(payload);
                    payloadFileFound = true;
                }
                else
                {
                    // Look to see if the payload value is a file in
                    // * Directory with user Lambda assemblies.
                    // * Lambda project directory
                    // * Properties directory under the project directory. This is to make it easy to reconcile from the launchSettings.json file.
                    // * Is a saved sample request from the web interface
                    var possiblePaths = new[]
                    {
                        Path.Combine(lambdaAssemblyDirectory, payload),
                        Path.Combine(lambdaProjectDirectory, payload),
                        Path.Combine(lambdaProjectDirectory, "Properties", payload),
                        Path.Combine(localLambdaOptions.GetPreferenceDirectory(false), new SampleRequestManager(localLambdaOptions.GetPreferenceDirectory(false)).GetSaveRequestRelativePath(payload))
                    };
                    foreach (var possiblePath in possiblePaths)
                    {
                        if (File.Exists(possiblePath))
                        {
                            runConfiguration.OutputWriter.WriteLine($"... Using payload with from the file {Path.GetFullPath(possiblePath)}");
                            payload = File.ReadAllText(possiblePath);
                            payloadFileFound = true;
                            break;
                        }
                    }
                }
            }

            if (!payloadFileFound)
            {
                if (!string.IsNullOrEmpty(payload))
                {
                    runConfiguration.OutputWriter.WriteLine($"... Using payload with the value {payload}");
                }
                else
                {
                    runConfiguration.OutputWriter.WriteLine("... No payload configured. If a payload is required set the --payload switch to a file path or a JSON document.");
                }
            }

            return payload;
        }

        private static void ExecuteRequest(ExecutionRequest request, LocalLambdaOptions localLambdaOptions, RunConfiguration runConfiguration)
        {
            try
            {
                var response = localLambdaOptions.LambdaRuntime.ExecuteLambdaFunctionAsync(request).GetAwaiter().GetResult();

                runConfiguration.OutputWriter.WriteLine("Captured Log information:");
                runConfiguration.OutputWriter.WriteLine(response.Logs);

                if (response.IsSuccess)
                {
                    runConfiguration.OutputWriter.WriteLine("Request executed successfully");
                    runConfiguration.OutputWriter.WriteLine("Response:");
                    runConfiguration.OutputWriter.WriteLine(response.Response);
                }
                else
                {
                    runConfiguration.OutputWriter.WriteLine("Request failed to execute");
                    runConfiguration.OutputWriter.WriteLine($"Error:");
                    runConfiguration.OutputWriter.WriteLine(response.Error);
                }
            }
            catch (Exception e)
            {
                runConfiguration.OutputWriter.WriteLine("Unknown error occurred in the Lambda test tool while executing request.");
                runConfiguration.OutputWriter.WriteLine($"Error Message: {e.Message}");
                runConfiguration.OutputWriter.WriteLine(e.StackTrace);
            }
        }
    }
}
