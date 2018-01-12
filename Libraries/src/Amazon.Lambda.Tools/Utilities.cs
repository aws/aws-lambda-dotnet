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

using YamlDotNet.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ThirdParty.Json.LitJson;
using System.Xml.Linq;

namespace Amazon.Lambda.Tools
{
    public static class Utilities
    {

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

        public static void ValidateMicrosoftAspNetCoreAllReference(IToolLogger logger, string csprofPath)
        {
            if(Directory.Exists(csprofPath))
            {
                var projectFiles = Directory.GetFiles(csprofPath, "*.csproj", SearchOption.TopDirectoryOnly);
                if(projectFiles.Length != 1)
                {
                    logger.WriteLine("Unable to determine csproj project file when validating version of Microsoft.AspNetCore.All");
                    return;
                }
                csprofPath = projectFiles[0];
            }

            // If the file is not a csproj file then skip validation. This could happen
            // if the project is an F# project or an older style project.json.
            if (!string.Equals(Path.GetExtension(csprofPath), ".csproj"))
                return;

            var projectContent = File.ReadAllText(csprofPath);

            var manifestContent = ToolkitConfigFileFetcher.Instance.GetFileContentAsync(logger, "LambdaPackageStoreManifest.xml").Result;
            if (!string.IsNullOrEmpty(manifestContent))
            {
                ValidateMicrosoftAspNetCoreAllReference(logger, manifestContent, projectContent);
            }
            else
            {
                logger.WriteLine("Skipping Microsoft.AspNetCore.All validation because error while downloading Lambda runtime store manifest.");
            }
        }

