using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

using Amazon;
using Amazon.Lambda;
using Amazon.Lambda.Model;

using Amazon.Lambda.Tools.Options;

namespace Amazon.Lambda.Tools.Commands
{
    /// <summary>
    /// Updates the configuration for an existing function. To avoid any accidental changes to the function
    /// only fields that were explicitly set are changed and defaults are ignored.
    /// </summary>
    public class UpdateFunctionConfigCommand : BaseCommand
    {
        public const string COMMAND_NAME = "update-function-config";
        public const string COMMAND_DESCRIPTION = "Command to update the runtime configuration for a Lambda function";
        public const string COMMAND_ARGUMENTS = "<FUNCTION-NAME> The name of the function to be updated";



        public static readonly IList<CommandOption> UpdateCommandOptions = BuildLineOptions(new List<CommandOption>
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
            DefinedCommandOptions.ARGUMENT_FUNCTION_TAGS,
            DefinedCommandOptions.ARGUMENT_FUNCTION_SUBNETS,
            DefinedCommandOptions.ARGUMENT_FUNCTION_SECURITY_GROUPS,
            DefinedCommandOptions.ARGUMENT_DEADLETTER_TARGET_ARN,
            DefinedCommandOptions.ARGUMENT_TRACING_MODE,
            DefinedCommandOptions.ARGUMENT_ENVIRONMENT_VARIABLES,
            DefinedCommandOptions.ARGUMENT_KMS_KEY_ARN, 
            DefinedCommandOptions.ARGUMENT_APPLY_DEFAULTS_FOR_UPDATE
        });

