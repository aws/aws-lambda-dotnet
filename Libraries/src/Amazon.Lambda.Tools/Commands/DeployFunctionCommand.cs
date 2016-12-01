using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

#if NETCORE
using System.Runtime.Loader;
#endif

using Amazon;
using Amazon.Lambda;
using Amazon.Lambda.Model;

using Amazon.S3;
using Amazon.S3.Model;

using Amazon.Lambda.Tools.Options;
using Amazon.Runtime;

namespace Amazon.Lambda.Tools.Commands
{
    /// <summary>
    /// Command to deploy a function to AWS Lambda. When redeploying an existing function only function configuration properties
    /// that were explicitly set will be used. Default function configuration values are ignored for redeploy. This
    /// is to avoid any accidental changes to the function.
    /// </summary>
    public class DeployFunctionCommand : UpdateFunctionConfigCommand
    {
        public const string COMMAND_DEPLOY_NAME = "deploy-function";
        public const string COMMAND_DEPLOY_DESCRIPTION = "Command to deploy the project to AWS Lambda";
        public const string COMMAND_DEPLOY_ARGUMENTS = "<FUNCTION-NAME> The name of the function to deploy";

        public static readonly IList<CommandOption> DeployCommandOptions = BuildLineOptions(new List<CommandOption>
        {
            DefinedCommandOptions.ARGUMENT_CONFIGURATION,
            DefinedCommandOptions.ARGUMENT_FRAMEWORK,
            DefinedCommandOptions.ARGUMENT_FUNCTION_NAME,
            DefinedCommandOptions.ARGUMENT_FUNCTION_DESCRIPTION,
            DefinedCommandOptions.ARGUMENT_FUNCTION_PUBLISH,
            DefinedCommandOptions.ARGUMENT_FUNCTION_HANDLER,
            DefinedCommandOptions.ARGUMENT_FUNCTION_MEMORY_SIZE,
            DefinedCommandOptions.ARGUMENT_FUNCTION_ROLE,
            DefinedCommandOptions.ARGUMENT_FUNCTION_TIMEOUT,
            DefinedCommandOptions.ARGUMENT_FUNCTION_RUNTIME,
            DefinedCommandOptions.ARGUMENT_FUNCTION_SUBNETS,
            DefinedCommandOptions.ARGUMENT_FUNCTION_SECURITY_GROUPS,
            DefinedCommandOptions.ARGUMENT_ENVIRONMENT_VARIABLES,
            DefinedCommandOptions.ARGUMENT_KMS_KEY_ARN,
            DefinedCommandOptions.ARGUMENT_S3_BUCKET,
            DefinedCommandOptions.ARGUMENT_S3_PREFIX
        });

        public string Configuration { get; set; }
        public string TargetFramework { get; set; }

        public string S3Bucket { get; set; }
        public string S3Prefix { get; set; }


        // Disable handler validation for now.
        // TODO: Fix issue with loading dependent assemblies when doing validation.
        public bool SkipHandlerValidation { get; set; } = true;


        public DeployFunctionCommand(IToolLogger logger, string workingDirectory, string[] args)
            : base(logger, workingDirectory, DeployCommandOptions, args)
        {
        }


