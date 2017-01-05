using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Diagnostics;

namespace Amazon.Lambda.Tools
{
    /// <summary>
    /// Wrapper around the dotnet cli used to execute the publish command.
    /// </summary>
    public class DotNetCLIWrapper
    {
        string _workingDirectory;
        IToolLogger _logger;

        public DotNetCLIWrapper(IToolLogger logger, string workingDirectory)
        {
            this._logger = logger;
            this._workingDirectory = workingDirectory;
        }

        /// <summary>
        /// Generates deployment manifest for staged content
        /// </summary>
        /// <param name="outputLocation"></param>
        /// <param name="targetFramework"></param>
        /// <param name="iisAppPath"></param>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        public int Publish(LambdaToolsDefaults defaults, string projectLocation, string outputLocation, string targetFramework, string configuration)
        {
            if (Directory.Exists(outputLocation))
            {
                try
                {
                    Directory.Delete(outputLocation, true);
                    _logger?.WriteLine("Deleted previous publish folder");
                }
                catch (Exception e)
                {
                    _logger?.WriteLine($"Warning unable to delete previous publish folder: {e.Message}");
                }
            }

            _logger?.WriteLine($"... invoking 'dotnet publish', working folder '{outputLocation}'");

            var dotnetCLI = FindExecutableInPath("dotnet.exe");
            if (dotnetCLI == null)
                dotnetCLI = FindExecutableInPath("dotnet");
            if (string.IsNullOrEmpty(dotnetCLI))
                throw new Exception("Failed to locate dotnet CLI executable. Make sure the dotnet CLI is installed in the environment PATH.");

            StringBuilder arguments = new StringBuilder("publish");
            if (!string.IsNullOrEmpty(projectLocation))
            {
                arguments.Append($" \"{Utilities.DetemineProjectLocation(this._workingDirectory, projectLocation)}\"");
            }
            if (!string.IsNullOrEmpty(outputLocation))
            {
                arguments.Append($" --output \"{outputLocation}\"");
            }

            if (!string.IsNullOrEmpty(configuration))
            {
                arguments.Append($" --configuration \"{configuration}\"");
            }

            if (!string.IsNullOrEmpty(targetFramework))
            {
                arguments.Append($" --framework \"{targetFramework}\"");
            }


            var psi = new ProcessStartInfo
            {
                FileName = dotnetCLI,
                Arguments = arguments.ToString(),
                WorkingDirectory = this._workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var handler = (DataReceivedEventHandler)((o, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;
                _logger?.WriteLine("... publish: " + e.Data);
            });

            int exitCode;
            using (var proc = new Process())
            {
                proc.StartInfo = psi;
                proc.Start();


                proc.ErrorDataReceived += handler;
                proc.OutputDataReceived += handler;
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                proc.EnableRaisingEvents = true;

                proc.WaitForExit();

                exitCode = proc.ExitCode;
            }

            if (exitCode == 0)
            {
                FlattenKnownPlatformDependencies(defaults, Utilities.DetemineProjectLocation(this._workingDirectory, projectLocation), outputLocation);

                var chmodPath = FindExecutableInPath("chmod");
                if (!string.IsNullOrEmpty(chmodPath) && File.Exists(chmodPath))
                {
                    // as we are not invoking through a shell, which would handle
                    // wildcard expansion for us, we need to invoke per-file
                    var dllFiles = Directory.GetFiles(outputLocation, "*.dll", SearchOption.TopDirectoryOnly);
                    foreach (var dllFile in dllFiles)
                    {
                        var dllFilename = Path.GetFileName(dllFile);
                        var psiChmod = new ProcessStartInfo
                        {
                            FileName = chmodPath,
                            Arguments = "+r " + dllFilename,
                            WorkingDirectory = outputLocation,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using (var proc = new Process())
                        {
                            proc.StartInfo = psiChmod;
                            proc.Start();

                            proc.ErrorDataReceived += handler;
                            proc.OutputDataReceived += handler;
                            proc.BeginOutputReadLine();
                            proc.BeginErrorReadLine();

                            proc.EnableRaisingEvents = true;
                            proc.WaitForExit();

                            if (proc.ExitCode == 0)
                            {
                                this._logger?.WriteLine($"Changed permissions on published dll (chmod +r {dllFilename}).");
                            }
                        }
                    }
                }
            }

            return exitCode;
        }

        static IList<string> KNOWN_PLATFORM_DEPENDENCIES = new List<string>
        {
            $"runtimes{Path.DirectorySeparatorChar}unix{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}netstandard1.3{Path.DirectorySeparatorChar}System.Data.SqlClient.dll",
            $"runtimes{Path.DirectorySeparatorChar}unix{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}netstandard1.3{Path.DirectorySeparatorChar}System.IO.Pipes.dll"

        };

        private void FlattenKnownPlatformDependencies(LambdaToolsDefaults defaults, string projectLocation, string publishLocation)
        {
            var listOfDependencies = KNOWN_PLATFORM_DEPENDENCIES;

            var extraDependences = defaults["additional-files"] as string[];
            if (extraDependences != null)
            {
                foreach (var item in extraDependences)
                    listOfDependencies.Add(item);
            }

            foreach (var relativePath in listOfDependencies)
            {
                var fileName = Path.GetFileName(relativePath);
                string source;
                if (Path.IsPathRooted(relativePath))
                    source = relativePath;
                else
                    source = Path.Combine(publishLocation, relativePath);
                var target = Path.Combine(publishLocation, fileName);
                if (File.Exists(source) && !File.Exists(target))
                {
                    File.Copy(source, target);
                    _logger?.WriteLine($"... publish: Adding additional file {relativePath}");
                }
            }
        }

        private DotNetCLIWrapper() { }

        /// <summary>
        /// A collection of known paths for common utilities that are usually not found in the path
        /// </summary>
        static readonly IDictionary<string, string> KNOWN_LOCATIONS = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"dotnet.exe", @"C:\Program Files\dotnet\dotnet.exe" },
            {"chmod", @"/bin/chmod" },
            {"zip", @"/usr/bin/zip" }
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
    }
}