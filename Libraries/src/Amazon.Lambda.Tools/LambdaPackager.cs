using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.IO.Compression;
using System.Runtime.InteropServices;

using Amazon.CloudFormation.Model;
using Amazon.Util;
using System.Text;

using ThirdParty.Json.LitJson;

namespace Amazon.Lambda.Tools
{
    /// <summary>
    /// This class will create the lambda zip package that can be upload to Lambda for deployment.
    /// </summary>
    public static class LambdaPackager
    {
        static IDictionary<string, Version> NETSTANDARD_LIBRARY_VERSIONS = new Dictionary<string, Version>
        {
            { "netcoreapp1.0", Version.Parse("1.6.0") },
            { "netcoreapp1.1", Version.Parse("1.6.1") }
        };

        /// <summary>
        /// Execute the dotnet publish command and zip up the resulting publish folder.
        /// </summary>
        /// <param name="defaults"></param>
        /// <param name="logger"></param>
        /// <param name="workingDirectory"></param>
        /// <param name="projectLocation"></param>
        /// <param name="targetFramework"></param>
        /// <param name="configuration"></param>
        /// <param name="disableVersionCheck"></param>
        /// <param name="publishLocation"></param>
        /// <param name="zipArchivePath"></param>
        public static bool CreateApplicationBundle(LambdaToolsDefaults defaults, IToolLogger logger, string workingDirectory, 
            string projectLocation, string configuration, string targetFramework, bool disableVersionCheck,
            out string publishLocation, ref string zipArchivePath)
        {
            var cli = new DotNetCLIWrapper(logger, workingDirectory);

            publishLocation = Utilities.DeterminePublishLocation(workingDirectory, projectLocation, configuration, targetFramework);
            logger?.WriteLine("Executing publish command");
            if (cli.Publish(defaults, projectLocation, publishLocation, targetFramework, configuration) != 0)
                return false;

            var buildLocation = Utilities.DetermineBuildLocation(workingDirectory, projectLocation, configuration, targetFramework);

            // This is here for legacy reasons. Some older versions of the dotnet CLI were not 
            // copying the deps.json file into the publish folder.
            foreach(var file in Directory.GetFiles(buildLocation, "*.deps.json", SearchOption.TopDirectoryOnly))
            {
                var destinationPath = Path.Combine(publishLocation, Path.GetFileName(file));
                if(!File.Exists(destinationPath))
                    File.Copy(file, destinationPath);
            }

            bool flattenRuntime = false;
            var depsJsonTargetNode = GetDepsJsonTargetNode(logger, publishLocation);
            // If there is no target node then this means the tool is being used on a future version of .NET Core
            // then was available when the this tool was written. Go ahead and continue the deployment with warnings so the
            // user can see if the future version will work.
            if (depsJsonTargetNode != null)
            {
                // Make sure the project is not pulling in dependencies requiring a later version of .NET Core then the declared target framework
                if (!ValidateDependencies(logger, targetFramework, depsJsonTargetNode, disableVersionCheck))
                    return false;

                // Flatten the runtime folder which reduces the package size by not including native dependencies
                // for other platforms.
                flattenRuntime = FlattenRuntimeFolder(logger, publishLocation, depsJsonTargetNode);
            }

            if (zipArchivePath == null)
                zipArchivePath = Path.Combine(Directory.GetParent(publishLocation).FullName, new DirectoryInfo(workingDirectory).Name + ".zip");

            zipArchivePath = Path.GetFullPath(zipArchivePath);
            logger?.WriteLine($"Zipping publish folder {publishLocation} to {zipArchivePath}");
            if (File.Exists(zipArchivePath))
                File.Delete(zipArchivePath);

            var zipArchiveParentDirectory = Path.GetDirectoryName(zipArchivePath);
            if (!Directory.Exists(zipArchiveParentDirectory))
            {
                logger?.WriteLine($"Creating directory {zipArchiveParentDirectory}");
                new DirectoryInfo(zipArchiveParentDirectory).Create();
            }


#if NETCORE
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                BundleWithDotNetCompression(zipArchivePath, publishLocation, flattenRuntime, logger);
            }
            else
            {
                // Use the native zip utility if it exist which will maintain linux/osx file permissions
                var zipCLI = DotNetCLIWrapper.FindExecutableInPath("zip");
                if (!string.IsNullOrEmpty(zipCLI))
                {
                    BundleWithZipCLI(zipCLI, zipArchivePath, publishLocation, flattenRuntime, logger);
                }
                else
                {
                    throw new LambdaToolsException("Failed to find the \"zip\" utility program in path. This program is required to maintain Linux file permissions in the zip archive.", LambdaToolsException.ErrorCode.FailedToFindZipProgram);
                }
            }
#else
            BundleWithDotNetCompression(zipArchivePath, publishLocation, flattenRuntime, logger);
#endif



