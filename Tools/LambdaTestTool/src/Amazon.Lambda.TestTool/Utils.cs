﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.TestTool.Runtime;

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
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
            if (Directory.GetFiles(directory, "*.csproj").Length > 0 ||
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
                        else if (!string.IsNullOrEmpty(configFile.Template) && File.Exists(Path.Combine(lambdaFunctionDirectory, configFile.Template)))
                        {
                            var config = LambdaDefaultsConfigFileParser.LoadFromFile(configFile);
                            if (config.FunctionInfos?.Count > 0)
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

        /// <summary>
        /// Attempt to pretty print the input string. If pretty print fails return back the input string in its original form.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string TryPrettyPrintJson(string data)
        {
            try
            {
                var doc = JsonDocument.Parse(data);
                var prettyPrintJson = System.Text.Json.JsonSerializer.Serialize(doc, new JsonSerializerOptions()
                {
                    WriteIndented = true
                });
                return prettyPrintJson;
            }
            catch (Exception)
            {
                return data;
            }
        }

        public static bool IsExecutableAssembliesSupported
        {
            get
            {
#if NET6_0_OR_GREATER
                return true;
#else
                return false;
#endif
            }
        }

        public static string DetermineLaunchUrl(string host, int port, string defaultHost)
        {
            if (!IPAddress.TryParse(host, out _))
                // Any host other than explicit IP will be redirected to default host (i.e. localhost)
                return $"http://{defaultHost}:{port}";

            return $"http://{host}:{port}";
        }

        /// <summary>
        /// From the debug directory look to see where the latest compilation occurred for debugging. This can vary between the
        /// root debug directory and the runtime specific subfolders. Starting with .NET 7 SDK if ready 2 run is enabled then 
        /// project compiles into the runtime specific folder.
        /// </summary>
        /// <param name="debugDirectory"></param>
        /// <returns></returns>
        public static string SearchLatestCompilationDirectory(string debugDirectory)
        {
            var depsFile = new DirectoryInfo(debugDirectory).GetFiles("*.deps.json", SearchOption.AllDirectories)
                                    .OrderByDescending(x => x.LastWriteTime).ToList();

            if (depsFile.Count == 0)
                return debugDirectory;

            return depsFile[0].Directory.FullName;            
        }

        /// <summary>
        /// Return tool path compatible with specific IDE for debug purposes
        /// </summary>
        /// <param name="ideName"></param>
        /// <returns></returns>
        public static string GetToolPath(string ideName)
        {
            ideName = ideName.Trim().ToLower();
            if (ideName == "vs" || ideName == "vscode")
            {
                return Process.GetCurrentProcess().MainModule!.FileName;
            }

            return Assembly.GetEntryAssembly()!.Location;
        }
    }
}