        /// <summary>
        /// Make sure that if the project references the Microsoft.AspNetCore.All package which is in implicit package store
        /// that the Lambda runtime has that store available. Otherwise the Lambda function will fail with an Internal server error.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="manifestContent"></param>
        /// <param name="csprojContent"></param>
        public static void ValidateMicrosoftAspNetCoreAllReference(IToolLogger logger, string manifestContent, string csprojContent)
        {
            const string ASPNET_CORE_ALL = "Microsoft.AspNetCore.All";
            try
            {
                XDocument csprojXmlDoc = XDocument.Parse(csprojContent);

                Func<string> searchForAspNetCoreAllVersion = () =>
                {
                    // Not using XPath because to avoid adding an addition dependency for a simple one time use.
                    foreach (XElement group in csprojXmlDoc.Root.Elements("ItemGroup"))
                    {
                        foreach (XElement packageReference in group.Elements("PackageReference"))
                        {
                            var name = packageReference.Attribute("Include")?.Value;
                            if (string.Equals(name, ASPNET_CORE_ALL, StringComparison.Ordinal))
                            {
                                return packageReference.Attribute("Version")?.Value;
                            }
                        }
                    }

                    return null;
                };

                var projectAspNetCoreVersion = searchForAspNetCoreAllVersion();

                if (string.IsNullOrEmpty(projectAspNetCoreVersion))
                {
                    // Project is not using Microsoft.AspNetCore.All so skip validation.
                    return;
                }


                XDocument manifestXmlDoc = XDocument.Parse(manifestContent);

                string latestLambdaDeployedVersion = null;
                foreach (var element in manifestXmlDoc.Root.Elements("Package"))
                {
                    var name = element.Attribute("Id")?.Value;
                    if (string.Equals(name, ASPNET_CORE_ALL, StringComparison.Ordinal))
                    {
                        var version = element.Attribute("Version")?.Value;
                        if (string.Equals(projectAspNetCoreVersion, version, StringComparison.Ordinal))
                        {
                            // Version specifed in project file is available in Lambda Runtime
                            return;
                        }

                        // Record latest supported version to provide meaningful error message.
                        if (latestLambdaDeployedVersion == null || Version.Parse(latestLambdaDeployedVersion) < Version.Parse(version))
                        {
                            latestLambdaDeployedVersion = version;
                        }
                    }
                }

                throw new LambdaToolsException($"Project is referencing version {projectAspNetCoreVersion} of {ASPNET_CORE_ALL} which is newer " + 
                    $"than {latestLambdaDeployedVersion}, the latest version available in the Lambda Runtime environment. Please update your project to " + 
                    $"use version {latestLambdaDeployedVersion} and then redeploy your Lambda function.", LambdaToolsException.ErrorCode.AspNetCoreAllValidation);
            }
            catch(LambdaToolsException)
            {
                throw;
            }
            catch(Exception e)
            {
                logger?.WriteLine($"Unknown error validating version of {ASPNET_CORE_ALL}: {e.Message}");
            }
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

        public static string ProcessTemplateSubstitions(IToolLogger logger, string templateBody, IDictionary<string, string> substitutions, string workingDirectory)
        {
            if (DetermineTemplateFormat(templateBody) != TemplateFormat.Json || substitutions == null || !substitutions.Any())
                return templateBody;

            logger?.WriteLine($"Processing {substitutions.Count} substitutions.");
            var root = JsonConvert.DeserializeObject(templateBody) as JObject;

            foreach(var kvp in substitutions)
            {
                logger?.WriteLine($"Processing substitution: {kvp.Key}");
                var token = root.SelectToken(kvp.Key);
                if (token == null)
                    throw new LambdaToolsException($"Failed to locate JSONPath {kvp.Key} for template substitution.", LambdaToolsException.ErrorCode.ServerlessTemplateSubstitutionError);

                logger?.WriteLine($"\tFound element of type {token.Type}");

                string replacementValue;
                if (workingDirectory != null && File.Exists(Path.Combine(workingDirectory, kvp.Value)))
                {
                    var path = Path.Combine(workingDirectory, kvp.Value);
                    logger?.WriteLine($"\tReading: {path}");
                    replacementValue = File.ReadAllText(path);
                }
                else
                {
                    replacementValue = kvp.Value;
                }

                try
                {
                    switch(token.Type)
                    {
                        case JTokenType.String:
                            ((JValue)token).Value = replacementValue;
                            break;
                        case JTokenType.Boolean:
                            bool b;
                            if(bool.TryParse(replacementValue, out b))
                            {
                                ((JValue)token).Value = b;
                            }
                            else
                            {
                                throw new LambdaToolsException($"Failed to convert {replacementValue} to a bool", LambdaToolsException.ErrorCode.ServerlessTemplateSubstitutionError);
                            }
                            
                            break;
                        case JTokenType.Integer:
                            int i;
                            if (int.TryParse(replacementValue, out i))
                            {
                                ((JValue)token).Value = i;
                            }
                            else
                            {
                                throw new LambdaToolsException($"Failed to convert {replacementValue} to an int", LambdaToolsException.ErrorCode.ServerlessTemplateSubstitutionError);
                            }
                            break;
                        case JTokenType.Float:
                            double d;
                            if (double.TryParse(replacementValue, out d))
                            {
                                ((JValue)token).Value = d;
                            }
                            else
                            {
                                throw new LambdaToolsException($"Failed to convert {replacementValue} to a double", LambdaToolsException.ErrorCode.ServerlessTemplateSubstitutionError);
                            }
                            break;
                        case JTokenType.Array:
                        case JTokenType.Object:
                            var jcon = token as JContainer;
                            var jprop = jcon.Parent as JProperty;
                            JToken subData;
                            try
                            {
                                subData = JsonConvert.DeserializeObject(replacementValue) as JToken;
                            }
                            catch(Exception e)
                            {
                                throw new LambdaToolsException($"Failed to parse substitue JSON data: {e.Message}", LambdaToolsException.ErrorCode.ServerlessTemplateSubstitutionError);
                            }
                            jprop.Value = subData;
                            break;
                        default:
                            throw new LambdaToolsException($"Unable to determine how to convert substitute value into the template. " +
                                                            "Make sure to have a default value in the template which is used to determine the type. " +
                                                            "For example \"\" for string fields or {} for JSON objects.", 
                                                            LambdaToolsException.ErrorCode.ServerlessTemplateSubstitutionError);
                    }
                }
                catch(Exception e)
                {
                    throw new LambdaToolsException($"Error setting property {kvp.Key} with value {kvp.Value}: {e.Message}", LambdaToolsException.ErrorCode.ServerlessTemplateSubstitutionError);
                }
            }

            var json = JsonConvert.SerializeObject(root);
            return json;
        }


        /// <summary>
        /// Search for the CloudFormation resources that references the app bundle sent to S3 and update them.
        /// </summary>
        /// <param name="templateBody"></param>
        /// <param name="s3Bucket"></param>
        /// <param name="s3Key"></param>
        /// <returns></returns>
        public static string UpdateCodeLocationInTemplate(string templateBody, string s3Bucket, string s3Key)
        {
            switch (Utilities.DetermineTemplateFormat(templateBody))
            {
                case TemplateFormat.Json:
                    return UpdateCodeLocationInJsonTemplate(templateBody, s3Bucket, s3Key);
                case TemplateFormat.Yaml:
                    return UpdateCodeLocationInYamlTemplate(templateBody, s3Bucket, s3Key);
                default:
                    throw new LambdaToolsException("Unable to determine template file format", LambdaToolsException.ErrorCode.ServerlessTemplateParseError);
            }
        }

        public static string UpdateCodeLocationInJsonTemplate(string templateBody, string s3Bucket, string s3Key)
        {
            var s3Url = $"s3://{s3Bucket}/{s3Key}";
            JsonData root;
            try
            {
                root = JsonMapper.ToObject(templateBody) as JsonData;
            }
            catch (Exception e)
            {
                throw new LambdaToolsException($"Error parsing CloudFormation template: {e.Message}", LambdaToolsException.ErrorCode.ServerlessTemplateParseError, e);
            }

            var resources = root["Resources"] as JsonData;

            foreach (var field in resources.PropertyNames)
            {
                var resource = resources[field] as JsonData;
                if (resource == null)
                    continue;

                var properties = resource["Properties"] as JsonData;
                if (properties == null)
                    continue;

                var type = resource["Type"]?.ToString();
                if (string.Equals(type, "AWS::Serverless::Function", StringComparison.Ordinal))
                {
                    properties["CodeUri"] = s3Url;
                }

                if (string.Equals(type, "AWS::Lambda::Function", StringComparison.Ordinal))
                {
                    var code = new JsonData();
                    code["S3Bucket"] = s3Bucket;
                    code["S3Key"] = s3Key;
                    properties["Code"] = code;
                }
            }

            var json = JsonMapper.ToJson(root);
            return json;
        }

        public static string UpdateCodeLocationInYamlTemplate(string templateBody, string s3Bucket, string s3Key)
        {
            var s3Url = $"s3://{s3Bucket}/{s3Key}";
            var deserialize = new YamlDotNet.Serialization.Deserializer();

            var root = deserialize.Deserialize(new StringReader(templateBody)) as Dictionary<object, object>;
            if (root == null)
                return templateBody;

            if (!root.ContainsKey("Resources"))
                return templateBody;

            var resources = root["Resources"] as IDictionary<object, object>;


            foreach(var kvp in resources)
            {
                var resource = kvp.Value as IDictionary<object, object>;
                if (resource == null)
                    continue;

                if (!resource.ContainsKey("Properties"))
                    continue;
                var properties = resource["Properties"] as IDictionary<object, object>;


                if (!resource.ContainsKey("Type"))
                    continue;

                var type = resource["Type"]?.ToString();
                if (string.Equals(type, "AWS::Serverless::Function", StringComparison.Ordinal))
                {
                    properties["CodeUri"] = s3Url;
                }

                if (string.Equals(type, "AWS::Lambda::Function", StringComparison.Ordinal))
                {
                    var code = new Dictionary<object, object>();
                    code["S3Bucket"] = s3Bucket;
                    code["S3Key"] = s3Key;
                    properties["Code"] = code;
                }
            }

            var serializer = new Serializer();
            var updatedTemplateBody = serializer.Serialize(root);

            return updatedTemplateBody;
        }


        internal static TemplateFormat DetermineTemplateFormat(string templateBody)
        {
            templateBody = templateBody.Trim();
            if (templateBody.Length > 0 && templateBody[0] == '{')
                return TemplateFormat.Json;

            return TemplateFormat.Yaml;
        }
    }
}
