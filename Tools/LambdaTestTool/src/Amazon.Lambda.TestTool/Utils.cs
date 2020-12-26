using Amazon.Lambda.TestTool.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Amazon.Lambda.TestTool
{
    public static class Utils
    {
        public const string DEFAULT_CONFIG_FILE = "aws-lambda-tools-defaults.json";

        public static string DetermineToolVersion()
        {
            AssemblyInformationalVersionAttribute attribute = null;
            try
            {
                var assembly = Assembly.GetEntryAssembly();
                if (assembly == null)
                    return null;
                attribute = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            }
            catch (Exception)
            {
                // ignored
            }

            return attribute?.InformationalVersion;
        }
        
        /// <summary>
        /// A collection of known paths for common utilities that are usually not found in the path
        /// </summary>
        static readonly IDictionary<string, string> KNOWN_LOCATIONS = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"dotnet.exe", @"C:\Program Files\dotnet\dotnet.exe" },
            {"dotnet", @"/usr/local/share/dotnet/dotnet" }
        };

        /// <summary>
        /// Search the path environment variable for the command given.
        /// </summary>
        /// <param name="command">The command to search for in the path</param>
        /// <returns>The full path to the command if found otherwise it will return null</returns>
        public static string FindExecutableInPath(string command)
        {

            if (File.Exists(command))
                return Path.GetFullPath(command);

            if (string.Equals(command, "dotnet.exe"))
            {
                if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    command = "dotnet";
                }

                var mainModule = Process.GetCurrentProcess().MainModule;
                if (!string.IsNullOrEmpty(mainModule?.FileName)
                    && Path.GetFileName(mainModule.FileName).Equals(command, StringComparison.OrdinalIgnoreCase))
                {
                    return mainModule.FileName;
                }
            }

            Func<string, string> quoteRemover = x =>
            {
                if (x.StartsWith("\""))
                    x = x.Substring(1);
                if (x.EndsWith("\""))
                    x = x.Substring(0, x.Length - 1);
                return x;
            };

            var envPath = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in envPath.Split(Path.PathSeparator))
            {
                try
                {
                    var fullPath = Path.Combine(quoteRemover(path), command);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
                catch (Exception)
                {
                    // Catch exceptions and continue if there are invalid characters in the user's path.
                }
            }

            if (KNOWN_LOCATIONS.ContainsKey(command) && File.Exists(KNOWN_LOCATIONS[command]))
                return KNOWN_LOCATIONS[command];

            return null;
        }

        public static bool IsProjectDirectory(string directory)
        {
            if(Directory.GetFiles(directory, "*.csproj").Length > 0 ||
                Directory.GetFiles(directory, "*.fsproj").Length > 0 ||
                Directory.GetFiles(directory, "*.vbproj").Length > 0)
                {
                return true;
            }

            return false;
        }

        public static string FindLambdaProjectDirectory(string lambdaAssemblyDirectory)
        {
            if (string.IsNullOrEmpty(lambdaAssemblyDirectory))
                return null;
            
            if (IsProjectDirectory(lambdaAssemblyDirectory))
                return lambdaAssemblyDirectory;
            
            return FindLambdaProjectDirectory(Directory.GetParent(lambdaAssemblyDirectory)?.FullName);
        }

        public static IList<string> SearchForConfigFiles(string lambdaFunctionDirectory)
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
                        var configFile = System.Text.Json.JsonSerializer.Deserialize<LambdaConfigFile>(File.ReadAllText(file), new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        configFile.ConfigFileLocation = file;

                        if (!string.IsNullOrEmpty(configFile.DetermineHandler()))
                        {
                            Console.WriteLine($"Found Lambda config file {file}");
                            configFiles.Add(file);
                        }
                        else if(!string.IsNullOrEmpty(configFile.Template) && File.Exists(Path.Combine(lambdaFunctionDirectory, configFile.Template)))
                        {
                            var config = LambdaDefaultsConfigFileParser.LoadFromFile(configFile);
                            if(config.FunctionInfos?.Count > 0)
                            {
                                Console.WriteLine($"Found Lambda config file {file}");
                                configFiles.Add(file);
                            }
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


        public static void PrintToolTitle(string productName)
        {
            var sb = new StringBuilder(productName);
            var version = Utils.DetermineToolVersion();
            if (!string.IsNullOrEmpty(version))
            {
                sb.Append($" ({version})");
            }

            Console.WriteLine(sb.ToString());
        }
    }
}