            return true;
        }

        /// <summary>
        /// Return the targets node which declares all the dependencies for the project along with the dependency's dependencies.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="publishLocation"></param>
        /// <returns></returns>
        private static JsonData GetDepsJsonTargetNode(IToolLogger logger, string publishLocation)
        {
            var depsJsonFilepath = Directory.GetFiles(publishLocation, "*.deps.json", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (!File.Exists(depsJsonFilepath))
            {
                logger?.WriteLine($"Missing deps.json file. Skipping flattening runtime folder because {depsJsonFilepath} is an unrecognized format");
                return null;
            }

            var depsRootData = JsonMapper.ToObject(File.ReadAllText(depsJsonFilepath));
            var runtimeTargetNode = depsRootData["runtimeTarget"];
            if (runtimeTargetNode == null)
            {
                logger?.WriteLine($"Missing runtimeTarget node. Skipping flattening runtime folder because {depsJsonFilepath} is an unrecognized format");
                return null;
            }

            string runtimeTarget;
            if (runtimeTargetNode.IsString)
            {
                runtimeTarget = runtimeTargetNode.ToString();
            }
            else
            {
                runtimeTarget = runtimeTargetNode["name"]?.ToString();
            }

            if (runtimeTarget == null)
            {
                logger?.WriteLine($"Missing runtimeTarget name. Skipping flattening runtime folder because {depsJsonFilepath} is an unrecognized format");
                return null;
            }

            var target = depsRootData["targets"]?[runtimeTarget];
            if (target == null)
            {
                logger?.WriteLine($"Missing targets node. Skipping flattening runtime folder because {depsJsonFilepath} is an unrecognized format");
                return null;
            }

            return target;
        }

        /// <summary>
        /// Check to see if any of the dependencies listed in the deps.json file are pulling in later version of NETStandard.Library
        /// then the target framework supports.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="targetFramework"></param>
        /// <param name="depsJsonTargetNode"></param>
        /// <param name="disableVersionCheck"></param>
        /// <returns></returns>
        private static bool ValidateDependencies(IToolLogger logger, string targetFramework, JsonData depsJsonTargetNode, bool disableVersionCheck)
        {
            Version maxNETStandardLibraryVersion;
            // If we don't know the NETStandard.Library NuGet package version then skip validation. This is to handle
            // the case we are packaging up for a future target framework verion then this version of the tooling knows about.
            // Skip validation so the tooling doesn't get in the way.
            if (!NETSTANDARD_LIBRARY_VERSIONS.TryGetValue(targetFramework, out maxNETStandardLibraryVersion))
                return true;

            var dependenciesUsingNETStandard = new List<string>();
            Version referencedNETStandardLibrary = null;

            var errorLevel = disableVersionCheck ? "Warning" : "Error";

            foreach (KeyValuePair<string, JsonData> dependencyNode in depsJsonTargetNode)
            {
                var nameAndVersion = dependencyNode.Key.Split('/');
                if (nameAndVersion.Length != 2)
                    continue;

                if (string.Equals(nameAndVersion[0], "netstandard.library", StringComparison.OrdinalIgnoreCase))
                {
                    if(!Version.TryParse(nameAndVersion[1], out referencedNETStandardLibrary))
                    {
                        logger.WriteLine($"{errorLevel} parsing version number for declared NETStandard.Library: {nameAndVersion[1]}");
                        return true;
                    }
                }
                // Collect the dependencies that are pulling in the NETStandard.Library metapackage
                else
                {
                    var subDependencies = dependencyNode.Value["dependencies"] as JsonData;
                    if (subDependencies != null)
                    {
                        foreach (KeyValuePair<string, JsonData> subDependency in subDependencies)
                        {
                            if (string.Equals(subDependency.Key, "netstandard.library", StringComparison.OrdinalIgnoreCase))
                            {
                                dependenciesUsingNETStandard.Add(nameAndVersion[0] + " : " + nameAndVersion[1]);
                                break;
                            }
                        }
                    }
                }
            }

            // If true the project is pulling in a new version of NETStandard.Library then the target framework supports.
            if(referencedNETStandardLibrary != null && maxNETStandardLibraryVersion < referencedNETStandardLibrary)
            {
                logger?.WriteLine($"{errorLevel}: Project is referencing NETStandard.Library version {referencedNETStandardLibrary.ToString()}. Max version supported by {targetFramework} is {maxNETStandardLibraryVersion.ToString()}.");

                // See if we can find the target framework that does support the version the project is pulling in.
                // This can help the user know what framework their dependencies are targeting instead of understanding NuGet version numbers.
                var matchingTargetFramework = NETSTANDARD_LIBRARY_VERSIONS.FirstOrDefault(x =>
                {
                    return x.Value.Equals(referencedNETStandardLibrary);
                });

                if(!string.IsNullOrEmpty(matchingTargetFramework.Key))
                {
                    logger?.WriteLine($"{errorLevel}: NETStandard.Library {referencedNETStandardLibrary.ToString()} is used for target framework {matchingTargetFramework.Key}.");
                }

                if (dependenciesUsingNETStandard.Count != 0)
                {
                    logger?.WriteLine($"{errorLevel}: Check the following dependencies for versions compatible with {targetFramework}:");
                    foreach(var dependency in dependenciesUsingNETStandard)
                    {
                        logger?.WriteLine($"{errorLevel}: \t{dependency}");
                    }
                }

                // If disable version check is true still write the warning messages 
                // but return true to continue deployment.
                return false || disableVersionCheck;
            }


            return true;
        }

        /// <summary>
        /// Process the runtime folder from the dotnet publish to flatten the platform specific dependencies to the
        /// root.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="publishLocation"></param>
        /// <param name="depsJsonTargetNode"></param>
        /// <returns>
        /// Returns true if flattening was successful. If the publishing folder changes in the future then flattening might fail. 
        /// In that case we want to publish the archive untouched so the tooling doesn't get in the way and let the user see if the  
        /// Lambda runtime has been updated to support the future changes. Warning messages will be written in case of failures.
        /// </returns>
        private static bool FlattenRuntimeFolder(IToolLogger logger, string publishLocation, JsonData depsJsonTargetNode)
        {

            bool flattenAny = false;
            // Copy file function if the file hasn't already copied.
            var copyFileIfNotExist = new Action<string>(sourceRelativePath =>
            {
                var sourceFullPath = Path.Combine(publishLocation, sourceRelativePath);
                var targetFullPath = Path.Combine(publishLocation, Path.GetFileName(sourceFullPath));

                // Skip the copy if it has already been copied.
                if (File.Exists(targetFullPath))
                    return;

                // Only write the log message about flattening if we are actually going to flatten anything.
                if(!flattenAny)
                {
                    logger?.WriteLine("Flattening platform specific dependencies");
                    flattenAny = true;
                }

                logger?.WriteLine($"... flatten: {sourceRelativePath}");
                File.Copy(sourceFullPath, targetFullPath);
            });

            var runtimeHierarchy = CalculateRuntimeHierarchy();
            // Loop through all the valid runtimes in precedence order so we copy over the first match
            foreach (var runtime in runtimeHierarchy)
            {
                foreach (KeyValuePair<string, JsonData> dependencyNode in depsJsonTargetNode)
                {
                    var depRuntimeTargets = dependencyNode.Value["runtimeTargets"];
                    if (depRuntimeTargets == null)
                        continue;

                    foreach (KeyValuePair<string, JsonData> depRuntimeTarget in depRuntimeTargets)
                    {
                        var rid = depRuntimeTarget.Value["rid"]?.ToString();

                        if(string.Equals(rid, runtime, StringComparison.Ordinal))
                        {
                            copyFileIfNotExist(depRuntimeTarget.Key);
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Compute the hierarchy of runtimes to search for platform dependencies.
        /// </summary>
        /// <returns></returns>
        private static IList<string> CalculateRuntimeHierarchy()
        {
            var runtimeHierarchy = new List<string>();

            using (var stream = typeof(LambdaPackager).GetTypeInfo().Assembly.GetManifestResourceStream(Constants.RUNTIME_HIERARCHY))
            using (var reader = new StreamReader(stream))
            {
                var rootData = JsonMapper.ToObject(reader.ReadToEnd());
                var runtimes = rootData["runtimes"];

                // Use a queue to do a breadth first search through the list of runtimes.
                var queue = new Queue<string>();
                queue.Enqueue(Constants.RUNTIME_HIERARCHY_STARTING_POINT);

                while(queue.Count > 0)
                {
                    var runtime = queue.Dequeue();
                    if (runtimeHierarchy.Contains(runtime))
                        continue;

                    runtimeHierarchy.Add(runtime);

                    var imports = runtimes[runtime]["#import"];
                    if (imports != null)
                    {
                        foreach (JsonData importedRuntime in imports)
                        {
                            queue.Enqueue(importedRuntime.ToString());
                        }
                    }
                }
            }

            return runtimeHierarchy;
        }


        /// <summary>
        /// Get the list of files from the publish folder that should be added to the zip archive.
        /// This will skip all files in the runtimes folder because they have already been flatten to the root.
        /// </summary>
        /// <param name="publishLocation"></param>
        /// <param name="flattenRuntime">If true the runtimes folder will be flatten</param>
        /// <returns></returns>
        private static IDictionary<string, string> GetFilesToIncludeInArchive(string publishLocation, bool flattenRuntime)
        {
            string RUNTIME_FOLDER_PREFIX = "runtimes" + Path.DirectorySeparatorChar;

            var includedFiles = new Dictionary<string, string>();
            var allFiles = Directory.GetFiles(publishLocation, "*.*", SearchOption.AllDirectories);
            foreach (var file in allFiles)
            {
                var relativePath = file.Substring(publishLocation.Length);
                if (relativePath[0] == Path.DirectorySeparatorChar)
                    relativePath = relativePath.Substring(1);

                if (flattenRuntime && relativePath.StartsWith(RUNTIME_FOLDER_PREFIX))
                    continue;

                includedFiles[relativePath] = file;
            }

            return includedFiles;
        }

        /// <summary>
        /// Zip up the publish folder using the .NET compression libraries. This is what is used when run on Windows.
        /// </summary>
        /// <param name="zipArchivePath">The path and name of the zip archive to create.</param>
        /// <param name="publishLocation">The location to be bundled.</param>
        /// <param name="flattenRuntime">If true the runtimes folder will be flatten</param>
        /// <param name="logger">Logger instance.</param>
        private static void BundleWithDotNetCompression(string zipArchivePath, string publishLocation, bool flattenRuntime, IToolLogger logger)
        {
            using (var zipArchive = ZipFile.Open(zipArchivePath, ZipArchiveMode.Create))
            {
                var includedFiles = GetFilesToIncludeInArchive(publishLocation, flattenRuntime);
                foreach (var kvp in includedFiles)
                {
                    zipArchive.CreateEntryFromFile(kvp.Value, kvp.Key);

                    logger?.WriteLine($"... zipping: {kvp.Key}");
                }
            }
        }

        /// <summary>
        /// Creates the deployment bundle using the native zip tool installed
        /// on the system (default /usr/bin/zip). This is what is typically used on Linux and OSX
        /// </summary>
        /// <param name="zipCLI">The path to the located zip binary.</param>
        /// <param name="zipArchivePath">The path and name of the zip archive to create.</param>
        /// <param name="publishLocation">The location to be bundled.</param>
        /// <param name="flattenRuntime">If true the runtimes folder will be flatten</param>
        /// <param name="logger">Logger instance.</param>
        private static void BundleWithZipCLI(string zipCLI, string zipArchivePath, string publishLocation, bool flattenRuntime, IToolLogger logger)
        {
            var args = new StringBuilder("\"" + zipArchivePath + "\"");

            // so that we can archive content in subfolders, take the length of the
            // path to the root publish location and we'll just substring the
            // found files so the subpaths are retained
            var publishRootLength = publishLocation.Length;
            if (publishLocation[publishRootLength-1] != Path.DirectorySeparatorChar)
                publishRootLength++;

            var allFiles = GetFilesToIncludeInArchive(publishLocation, flattenRuntime);
            foreach (var kvp in allFiles)
            {
                args.AppendFormat(" \"{0}\"", kvp.Key);
            }

            var psiZip = new ProcessStartInfo
            {
                FileName = zipCLI,
                Arguments = args.ToString(),
                WorkingDirectory = publishLocation,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var handler = (DataReceivedEventHandler)((o, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;
                logger?.WriteLine("... zipping: " + e.Data);
            });

            using (var proc = new Process())
            {
                proc.StartInfo = psiZip;
                proc.Start();

                proc.ErrorDataReceived += handler;
                proc.OutputDataReceived += handler;
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                proc.EnableRaisingEvents = true;
                proc.WaitForExit();

                if (proc.ExitCode == 0)
                {
                    logger?.WriteLine(string.Format("Created publish archive ({0}).", zipArchivePath));
                }
            }
        }
    }
}