        /// <summary>
        /// Parse the CommandOptions into the Properties on the command.
        /// </summary>
        /// <param name="values"></param>
        protected override void ParseCommandArguments(CommandOptions values)
        {
            base.ParseCommandArguments(values);
            if (values.Arguments.Count > 0)
            {
                this.FunctionName = values.Arguments[0];
            }

            Tuple<CommandOption, CommandOptionValue> tuple;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_CONFIGURATION.Switch)) != null)
                this.Configuration = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FRAMEWORK.Switch)) != null)
                this.TargetFramework = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_S3_BUCKET.Switch)) != null)
                this.S3Bucket = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_S3_PREFIX.Switch)) != null)
                this.S3Prefix = tuple.Item2.StringValue;
        }



        public override async Task<bool> ExecuteAsync()
        {
            try
            {
                string configuration = this.GetStringValueOrDefault(this.Configuration, DefinedCommandOptions.ARGUMENT_CONFIGURATION, true);
                string targetFramework = this.GetStringValueOrDefault(this.TargetFramework, DefinedCommandOptions.ARGUMENT_FRAMEWORK, true);
                string projectLocation = this.GetStringValueOrDefault(this.ProjectLocation, DefinedCommandOptions.ARGUMENT_PROJECT_LOCATION, false);

                ValidateTargetFrameworkAndLambdaRuntime();

                string publishLocation;
                string zipArchivePath = null;
                Utilities.CreateApplicationBundle(this.Logger, this.WorkingDirectory, projectLocation, configuration, targetFramework, out publishLocation, ref zipArchivePath);
                if (string.IsNullOrEmpty(zipArchivePath))
                    return false;


                using (var stream = new MemoryStream(File.ReadAllBytes(zipArchivePath)))
                {
                    var s3Bucket = this.GetStringValueOrDefault(this.S3Bucket, DefinedCommandOptions.ARGUMENT_S3_BUCKET, false);
                    string s3Key = null;
                    if (!string.IsNullOrEmpty(s3Bucket))
                    {
                        await Utilities.ValidateBucketRegionAsync(this.S3Client, s3Bucket);

                        var functionName = this.GetStringValueOrDefault(this.FunctionName, DefinedCommandOptions.ARGUMENT_FUNCTION_NAME, true);
                        var s3Prefix = this.GetStringValueOrDefault(this.S3Prefix, DefinedCommandOptions.ARGUMENT_S3_PREFIX, false);
                        s3Key = await Utilities.UploadToS3Async(this.Logger, this.S3Client, s3Bucket, s3Prefix, functionName, stream);
                    }


                    var currentConfiguration = await GetFunctionConfigurationAsync();
                    if (currentConfiguration == null)
                    {
                        this.Logger.WriteLine($"Creating new Lambda function {this.FunctionName}");
                        var createRequest = new CreateFunctionRequest
                        {
                            FunctionName = this.GetStringValueOrDefault(this.FunctionName, DefinedCommandOptions.ARGUMENT_FUNCTION_NAME, true),
                            Description = this.GetStringValueOrDefault(this.Description, DefinedCommandOptions.ARGUMENT_FUNCTION_DESCRIPTION, false),
                            Role = this.GetStringValueOrDefault(this.Role, DefinedCommandOptions.ARGUMENT_FUNCTION_ROLE, true),
                            Handler = this.GetStringValueOrDefault(this.Handler, DefinedCommandOptions.ARGUMENT_FUNCTION_HANDLER, true),
                            Publish = this.GetBoolValueOrDefault(this.Publish, DefinedCommandOptions.ARGUMENT_FUNCTION_PUBLISH, false).GetValueOrDefault(),
                            MemorySize = this.GetIntValueOrDefault(this.MemorySize, DefinedCommandOptions.ARGUMENT_FUNCTION_MEMORY_SIZE, true).GetValueOrDefault(),
                            Runtime = this.GetStringValueOrDefault(this.Runtime, DefinedCommandOptions.ARGUMENT_FUNCTION_RUNTIME, true),
                            Timeout = this.GetIntValueOrDefault(this.Timeout, DefinedCommandOptions.ARGUMENT_FUNCTION_TIMEOUT, true).GetValueOrDefault(),
                            KMSKeyArn = this.GetStringValueOrDefault(this.KMSKeyArn, DefinedCommandOptions.ARGUMENT_KMS_KEY_ARN, false),
                            VpcConfig = new VpcConfig
                            {
                                SubnetIds = this.GetStringValuesOrDefault(this.SubnetIds, DefinedCommandOptions.ARGUMENT_FUNCTION_SUBNETS, false)?.ToList(),
                                SecurityGroupIds = this.GetStringValuesOrDefault(this.SecurityGroupIds, DefinedCommandOptions.ARGUMENT_FUNCTION_SUBNETS, false)?.ToList()
                            }
                        };

                        var environmentVariables = this.GetKeyValuePairOrDefault(this.EnvironmentVariables, DefinedCommandOptions.ARGUMENT_ENVIRONMENT_VARIABLES, false);
                        if(environmentVariables != null && environmentVariables.Count > 0)
                        {
                            createRequest.Environment = new Model.Environment
                            {
                                Variables = environmentVariables
                            };

                        }

                        if (s3Bucket != null)
                        {
                            createRequest.Code = new FunctionCode
                            {
                                S3Bucket = s3Bucket,
                                S3Key = s3Key
                            };
                        }
                        else
                        {
                            createRequest.Code = new FunctionCode
                            {
                                ZipFile = stream
                            };
                        }


                        if (!this.SkipHandlerValidation)
                        {
                            createRequest.Handler = EnsureFunctionHandlerIsValid(publishLocation, createRequest.Handler);
                        }

                        try
                        {
                            await this.LamdbaClient.CreateFunctionAsync(createRequest);
                            this.Logger.WriteLine("New Lambda function created");
                        }
                        catch (Exception e)
                        {
                            throw new LambdaToolsException($"Error creating Lambda function: {e.Message}");
                        }
                    }
                    else
                    {
                        if (!this.SkipHandlerValidation)
                        {
                            if (!string.IsNullOrEmpty(this.Handler))
                                this.Handler = EnsureFunctionHandlerIsValid(publishLocation, this.Handler);
                            else
                                this.Handler = EnsureFunctionHandlerIsValid(publishLocation, currentConfiguration.Handler);
                        }

                        this.Logger.WriteLine($"Updating code for existing function {this.FunctionName}");

                        var updateCodeRequest = new UpdateFunctionCodeRequest
                        {
                            FunctionName = this.GetStringValueOrDefault(this.FunctionName, DefinedCommandOptions.ARGUMENT_FUNCTION_NAME, true)
                        };

                        if (s3Bucket != null)
                        {
                            updateCodeRequest.S3Bucket = s3Bucket;
                            updateCodeRequest.S3Key = s3Key;
                        }
                        else
                        {
                            updateCodeRequest.ZipFile = stream;
                        }

                        try
                        {
                            await this.LamdbaClient.UpdateFunctionCodeAsync(updateCodeRequest);
                        }
                        catch (Exception e)
                        {
                            throw new LambdaToolsException($"Error updating code for Lambda function: {e.Message}");
                        }

                        await base.UpdateConfigAsync(currentConfiguration);
                    }
                }

                return true;
            }
            catch (LambdaToolsException e)
            {
                this.Logger.WriteLine(e.Message);
                return false;
            }
            catch (Exception e)
            {
                this.Logger.WriteLine($"Unknown error executing Lambda deployment: {e.Message}");
                this.Logger.WriteLine(e.StackTrace);
                return false;
            }
        }

        private void ValidateTargetFrameworkAndLambdaRuntime()
        {
            string runtimeName = this.GetStringValueOrDefault(this.Runtime, DefinedCommandOptions.ARGUMENT_FUNCTION_RUNTIME, true);
            string frameworkName = this.GetStringValueOrDefault(this.TargetFramework, DefinedCommandOptions.ARGUMENT_FRAMEWORK, true);
            Utilities.ValidateTargetFrameworkAndLambdaRuntime(runtimeName, frameworkName);
        }

        private string EnsureFunctionHandlerIsValid(string publishLocation, string handler)
        {
            try
            {
                Utilities.ValidateHandler(publishLocation, handler);
                return handler;
            }
            catch (ValidateHandlerException e)
            {
                Console.Error.WriteLine($"Error validating function handler {e.Handler} - {e.Message}");
            }

            return PromptForValue(DefinedCommandOptions.ARGUMENT_FUNCTION_HANDLER);
        }

    }
}