        public string FunctionName { get; set; }
        public string Description { get; set; }
        public bool? Publish { get; set; }
        public string Handler { get; set; }
        public int? MemorySize { get; set; }
        public string Role { get; set; }
        public int? Timeout { get; set; }
        public string[] SubnetIds { get; set; }
        public string[] SecurityGroupIds { get; set; }
        public Runtime Runtime { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public string KMSKeyArn { get; set; }
        public string DeadLetterTargetArn { get; set; }
        public string TracingMode { get; set; }
        public bool? ApplyDefaultsForUpdate { get; set; }

        public UpdateFunctionConfigCommand(IToolLogger logger, string workingDirectory, string[] args)
            : base(logger, workingDirectory, UpdateCommandOptions, args)
        {
        }

        protected UpdateFunctionConfigCommand(IToolLogger logger, string workingDirectory, IList<CommandOption> possibleOptions, string[] args)
            : base(logger, workingDirectory, possibleOptions, args)
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
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_NAME.Switch)) != null)
                this.FunctionName = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_DESCRIPTION.Switch)) != null)
                this.Description = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_PUBLISH.Switch)) != null)
                this.Publish = tuple.Item2.BoolValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_HANDLER.Switch)) != null)
                this.Handler = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_MEMORY_SIZE.Switch)) != null)
                this.MemorySize = tuple.Item2.IntValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_ROLE.Switch)) != null)
                this.Role = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_TIMEOUT.Switch)) != null)
                this.Timeout = tuple.Item2.IntValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_RUNTIME.Switch)) != null)
                this.Runtime = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_TAGS.Switch)) != null)
                this.Tags = tuple.Item2.KeyValuePairs;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_SUBNETS.Switch)) != null)
                this.SubnetIds = tuple.Item2.StringValues;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_SECURITY_GROUPS.Switch)) != null)
                this.SecurityGroupIds = tuple.Item2.StringValues;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_DEADLETTER_TARGET_ARN.Switch)) != null)
                this.DeadLetterTargetArn = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_TRACING_MODE.Switch)) != null)
                this.TracingMode = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_ENVIRONMENT_VARIABLES.Switch)) != null)
                this.EnvironmentVariables = tuple.Item2.KeyValuePairs;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_KMS_KEY_ARN.Switch)) != null)
                this.KMSKeyArn = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_APPLY_DEFAULTS_FOR_UPDATE.Switch)) != null)
                this.ApplyDefaultsForUpdate = tuple.Item2.BoolValue;
        }


        public override async Task<bool> ExecuteAsync()
        {
            try
            {
                var currentConfiguration = await GetFunctionConfigurationAsync();
                if(currentConfiguration == null)
                {
                    this.Logger.WriteLine($"Could not find existing Lambda function {this.GetStringValueOrDefault(this.FunctionName, DefinedCommandOptions.ARGUMENT_FUNCTION_NAME, true)}");
                    return false;
                }
                await UpdateConfigAsync(currentConfiguration);

                await ApplyTags(currentConfiguration.FunctionArn);

                var publish = this.GetBoolValueOrDefault(this.Publish, DefinedCommandOptions.ARGUMENT_FUNCTION_PUBLISH, false).GetValueOrDefault();
                if (publish)
                {
                    await PublishFunctionAsync(currentConfiguration.FunctionName);
                }

                return true;
            }
            catch(LambdaToolsException e)
            {
                this.Logger.WriteLine(e.Message);
                this.LastToolsException = e;
                return false;
            }
            catch(Exception e)
            {
                this.Logger.WriteLine($"Unknown error updating configuration for Lambda deployment: {e.Message}");
                this.Logger.WriteLine(e.StackTrace);
                return false;
            }
        }

        protected async Task PublishFunctionAsync(string functionName)
        {
            try
            {
                var response = await this.LambdaClient.PublishVersionAsync(new PublishVersionRequest
                {
                    FunctionName = functionName
                });
                this.Logger.WriteLine("Published new Lambda function version: " + response.Version);
            }
            catch (Exception e)
            {
                throw new LambdaToolsException($"Error publishing Lambda function: {e.Message}", LambdaToolsException.ErrorCode.LambdaPublishFunction, e);
            }
        }

        protected async Task ApplyTags(string functionArn)
        {
            try
            {
                var tags = this.GetKeyValuePairOrDefault(this.Tags, DefinedCommandOptions.ARGUMENT_FUNCTION_TAGS, false);
                if (tags == null || tags.Count == 0)
                    return;

                var tagRequest = new TagResourceRequest
                {
                    Resource = functionArn,
                    Tags = tags
                };

                await this.LambdaClient.TagResourceAsync(tagRequest);
                this.Logger?.WriteLine($"Applying {tags.Count} tag(s) to function");
            }
            catch (Exception e)
            {
                throw new LambdaToolsException($"Error tagging Lambda function: {e.Message}", LambdaToolsException.ErrorCode.LambdaTaggingFunction, e);
            }
        }

        protected async Task UpdateConfigAsync(GetFunctionConfigurationResponse existingConfiguration)
        {
            var request = CreateConfigurationRequestIfDifferent(existingConfiguration);
            if (request != null)
            {
                this.Logger.WriteLine($"Updating runtime configuration for function {this.GetStringValueOrDefault(this.FunctionName, DefinedCommandOptions.ARGUMENT_FUNCTION_NAME, true)}");
                try
                {
                    await this.LambdaClient.UpdateFunctionConfigurationAsync(request);
                }
                catch (Exception e)
                {
                    throw new LambdaToolsException($"Error updating configuration for Lambda function: {e.Message}", LambdaToolsException.ErrorCode.LambdaUpdateFunctionConfiguration, e);
                }
            }
        }

        public async Task<GetFunctionConfigurationResponse> GetFunctionConfigurationAsync()
        {
            var request = new GetFunctionConfigurationRequest
            {
                FunctionName = this.GetStringValueOrDefault(this.FunctionName, DefinedCommandOptions.ARGUMENT_FUNCTION_NAME, true)
            };
            try
            {
                var response = await this.LambdaClient.GetFunctionConfigurationAsync(request);
                return response;
            }
            catch (ResourceNotFoundException)
            {
                return null;
            }
            catch(Exception e)
            {
                throw new LambdaToolsException($"Error retrieving configuration for function {request.FunctionName}: {e.Message}", LambdaToolsException.ErrorCode.LambdaGetConfiguration, e);
            }
        }

        /// <summary>
        /// Create an UpdateFunctionConfigurationRequest if any fields have changed. Otherwise it returns back null causing the Update
        /// to skip.
        /// </summary>
        /// <param name="existingConfiguration"></param>
        /// <returns></returns>
        private UpdateFunctionConfigurationRequest CreateConfigurationRequestIfDifferent(GetFunctionConfigurationResponse existingConfiguration)
        {
            bool applyDefaultsFile = this.GetBoolValueOrDefault(this.ApplyDefaultsForUpdate, DefinedCommandOptions.ARGUMENT_APPLY_DEFAULTS_FOR_UPDATE, false).GetValueOrDefault();

            if(applyDefaultsFile)
            {
                this.Logger.WriteLine("Apply defaults values from defaults file while updating function configuration");
            }

            bool different = false;
            var request = new UpdateFunctionConfigurationRequest
            {
                FunctionName = this.GetStringValueOrDefault(this.FunctionName, DefinedCommandOptions.ARGUMENT_FUNCTION_NAME, true)
            };

            var description = applyDefaultsFile ? this.GetStringValueOrDefault(this.Description, DefinedCommandOptions.ARGUMENT_FUNCTION_DESCRIPTION, false) : Description;
            if (!string.IsNullOrEmpty(description) && !string.Equals(description, existingConfiguration.Description, StringComparison.Ordinal))
            {
                request.Description = description;
                different = true;
            }

            var role = applyDefaultsFile ? this.GetStringValueOrDefault(this.Role, DefinedCommandOptions.ARGUMENT_FUNCTION_ROLE, false) : this.Role;
            if (!string.IsNullOrEmpty(role))
            {
                string fullRole;
                if (role.StartsWith(Constants.IAM_ARN_PREFIX))
                    fullRole = role;
                else
                    fullRole = RoleHelper.ExpandRoleName(this.IAMClient, role);

                if (!string.Equals(fullRole, existingConfiguration.Role, StringComparison.Ordinal))
                {
                    request.Role = fullRole;
                    different = true;
                }
            }

            var handler = applyDefaultsFile ? this.GetStringValueOrDefault(this.Handler, DefinedCommandOptions.ARGUMENT_FUNCTION_HANDLER, false) : this.Handler;
            if (!string.IsNullOrEmpty(handler) && !string.Equals(handler, existingConfiguration.Handler, StringComparison.Ordinal))
            {
                request.Handler = handler;
                different = true;
            }

            var memorySize = applyDefaultsFile ? this.GetIntValueOrDefault(this.MemorySize, DefinedCommandOptions.ARGUMENT_FUNCTION_MEMORY_SIZE, false) : this.MemorySize;
            if(memorySize.HasValue && memorySize.Value != existingConfiguration.MemorySize)
            {
                request.MemorySize = memorySize.Value;
                different = true;
            }

            var runtime = applyDefaultsFile ? this.GetStringValueOrDefault(this.Runtime, DefinedCommandOptions.ARGUMENT_FUNCTION_RUNTIME, false) : this.Runtime?.Value;
            if (runtime != null && runtime != existingConfiguration.Runtime)
            {
                request.Runtime = runtime;
                different = true;
            }

            var timeout = applyDefaultsFile ? this.GetIntValueOrDefault(this.Timeout, DefinedCommandOptions.ARGUMENT_FUNCTION_TIMEOUT, false) : this.Timeout;
            if (timeout.HasValue && timeout.Value != existingConfiguration.Timeout)
            {
                request.Timeout = timeout.Value;
                different = true;
            }

            var subnetIds = applyDefaultsFile ? this.GetStringValuesOrDefault(this.SubnetIds, DefinedCommandOptions.ARGUMENT_FUNCTION_SUBNETS, false) : this.SubnetIds;
            if (subnetIds != null)
            {
                if(request.VpcConfig == null)
                {
                    request.VpcConfig = new VpcConfig
                    {
                        SubnetIds = subnetIds.ToList()
                    };
                    different = true;
                }
                if(AreDifferent(subnetIds, request.VpcConfig.SubnetIds))
                {
                    request.VpcConfig.SubnetIds = subnetIds.ToList();
                    different = true;
                }
            }

            var securityGroupIds = applyDefaultsFile ? this.GetStringValuesOrDefault(this.SecurityGroupIds, DefinedCommandOptions.ARGUMENT_FUNCTION_SECURITY_GROUPS, false) : this.SecurityGroupIds;
            if (securityGroupIds != null)
            {
                if (request.VpcConfig == null)
                {
                    request.VpcConfig = new VpcConfig
                    {
                        SecurityGroupIds = securityGroupIds.ToList()
                    };
                    different = true;
                }
                if (AreDifferent(securityGroupIds, request.VpcConfig.SecurityGroupIds))
                {
                    request.VpcConfig.SecurityGroupIds = securityGroupIds.ToList();
                    different = true;
                }
            }

            var deadLetterTargetArn = applyDefaultsFile ? this.GetStringValueOrDefault(this.DeadLetterTargetArn, DefinedCommandOptions.ARGUMENT_DEADLETTER_TARGET_ARN, false) : this.DeadLetterTargetArn;
            if (deadLetterTargetArn != null)
            {
                if (!string.IsNullOrEmpty(deadLetterTargetArn) && !string.Equals(deadLetterTargetArn, existingConfiguration.DeadLetterConfig?.TargetArn, StringComparison.Ordinal))
                {
                    request.DeadLetterConfig = existingConfiguration.DeadLetterConfig ?? new DeadLetterConfig();
                    request.DeadLetterConfig.TargetArn = deadLetterTargetArn;
                    different = true;
                }
                else if (string.IsNullOrEmpty(deadLetterTargetArn) && !string.IsNullOrEmpty(existingConfiguration.DeadLetterConfig?.TargetArn))
                {
                    request.DeadLetterConfig = null;
                    request.DeadLetterConfig = existingConfiguration.DeadLetterConfig ?? new DeadLetterConfig();
                    request.DeadLetterConfig.TargetArn = string.Empty;
                    different = true;
                }
            }

            var tracingMode = applyDefaultsFile ? this.GetStringValueOrDefault(this.TracingMode, DefinedCommandOptions.ARGUMENT_TRACING_MODE, false) : this.TracingMode;
            if (tracingMode != null)
            {
                var eTraceMode = Amazon.Lambda.TracingMode.FindValue(tracingMode);
                if (eTraceMode != existingConfiguration.TracingConfig?.Mode)
                {
                    request.TracingConfig = new TracingConfig();
                    request.TracingConfig.Mode = eTraceMode;
                    different = true;
                }
            }

            var kmsKeyArn = applyDefaultsFile ? this.GetStringValueOrDefault(this.KMSKeyArn, DefinedCommandOptions.ARGUMENT_KMS_KEY_ARN, false) : this.KMSKeyArn;
            if (!string.IsNullOrEmpty(kmsKeyArn) && !string.Equals(kmsKeyArn, existingConfiguration.KMSKeyArn, StringComparison.Ordinal))
            {
                request.KMSKeyArn = kmsKeyArn;
                different = true;
            }

            var environmentVariables = applyDefaultsFile ? this.GetKeyValuePairOrDefault(this.EnvironmentVariables, DefinedCommandOptions.ARGUMENT_ENVIRONMENT_VARIABLES, false) : this.EnvironmentVariables;
            if(environmentVariables != null && AreDifferent(environmentVariables, existingConfiguration?.Environment?.Variables))
            {
                request.Environment = new Model.Environment { Variables = environmentVariables };
                different = true;
            }



            if (!different)
                return null;

            return request;
        }

        private bool AreDifferent(IDictionary<string, string> source, IDictionary<string, string> target)
        {
            if (target == null)
                target = new Dictionary<string, string>();

            if (source.Count != target.Count)
                return true;

            foreach(var kvp in source)
            {
                string value;
                if (!target.TryGetValue(kvp.Key, out value))
                    return true;
                if (!string.Equals(kvp.Value, value, StringComparison.Ordinal))
                    return true;
            }

            foreach (var kvp in target)
            {
                string value;
                if (!source.TryGetValue(kvp.Key, out value))
                    return true;
                if (!string.Equals(kvp.Value, value, StringComparison.Ordinal))
                    return true;
            }


            return false;
        }

        private bool AreDifferent(IEnumerable<string> source, IEnumerable<string> target)
        {
            if (source == null && target == null)
                return false;

            if(source?.Count() != target?.Count())
                return true;

            foreach(var item in source)
            {
                if (!target.Contains(item))
                    return true;
            }
            foreach (var item in target)
            {
                if (!source.Contains(item))
                    return true;
            }

            return false;
        }
    }
}
