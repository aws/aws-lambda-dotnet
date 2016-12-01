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
            DefinedCommandOptions.ARGUMENT_FUNCTION_SUBNETS,
            DefinedCommandOptions.ARGUMENT_FUNCTION_SECURITY_GROUPS,
            DefinedCommandOptions.ARGUMENT_ENVIRONMENT_VARIABLES,
            DefinedCommandOptions.ARGUMENT_KMS_KEY_ARN
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
        public string KMSKeyArn { get; set; }

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
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_SUBNETS.Switch)) != null)
                this.SubnetIds = tuple.Item2.StringValues;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_SECURITY_GROUPS.Switch)) != null)
                this.SecurityGroupIds = tuple.Item2.StringValues;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_ENVIRONMENT_VARIABLES.Switch)) != null)
                this.EnvironmentVariables = tuple.Item2.KeyValuePairs;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_KMS_KEY_ARN.Switch)) != null)
                this.KMSKeyArn = tuple.Item2.StringValue;
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

                return true;
            }
            catch(LambdaToolsException e)
            {
                this.Logger.WriteLine(e.Message);
                return false;
            }
            catch(Exception e)
            {
                this.Logger.WriteLine($"Unknown error updating configuration for Lambda deployment: {e.Message}");
                this.Logger.WriteLine(e.StackTrace);
                return false;
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
                    await this.LamdbaClient.UpdateFunctionConfigurationAsync(request);
                }
                catch (Exception e)
                {
                    throw new LambdaToolsException($"Error updating configuration for Lambda function: {e.Message}");
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
                var response = await this.LamdbaClient.GetFunctionConfigurationAsync(request);
                return response;
            }
            catch (ResourceNotFoundException)
            {
                return null;
            }
            catch(Exception e)
            {
                throw new LambdaToolsException($"Error retrieving configuration for function {request.FunctionName}: {e.Message}");
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
            bool different = false;
            var request = new UpdateFunctionConfigurationRequest
            {
                FunctionName = this.GetStringValueOrDefault(this.FunctionName, DefinedCommandOptions.ARGUMENT_FUNCTION_NAME, true)
            };

            if (!string.IsNullOrEmpty(this.Description) && !string.Equals(this.Description, existingConfiguration.Description, StringComparison.Ordinal))
            {
                request.Description = Description;
                different = true;
            }

            if (!string.IsNullOrEmpty(this.Role))
            {
                string fullRole;
                if (this.Role.StartsWith(Constants.IAM_ARN_PREFIX))
                    fullRole = this.Role;
                else
                    fullRole = RoleHelper.ExpandRoleName(this.IAMClient, this.Role);

                if (!string.Equals(fullRole, existingConfiguration.Role, StringComparison.Ordinal))
                {
                    request.Role = fullRole;
                    different = true;
                }
            }

            if (!string.IsNullOrEmpty(this.Handler) && !string.Equals(this.Handler, existingConfiguration.Handler, StringComparison.Ordinal))
            {
                request.Handler = Handler;
                different = true;
            }

            if(MemorySize.HasValue && MemorySize.Value != existingConfiguration.MemorySize)
            {
                request.MemorySize = MemorySize.Value;
                different = true;
            }

            if (Runtime != null && Runtime != existingConfiguration.Runtime)
            {
                request.Runtime = Runtime;
                different = true;
            }

            if (Timeout.HasValue && Timeout.Value != existingConfiguration.Timeout)
            {
                request.Timeout = Timeout.Value;
                different = true;
            }

            if(this.SubnetIds != null)
            {
                if(request.VpcConfig == null)
                {
                    request.VpcConfig = new VpcConfig
                    {
                        SubnetIds = this.SubnetIds.ToList()
                    };
                    different = true;
                }
                if(AreDifferent(this.SubnetIds, request.VpcConfig.SubnetIds))
                {
                    request.VpcConfig.SubnetIds = this.SubnetIds.ToList();
                    different = true;
                }
            }
            if (this.SecurityGroupIds != null)
            {
                if (request.VpcConfig == null)
                {
                    request.VpcConfig = new VpcConfig
                    {
                        SecurityGroupIds = this.SecurityGroupIds.ToList()
                    };
                    different = true;
                }
                if (AreDifferent(this.SecurityGroupIds, request.VpcConfig.SecurityGroupIds))
                {
                    request.VpcConfig.SecurityGroupIds = this.SecurityGroupIds.ToList();
                    different = true;
                }
            }

            if (!string.IsNullOrEmpty(this.KMSKeyArn) && !string.Equals(this.KMSKeyArn, existingConfiguration.KMSKeyArn, StringComparison.Ordinal))
            {
                request.KMSKeyArn = this.KMSKeyArn;
                different = true;
            }
            if(this.EnvironmentVariables != null && AreDifferent(this.EnvironmentVariables, existingConfiguration?.Environment?.Variables))
            {
                request.Environment = new Model.Environment { Variables = this.EnvironmentVariables };
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
