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

using Amazon.CloudFormation.Model;
using Amazon.Util;
using System.Text;

#if NETCORE
using System.Runtime.Loader;
#endif

namespace Amazon.Lambda.Tools
{
    public static class Utilities
    {
        /// <summary>
        /// Make sure the handler specified actually exists.
        /// 
        /// TODO: This method's validation is not complete so disabled for now
        /// </summary>
        /// <param name="publishLocation"></param>
        /// <param name="handler"></param>
        public static void ValidateHandler(string publishLocation, string handler)
        {
            var tokens = handler.Split(new string[] { "::" }, StringSplitOptions.None);

            if (tokens.Length != 3)
            {
                throw new ValidateHandlerException(publishLocation, handler, "Invalid format for handler, format should be <assembly>::<type>::<method>");
            }

            var assemblyName = tokens[0];
            var typeName = tokens[1];
            var methodName = tokens[2];

            if (!assemblyName.EndsWith(".dll"))
                assemblyName += ".dll";

            var assemblyFullPath = Path.Combine(publishLocation, assemblyName);
            if (!File.Exists(assemblyFullPath))
            {
                throw new ValidateHandlerException(publishLocation, handler, $"Failed to find assembly {assemblyFullPath}");
            }

            Assembly assembly = GetAssembly(publishLocation, handler, assemblyFullPath);

            Type type = null;
            try
            {
                type = assembly.GetType(typeName);
            }
            catch (Exception e)
            {
                throw new ValidateHandlerException(publishLocation, handler, $"Error finding type {typeName}: {e.Message}");
            }

            if (type == null)
                throw new ValidateHandlerException(publishLocation, handler, $"Failed to find type {typeName}");

            var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy).FirstOrDefault(x =>
            {
                return string.Equals(methodName, x.Name, StringComparison.Ordinal);
            });

            if (method == null)
                throw new ValidateHandlerException(publishLocation, handler, $"Failed to find method {methodName}");

            if(method.GetParameters().Length > 2)
                throw new ValidateHandlerException(publishLocation, handler, $"Method {methodName} contains too parameters. Lambda functions can take 0 or 1 parameters plus an optional ILambdaContext object.");
        }

