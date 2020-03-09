using System;
using System.IO;

using LitJson;

using Amazon.Lambda.TestTool.Runtime;
using Amazon.Lambda.TestTool.WebTester;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Amazon.Lambda.TestTool
{
    class Program
    {
        static async Task Main(string[] args)
        {            
            try
            {
                Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", "AWS_DOTNET_LAMDBA_TEST_TOOL_" + Utils.DetermineToolVersion());

                PrintToolTitle();
                
                var commandOptions = CommandLineOptions.Parse(args);
                
                var options = new LocalLambdaOptions()
                {
                    Port = commandOptions.Port
                };

                var path = commandOptions.Path ?? Directory.GetCurrentDirectory();

                // Check to see if running in debug mode from this project's directory which means the test tool is being debugged.
                // To make debugging easier pick one of the test Lambda projects.
                if (path.EndsWith("Amazon.Lambda.TestTool"))
                {
                    path = Path.Combine(path, "../LambdaFunctions/S3EventFunction/bin/Debug/netcoreapp2.1");
                }
                // If running in the project directory select the build directory so the deps.json file can be found.
                else if (Utils.IsProjectDirectory(path))
                {
                    path = Path.Combine(path, "bin/Debug/netcoreapp2.1");
                }

                options.LambdaRuntime = LocalLambdaRuntime.Initialize(path);
                Console.WriteLine($"Loaded local Lambda runtime from project output {path}");

                // Look for aws-lambda-tools-defaults.json or other config files.
                options.LambdaConfigFiles = await SearchForConfigFiles(path);

                // Start the test tool web server.
                Startup.LaunchWebTester(options, !commandOptions.NoLaunchWindow);
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
            catch(Exception e)
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

        static async Task<IList<string>> SearchForConfigFiles(string lambdaFunctionDirectory)
        {
            var configFiles = new List<string>();

            // Look for JSON files that are .NET Lambda config files like aws-lambda-tools-defaults.json. The parameter
            // lambdaFunctionDirectory will be set to the build directory so the search goes up the directory hierarchy.
            do
            {
                foreach (var file in Directory.GetFiles(lambdaFunctionDirectory, "*.json", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var data = JsonMapper.ToObject(await File.ReadAllTextAsync(file));

                        if (data.IsObject &&
                            data.ContainsKey("framework") && data["framework"].ToString().StartsWith("netcoreapp") &&
                            (data.ContainsKey("function-handler") || data.ContainsKey("template")))
                        {
                            Console.WriteLine($"Found Lambda config file {file}");
                            configFiles.Add(file);
                        }
                    }
                    catch
                    {
                        Console.WriteLine($"Error parsing JSON file: {file}");
                    }
                }

                lambdaFunctionDirectory = Directory.GetParent(lambdaFunctionDirectory)?.FullName;

            } while (lambdaFunctionDirectory != null && configFiles.Count == 0);

            return configFiles;
        }
        
        
        static void PrintToolTitle()
        {
            var sb = new StringBuilder(Constants.PRODUCT_NAME);
            var version = Utils.DetermineToolVersion();
            if (!string.IsNullOrEmpty(version))
            {
                sb.Append($" ({version})");
            }

            Console.WriteLine(sb.ToString());
        }
    }
}