        private static Assembly GetAssembly(string publishLocation, string handler, string assemblyFullPath)
        {
            Assembly assembly = null;
#if NETCORE            
            try
            {
                var name = AssemblyLoadContext.GetAssemblyName(assemblyFullPath);
                try
                {
                    assembly = Assembly.Load(name);
                    if (assembly != null)
                        return assembly;
                }
                catch
                {

                }

                if (assembly == null)
                {
                    assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyFullPath);
                }
            }
            catch (Exception e)
            {
                throw new ValidateHandlerException(publishLocation, handler, $"Error loading assembly {assemblyFullPath}: {e.Message}");
            }
#else
            try
            {
                assembly = Assembly.LoadFile(assemblyFullPath);
            }
            catch (Exception e)
            {
                throw new ValidateHandlerException(publishLocation, handler, $"Error loading assembly {assemblyFullPath}: {e.Message}");
            }
#endif
            return assembly;
        }

        internal static string[] SplitByComma(this string str)
        {
            return str?.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }


        const int UPLOAD_PROGRESS_INCREMENT = 10;
        private static EventHandler<StreamTransferProgressArgs> CreateProgressHandler(IToolLogger logger)
        {
            int percentToUpdateOn = UPLOAD_PROGRESS_INCREMENT;
            EventHandler<StreamTransferProgressArgs> handler = ((s, e) =>
            {
                if (e.PercentDone == percentToUpdateOn || e.PercentDone > percentToUpdateOn)
                {
                    int increment = e.PercentDone % UPLOAD_PROGRESS_INCREMENT;
                    if (increment == 0)
                        increment = UPLOAD_PROGRESS_INCREMENT;
                    percentToUpdateOn = e.PercentDone + increment;
                    logger.WriteLine($"... Progress: {e.PercentDone}%");
                }
            });

            return handler;
        }

        public static async Task<string> UploadToS3Async(IToolLogger logger, IAmazonS3 s3Client, string bucket, string prefix, string rootName, Stream stream)
        {
            var extension = ".zip";
            if(!string.IsNullOrEmpty(Path.GetExtension(rootName)))
            {
                extension = Path.GetExtension(rootName);
                rootName = Path.GetFileNameWithoutExtension(rootName);
            }

            var key = (prefix ?? "") + $"{rootName}-{DateTime.Now.Ticks}{extension}";
            logger.WriteLine($"Uploading to S3. (Bucket: {bucket} Key: {key})");

            var request = new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = stream
            };
            request.StreamTransferProgress = Utilities.CreateProgressHandler(logger);

            try
            {
                await s3Client.PutObjectAsync(request);
            }
            catch(Exception e)
            {
                throw new LambdaToolsException($"Error uploading to {key} in bucket {bucket}: {e.Message}", LambdaToolsException.ErrorCode.S3UploadError, e);
            }

            return key;
        }

        public static async Task ValidateBucketRegionAsync(IAmazonS3 s3Client, string s3Bucket)
        {
            string bucketRegion;
            try
            {
                bucketRegion = await Utilities.GetBucketRegionAsync(s3Client, s3Bucket);
            }
            catch(Exception e)
            {
                throw new LambdaToolsException($"Error determining region for bucket {s3Bucket}: {e.Message}", LambdaToolsException.ErrorCode.S3GetBucketLocation, e);
            }

            var configuredRegion = s3Client.Config.RegionEndpoint?.SystemName;
            if(configuredRegion == null && !string.IsNullOrEmpty(s3Client.Config.ServiceURL))
            {
                configuredRegion = AWSSDKUtils.DetermineRegion(s3Client.Config.ServiceURL);
            }

            // If we still don't know the region and assume we are running in a non standard way and assume the caller
            // knows what they are doing.
            if (configuredRegion == null)
                return;
            
            if (!string.Equals(bucketRegion, configuredRegion))
            {
                throw new LambdaToolsException($"Error: S3 bucket must be in the same region as the configured region {configuredRegion}. {s3Bucket} is in the region {bucketRegion}.", LambdaToolsException.ErrorCode.BucketInDifferentRegionThenStack);
            }

        }

        public static async Task<string> GetBucketRegionAsync(IAmazonS3 s3Client, string bucket)
        {
            try
            {
                var request = new GetBucketLocationRequest { BucketName = bucket };
                var response = await s3Client.GetBucketLocationAsync(request);

                // Handle the legacy naming conventions
                if (response.Location == S3Region.US)
                    return "us-east-1";
                if (response.Location == S3Region.EU)
                    return "eu-west-1";

                return response.Location.Value;
            }
            catch(Exception e)
            {
                throw new LambdaToolsException($"Error determining region for bucket {bucket}: {e.Message}", LambdaToolsException.ErrorCode.S3GetBucketLocation, e);
            }
        }

        /// <summary>
        /// Make sure nobody is trying to deploy a function based on a higher .NET Core framework then the Lambda runtime knows about.
        /// </summary>
        /// <param name="lambdaRuntime"></param>
        /// <param name="targetFramework"></param>
        public static void ValidateTargetFrameworkAndLambdaRuntime(string lambdaRuntime, string targetFramework)
        {
            if (lambdaRuntime.Length < 3)
                return;

            string suffix = lambdaRuntime.Substring(lambdaRuntime.Length - 3);
            Version runtimeVersion;
            if (!Version.TryParse(suffix, out runtimeVersion))
                return;

            if (targetFramework.Length < 3)
                return;

            suffix = targetFramework.Substring(targetFramework.Length - 3);
            Version frameworkVersion;
            if (!Version.TryParse(suffix, out frameworkVersion))
                return;

            if (runtimeVersion < frameworkVersion)
            {
                throw new LambdaToolsException($"The framework {targetFramework} is a newer version than Lambda runtime {lambdaRuntime} supports", LambdaToolsException.ErrorCode.FrameworkNewerThanRuntime);
            }
        }

        /// <summary>
        /// Determines the location of the project depending on how the workingDirectory and projectLocation
        /// fields are set. This location is root of the project.
        /// </summary>
        /// <param name="workingDirectory"></param>
        /// <param name="projectLocation"></param>
        /// <returns></returns>
        public static string DetermineProjectLocation(string workingDirectory, string projectLocation)
        {
            string location;
            if (string.IsNullOrEmpty(projectLocation))
            {
                location = workingDirectory;
            }
            else
            {
                if (Path.IsPathRooted(projectLocation))
                    location = projectLocation;
                else
                    location = Path.Combine(workingDirectory, projectLocation);
            }

            if (location.EndsWith(@"\") || location.EndsWith(@"/"))
                location = location.Substring(0, location.Length - 1);

            return location;
        }

        /// <summary>
        /// Determine where the dotnet build directory is.
        /// </summary>
        /// <param name="workingDirectory"></param>
        /// <param name="projectLocation"></param>
        /// <param name="configuration"></param>
        /// <param name="targetFramework"></param>
        /// <returns></returns>
        public static string DetermineBuildLocation(string workingDirectory, string projectLocation, string configuration, string targetFramework)
        {
            var path = Path.Combine(
                    DetermineProjectLocation(workingDirectory, projectLocation),
                    "bin",
                    configuration,
                    targetFramework);
            return path;
        }

        /// <summary>
        /// Determine where the dotnet publish should put its artifacts at.
        /// </summary>
        /// <param name="workingDirectory"></param>
        /// <param name="projectLocation"></param>
        /// <param name="configuration"></param>
        /// <param name="targetFramework"></param>
        /// <returns></returns>
        public static string DeterminePublishLocation(string workingDirectory, string projectLocation, string configuration, string targetFramework)
        {
            var path = Path.Combine(DetermineProjectLocation(workingDirectory, projectLocation),
                    "bin",
                    configuration,
                    targetFramework,
                    "publish");
            return path;
        }



        /// <summary>
        /// A utility method for parsing KeyValue pair CommandOptions.
        /// </summary>
        /// <param name="option"></param>
        /// <returns></returns>
        public static Dictionary<string,string> ParseKeyValueOption(string option)
        {
            var parameters = new Dictionary<string,string>();
            if (string.IsNullOrWhiteSpace(option))
                return parameters;

            try
            {
                int currentPos = 0;
                while (currentPos != -1 && currentPos < option.Length)
                {
                    string name;
                    GetNextToken(option, '=', ref currentPos, out name);

                    string value;
                    GetNextToken(option, ';', ref currentPos, out value);

                    if(string.IsNullOrEmpty(name))
                        throw new LambdaToolsException($"Error parsing option ({option}), format should be <key1>=<value1>;<key2>=<value2>", LambdaToolsException.ErrorCode.CommandLineParseError);

                    parameters[name] = value ?? string.Empty;
                }
            }
            catch(LambdaToolsException)
            {
                throw;
            }
            catch(Exception e)
            {
                throw new LambdaToolsException($"Error parsing option ({option}), format should be <key1>=<value1>;<key2>=<value2>: {e.Message}", LambdaToolsException.ErrorCode.CommandLineParseError);
            }


            return parameters;
        }

        private static void GetNextToken(string option, char endToken, ref int currentPos, out string token)
        {
            if (option.Length <= currentPos)
            {
                token = string.Empty;
                return;
            }

            int tokenStart = currentPos;
            int tokenEnd = -1;
            bool inQuote = false;
            if(option[currentPos] == '"')
            {
                inQuote = true;
                tokenStart++;
                currentPos++;

                while (currentPos < option.Length && option[currentPos] != '"')
                {
                    currentPos++;
                } 

                if (option[currentPos] == '"')
                    tokenEnd = currentPos;
            }

            while (currentPos < option.Length && option[currentPos] != endToken)
            {
                currentPos++;
            }
                

            if(!inQuote)
            {
                if (currentPos < option.Length && option[currentPos] == endToken)
                    tokenEnd = currentPos;
            }

            if (tokenEnd == -1)
                token = option.Substring(tokenStart);
            else
                token = option.Substring(tokenStart, tokenEnd - tokenStart);

            currentPos++;
        }

    }
}
