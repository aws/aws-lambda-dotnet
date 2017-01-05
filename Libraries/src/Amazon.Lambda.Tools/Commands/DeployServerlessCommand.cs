using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

using Amazon.Lambda.Tools.Options;
using Amazon.Runtime;
using System.IO;

using ThirdParty.Json.LitJson;
using System.Text;

namespace Amazon.Lambda.Tools.Commands
{
    /// <summary>
    /// Deployment command that uses an AWS Serverless CloudFormation template to drive the creation of the resources
    /// for an AWS Serverless application.
    /// </summary>
    public class DeployServerlessCommand : BaseCommand
    {
        public const string COMMAND_NAME = "deploy-serverless";
        public const string COMMAND_DESCRIPTION = "Command to deploy an AWS Serverless application";
        public const string COMMAND_ARGUMENTS = "<STACK-NAME> The name of the CloudFormation stack used to deploy the AWS Serverless application";

        // CloudFormation statuses for when the stack is in transition all end with IN_PROGRESS
        const string IN_PROGRESS_SUFFIX = "IN_PROGRESS";



        public static readonly IList<CommandOption> DeployServerlessCommandOptions = BuildLineOptions(new List<CommandOption>
        {
            DefinedCommandOptions.ARGUMENT_CONFIGURATION,
            DefinedCommandOptions.ARGUMENT_FRAMEWORK,
            DefinedCommandOptions.ARGUMENT_PACKAGE,
            DefinedCommandOptions.ARGUMENT_S3_BUCKET,
            DefinedCommandOptions.ARGUMENT_S3_PREFIX,
            DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE,
            DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE_PARAMETER,
            DefinedCommandOptions.ARGUMENT_STACK_NAME,
            DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_DISABLE_CAPABILITIES,
            DefinedCommandOptions.ARGUMENT_STACK_WAIT,
            DefinedCommandOptions.ARGUMENT_PERSIST_CONFIG_FILE
        });

        public string Configuration { get; set; }
        public string TargetFramework { get; set; }
        public string Package { get; set; }

        public string S3Bucket { get; set; }
        public string S3Prefix { get; set; }

        public string CloudFormationTemplate { get; set; }
        public string StackName { get; set; }
        public bool? WaitForStackToComplete { get; set; }
        public Dictionary<string, string> TemplateParameters { get; set; }

        public bool? PersistConfigFile { get; set; }


        public string[] DisabledCapabilities { get; set; }

        /// <summary>
        /// Parse the CommandOptions into the Properties on the command.
        /// </summary>
        /// <param name="values"></param>
        protected override void ParseCommandArguments(CommandOptions values)
        {
            base.ParseCommandArguments(values);
            if (values.Arguments.Count > 0)
            {
                this.StackName = values.Arguments[0];
            }

            Tuple<CommandOption, CommandOptionValue> tuple;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_CONFIGURATION.Switch)) != null)
                this.Configuration = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FRAMEWORK.Switch)) != null)
                this.TargetFramework = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_PACKAGE.Switch)) != null)
                this.Package = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_S3_BUCKET.Switch)) != null)
                this.S3Bucket = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_S3_PREFIX.Switch)) != null)
                this.S3Prefix = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_STACK_NAME.Switch)) != null)
                this.StackName = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE.Switch)) != null)
                this.CloudFormationTemplate = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_STACK_WAIT.Switch)) != null)
                this.WaitForStackToComplete = tuple.Item2.BoolValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE_PARAMETER.Switch)) != null)
                this.TemplateParameters = tuple.Item2.KeyValuePairs;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_PERSIST_CONFIG_FILE.Switch)) != null)
                this.PersistConfigFile = tuple.Item2.BoolValue;
        }


        public DeployServerlessCommand(IToolLogger logger, string workingDirectory, string[] args)
            : base(logger, workingDirectory, DeployServerlessCommandOptions, args)
        {
        }

        public override async Task<bool> ExecuteAsync()
        {
            try
            {
                string projectLocation = this.GetStringValueOrDefault(this.ProjectLocation, DefinedCommandOptions.ARGUMENT_PROJECT_LOCATION, false);
                string stackName = this.GetStringValueOrDefault(this.StackName, DefinedCommandOptions.ARGUMENT_STACK_NAME, true);
                string s3Bucket = this.GetStringValueOrDefault(this.S3Bucket, DefinedCommandOptions.ARGUMENT_S3_BUCKET, true);
                string s3Prefix = this.GetStringValueOrDefault(this.S3Prefix, DefinedCommandOptions.ARGUMENT_S3_PREFIX, false);
                string templatePath = this.GetStringValueOrDefault(this.CloudFormationTemplate, DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE, true);

                await Utilities.ValidateBucketRegionAsync(this.S3Client, s3Bucket);

                if (!Path.IsPathRooted(templatePath))
                {
                    templatePath = Path.Combine(Utilities.DetemineProjectLocation(this.WorkingDirectory, projectLocation), templatePath);
                }

                if (!File.Exists(templatePath))
                    throw new LambdaToolsException($"Template file {templatePath} cannot be found.", LambdaToolsException.ErrorCode.ServerlessTemplateNotFound);


                // Build and bundle up the users project.
                string publishLocation;
                string zipArchivePath = null;
                string package = this.GetStringValueOrDefault(this.Package, DefinedCommandOptions.ARGUMENT_PACKAGE, false);
                if(string.IsNullOrEmpty(package))
                {
                    string configuration = this.GetStringValueOrDefault(this.Configuration, DefinedCommandOptions.ARGUMENT_CONFIGURATION, true);
                    string targetFramework = this.GetStringValueOrDefault(this.TargetFramework, DefinedCommandOptions.ARGUMENT_FRAMEWORK, true);
                    Utilities.CreateApplicationBundle(this.DefaultConfig, this.Logger, this.WorkingDirectory, projectLocation, configuration, targetFramework, out publishLocation, ref zipArchivePath);
                    if (string.IsNullOrEmpty(zipArchivePath))
                        return false;
                }
                else
                {
                    if (!File.Exists(package))
                        throw new LambdaToolsException($"Package {package} does not exist", LambdaToolsException.ErrorCode.InvalidPackage);
                    if (!string.Equals(Path.GetExtension(package), ".zip", StringComparison.OrdinalIgnoreCase))
                        throw new LambdaToolsException($"Package {package} must be a zip file", LambdaToolsException.ErrorCode.InvalidPackage);

                    this.Logger.WriteLine($"Skipping compilation and using precompiled package {package}");
                    zipArchivePath = package;
                }


                // Upload the app bundle to S3
                string s3KeyApplicationBundle;
                using (var stream = new MemoryStream(File.ReadAllBytes(zipArchivePath)))
                {
                    s3KeyApplicationBundle = await Utilities.UploadToS3Async(this.Logger, this.S3Client, s3Bucket, s3Prefix, stackName, stream);
                }

                // Read in the serverless template and update all the locations for Lambda functions to point to the app bundle that was just uploaded.
                string templateBody = File.ReadAllText(templatePath);
                templateBody = UpdateCodeLocationInTemplate(templateBody, s3Bucket, s3KeyApplicationBundle);

                // Upload the template to S3 instead of sending it straight to CloudFormation to avoid the size limitation
                string s3KeyTemplate;
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(templateBody)))
                {
                    s3KeyTemplate = await Utilities.UploadToS3Async(this.Logger, this.S3Client, s3Bucket, s3Prefix, stackName + "-" + Path.GetFileName(templatePath), stream);
                }

                var existingStack = await GetExistingStackAsync(stackName);
                this.Logger.WriteLine("Found existing stack: " + (existingStack != null));
                var changeSetName = "Lambda-Tools-" + DateTime.Now.Ticks;

                // Determine if the stack is in a good state to be updated.
                ChangeSetType changeSetType;
                if (existingStack == null || existingStack.StackStatus == StackStatus.REVIEW_IN_PROGRESS || existingStack.StackStatus == StackStatus.DELETE_COMPLETE)
                {
                    changeSetType = ChangeSetType.CREATE;
                }
                // If the status was ROLLBACK_COMPLETE that means the stack failed on initial creation
                // and the resources were cleaned up. It is safe to delete the stack so we can recreate it.
                else if (existingStack.StackStatus == StackStatus.ROLLBACK_COMPLETE)
                {
                    await DeleteRollbackCompleteStackAsync(existingStack);
                    changeSetType = ChangeSetType.CREATE;
                }
                // If the status was ROLLBACK_IN_PROGRESS that means the initial creation is failing.
                // Wait to see if it goes into ROLLBACK_COMPLETE status meaning everything got cleaned up and then delete it.
                else if (existingStack.StackStatus == StackStatus.ROLLBACK_IN_PROGRESS)
                {
                    existingStack = await WaitForNoLongerInProgress(existingStack.StackName);
                    if (existingStack != null && existingStack.StackStatus == StackStatus.ROLLBACK_COMPLETE)
                        await DeleteRollbackCompleteStackAsync(existingStack);

                    changeSetType = ChangeSetType.CREATE;
                }
                // If the status was DELETE_IN_PROGRESS then just wait for delete to complete 
                else if (existingStack.StackStatus == StackStatus.DELETE_IN_PROGRESS)
                {
                    await WaitForNoLongerInProgress(existingStack.StackName);
                    changeSetType = ChangeSetType.CREATE;
                }
                // The Stack state is in a normal state and ready to be updated.
                else if (existingStack.StackStatus == StackStatus.CREATE_COMPLETE ||
                        existingStack.StackStatus == StackStatus.UPDATE_COMPLETE ||
                        existingStack.StackStatus == StackStatus.UPDATE_ROLLBACK_COMPLETE)
                {
                    changeSetType = ChangeSetType.UPDATE;
                }
                // All other states means the Stack is in an inconsistent state.
                else
                {
                    this.Logger.WriteLine($"The stack's current state of {existingStack.StackStatus} is invalid for updating");
                    return false;

                }

                CreateChangeSetResponse changeSetResponse;
                try
                {
                    var templateParameters = GetTemplateParameters(changeSetType == ChangeSetType.UPDATE ? existingStack : null);
                    if (templateParameters?.Count > 0)
                    {
                        var setParameters = templateParameters.Where(x => !x.UsePreviousValue);
                        if (setParameters?.Count() > 0)
                        {
                            this.Logger.WriteLine("Template Parameters Applied:");
                            foreach (var parameter in setParameters)
                            {
                                this.Logger.WriteLine($"\t{parameter.ParameterKey}: {parameter.ParameterValue}");
                            }
                        }
                    }

                    var capabilities = new List<string>();
                    var disabledCapabilties = GetStringValuesOrDefault(this.DisabledCapabilities, DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_DISABLE_CAPABILITIES, false);

                    if (disabledCapabilties?.FirstOrDefault(x => string.Equals(x, "CAPABILITY_IAM", StringComparison.OrdinalIgnoreCase)) == null)
                    {
                        capabilities.Add("CAPABILITY_IAM");
                    }
                    if (disabledCapabilties?.FirstOrDefault(x => string.Equals(x, "CAPABILITY_NAMED_IAM", StringComparison.OrdinalIgnoreCase)) == null)
                    {
                        capabilities.Add("CAPABILITY_NAMED_IAM");
                    }

                    // Create the change set which performs the transformation on the Serverless resources in the template.
                    changeSetResponse = await this.CloudFormationClient.CreateChangeSetAsync(new CreateChangeSetRequest
                    {
                        StackName = stackName,
                        Parameters = templateParameters,
                        ChangeSetName = changeSetName,
                        ChangeSetType = changeSetType,
                        Capabilities = capabilities,
                        Tags = new List<Tag> { new Tag { Key = Constants.SERVERLESS_TAG_NAME, Value = "true" } },
                        TemplateURL = this.S3Client.GetPreSignedURL(new S3.Model.GetPreSignedUrlRequest { BucketName = s3Bucket, Key = s3KeyTemplate, Expires = DateTime.Now.AddHours(1) })
                    });


                    this.Logger.WriteLine("CloudFormation change set created");
                }
                catch (Exception e)
                {
                    throw new LambdaToolsException($"Error creating CloudFormation change set: {e.Message}", LambdaToolsException.ErrorCode.CloudFormationCreateStack, e);
                }

                // The change set can take a few seconds to be reviewed and be ready to be executed.
                if (!await WaitForChangeSetBeingAvailableAsync(changeSetResponse.Id, existingStack != null))
                    return false;

                var executeChangeSetRequest = new ExecuteChangeSetRequest
                {
                    StackName = stackName,
                    ChangeSetName = changeSetResponse.Id
                };

                // Execute the change set.
                DateTime timeChangeSetExecuted = DateTime.Now;
                ExecuteChangeSetResponse executeChangeSetResponse;
                try
                {
                    executeChangeSetResponse = await this.CloudFormationClient.ExecuteChangeSetAsync(executeChangeSetRequest);
                    if (changeSetType == ChangeSetType.CREATE)
                        this.Logger.WriteLine($"Created CloudFormation stack {stackName}");
                    else
                        this.Logger.WriteLine($"Initiated CloudFormation stack update on {stackName}");
                }
                catch (Exception e)
                {
                    throw new LambdaToolsException($"Error executing CloudFormation change set: {e.Message}", LambdaToolsException.ErrorCode.CloudFormationCreateChangeSet, e);
                }

                // Wait for the stack to finish unless the user opts out of waiting. The VS Toolkit opts out and
                // instead shows the stack view in the IDE, enabling the user to view progress.
                var shouldWait = GetBoolValueOrDefault(this.WaitForStackToComplete, DefinedCommandOptions.ARGUMENT_STACK_WAIT, false);
                if (!shouldWait.HasValue || shouldWait.Value)
                {
                    var updatedStack = await WaitStackToCompleteAsync(stackName, timeChangeSetExecuted);

                    if (updatedStack.StackStatus == StackStatus.CREATE_COMPLETE || updatedStack.StackStatus == StackStatus.UPDATE_COMPLETE)
                    {
                        this.Logger.WriteLine($"Stack finished updating with status: {updatedStack.StackStatus}");

                        // Display the output parameters.
                        DisplayOutputs(updatedStack);
                    }
                    else
                    {

                        this.Logger.WriteLine($"Stack update failed with status: {updatedStack.StackStatus} ({updatedStack.StackStatusReason})");
                    }
                }

                if (this.GetBoolValueOrDefault(this.PersistConfigFile, DefinedCommandOptions.ARGUMENT_PERSIST_CONFIG_FILE, false).GetValueOrDefault())
                {
                    this.SaveConfigFile();
                }

                return true;
            }
            catch (LambdaToolsException e)
            {
                this.Logger.WriteLine(e.Message);
                this.LastToolsException = e;
                return false;
            }
            catch (Exception e)
            {
                this.Logger.WriteLine($"Unknown error executing AWS Serverless deployment: {e.Message}");
                this.Logger.WriteLine(e.StackTrace);
                return false;
            }
        }

        private void DisplayOutputs(Stack stack)
        {
            if (stack.Outputs.Count == 0)
                return;

            const int OUTPUT_NAME_WIDTH = 30;
            const int OUTPUT_VALUE_WIDTH = 50;

            this.Logger.WriteLine("   ");
            this.Logger.WriteLine("Output Name".PadRight(OUTPUT_NAME_WIDTH) + " " + "Value".PadRight(OUTPUT_VALUE_WIDTH));
            this.Logger.WriteLine($"{new string('-', OUTPUT_NAME_WIDTH)} {new string('-', OUTPUT_VALUE_WIDTH)}");
            foreach (var output in stack.Outputs)
            {
                string line = output.OutputKey.PadRight(OUTPUT_NAME_WIDTH) + " " + output.OutputValue?.PadRight(OUTPUT_VALUE_WIDTH);
                this.Logger.WriteLine(line);
            }
        }

        static readonly TimeSpan POLLING_PERIOD = TimeSpan.FromSeconds(3);
        private async Task<Stack> WaitStackToCompleteAsync(string stackName, DateTime mintimeStampForEvents)
        {
            const int TIMESTAMP_WIDTH = 20;
            const int LOGICAL_RESOURCE_WIDTH = 40;
            const int RESOURCE_STATUS = 40;
            string mostRecentEventId = "";

            // Write header for the status table.
            this.Logger.WriteLine("   ");
            this.Logger.WriteLine(
                "Timestamp".PadRight(TIMESTAMP_WIDTH) + " " +
                "Logical Resource Id".PadRight(LOGICAL_RESOURCE_WIDTH) + " " +
                "Status".PadRight(RESOURCE_STATUS) + " ");
            this.Logger.WriteLine(
                new string('-', TIMESTAMP_WIDTH) + " " +
                new string('-', LOGICAL_RESOURCE_WIDTH) + " " +
                new string('-', RESOURCE_STATUS) + " ");

            Stack stack;
            do
            {
                Thread.Sleep(POLLING_PERIOD);
                stack = await GetExistingStackAsync(stackName);

                var events = await GetLatestEventsAsync(stackName, mintimeStampForEvents, mostRecentEventId);
                if (events.Count > 0)
                    mostRecentEventId = events[0].EventId;

                for (int i = events.Count - 1; i >= 0; i--)
                {
                    string line =
                        events[i].Timestamp.ToString("g").PadRight(TIMESTAMP_WIDTH) + " " +
                        events[i].LogicalResourceId.PadRight(LOGICAL_RESOURCE_WIDTH) + " " +
                        events[i].ResourceStatus.ToString().PadRight(RESOURCE_STATUS);

                    // To save screen space only show error messages.
                    if (!events[i].ResourceStatus.ToString().EndsWith(IN_PROGRESS_SUFFIX) && !string.IsNullOrEmpty(events[i].ResourceStatusReason))
                        line += " " + events[i].ResourceStatusReason;



                    this.Logger.WriteLine(line);
                }

            } while (stack.StackStatus.ToString().EndsWith(IN_PROGRESS_SUFFIX));

            return stack;
        }

        private async Task<List<StackEvent>> GetLatestEventsAsync(string stackName, DateTime mintimeStampForEvents, string mostRecentEventId)
        {
            bool noNewEvents = false;
            List<StackEvent> events = new List<StackEvent>();
            DescribeStackEventsResponse response = null;
            do
            {
                var request = new DescribeStackEventsRequest() { StackName = stackName };
                if (response != null)
                    request.NextToken = response.NextToken;

                try
                {
                    response = await this.CloudFormationClient.DescribeStackEventsAsync(request);
                }
                catch (Exception e)
                {
                    throw new LambdaToolsException($"Error getting events for stack: {e.Message}", LambdaToolsException.ErrorCode.CloudFormationDescribeStackEvents, e);
                }
                foreach (var evnt in response.StackEvents)
                {
                    if (string.Equals(evnt.EventId, mostRecentEventId) || evnt.Timestamp < mintimeStampForEvents)
                    {
                        noNewEvents = true;
                        break;
                    }

                    events.Add(evnt);
                }

            } while (!noNewEvents && !string.IsNullOrEmpty(response.NextToken));

            return events;
        }

        private async Task DeleteRollbackCompleteStackAsync(Stack stack)
        {
            try
            {
                if (stack.StackStatus == StackStatus.ROLLBACK_COMPLETE)
                    await this.CloudFormationClient.DeleteStackAsync(new DeleteStackRequest { StackName = stack.StackName });

                await WaitForNoLongerInProgress(stack.StackName);
            }
            catch (Exception e)
            {
                throw new LambdaToolsException($"Error removing previous failed stack creation {stack.StackName}: {e.Message}", LambdaToolsException.ErrorCode.CloudFormationDeleteStack, e);
            }
        }

        private async Task<Stack> WaitForNoLongerInProgress(string stackName)
        {
            try
            {
                long start = DateTime.Now.Ticks;
                Stack currentStack = null;
                do
                {
                    if (currentStack != null)
                        this.Logger.WriteLine($"... Waiting for stack's state to change from {currentStack.StackStatus}: {TimeSpan.FromTicks(DateTime.Now.Ticks - start).TotalSeconds.ToString("0").PadLeft(3)} secs");
                    currentStack = null;
                    Thread.Sleep(POLLING_PERIOD);
                    currentStack = await GetExistingStackAsync(stackName);

                } while (currentStack != null && currentStack.StackStatus.ToString().EndsWith(IN_PROGRESS_SUFFIX));

                return currentStack;
            }
            catch (Exception e)
            {
                throw new LambdaToolsException($"Error waiting for stack state change: {e.Message}", LambdaToolsException.ErrorCode.WaitingForStackError, e);
            }
        }

        private async Task<bool> WaitForChangeSetBeingAvailableAsync(string changeSetId, bool updated)
        {
            try
            {
                var request = new DescribeChangeSetRequest
                {
                    ChangeSetName = changeSetId
                };

                this.Logger.WriteLine($"... Waiting for change set to be reviewed");
                DescribeChangeSetResponse response;
                do
                {
                    Thread.Sleep(POLLING_PERIOD);
                    response = await this.CloudFormationClient.DescribeChangeSetAsync(request);
                } while (response.Status == ChangeSetStatus.CREATE_IN_PROGRESS || response.Status == ChangeSetStatus.CREATE_PENDING);

                if (response.Status == ChangeSetStatus.FAILED)
                {
                    this.Logger.WriteLine($"Failed to create CloudFormation change set: {response.StatusReason}");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                throw new LambdaToolsException($"Error getting status of change set: {e.Message}", LambdaToolsException.ErrorCode.CloudFormationDescribeChangeSet, e);
            }
        }

        public async Task<Stack> GetExistingStackAsync(string stackName)
        {
            try
            {
                var response = await this.CloudFormationClient.DescribeStacksAsync(new DescribeStacksRequest { StackName = stackName });
                if (response.Stacks.Count != 1)
                    return null;

                return response.Stacks[0];
            }
            catch (AmazonCloudFormationException)
            {
                return null;
            }
        }


        private List<Parameter> GetTemplateParameters(Stack stack)
        {
            var parameters = new List<Parameter>();

            var map = GetKeyValuePairOrDefault(this.TemplateParameters, DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE_PARAMETER, false);
            if (map != null)
            {
                foreach (var kvp in map)
                {
                    parameters.Add(new Parameter { ParameterKey = kvp.Key, ParameterValue = kvp.Value ?? "" });
                }
            }

            if (stack != null)
            {
                foreach (var existingParameter in stack.Parameters)
                {
                    if (!parameters.Any(x => string.Equals(x.ParameterKey, existingParameter.ParameterKey)))
                    {
                        parameters.Add(new Parameter { ParameterKey = existingParameter.ParameterKey, UsePreviousValue = true });
                    }
                }
            }

            return parameters;
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

                var type = resource["Type"]?.ToString();
                if (!string.Equals(type, "AWS::Serverless::Function", StringComparison.Ordinal))
                    continue;

                var properties = resource["Properties"] as JsonData;
                if (properties == null)
                    continue;

                properties["CodeUri"] = s3Url;
            }

            var json = JsonMapper.ToJson(root);
            return json;
        }

        private void SaveConfigFile()
        {
            try
            {
                JsonData data;
                if (File.Exists(this.DefaultConfig.SourceFile))
                {
                    data = JsonMapper.ToObject(File.ReadAllText(this.DefaultConfig.SourceFile));
                }
                else
                {
                    data = new JsonData();
                }

                data.SetIfNotNull(DefinedCommandOptions.ARGUMENT_AWS_REGION.ConfigFileKey, this.GetStringValueOrDefault(this.Region, DefinedCommandOptions.ARGUMENT_AWS_REGION, false));
                data.SetIfNotNull(DefinedCommandOptions.ARGUMENT_AWS_PROFILE.ConfigFileKey, this.GetStringValueOrDefault(this.Profile, DefinedCommandOptions.ARGUMENT_AWS_PROFILE, false));
                data.SetIfNotNull(DefinedCommandOptions.ARGUMENT_AWS_PROFILE_LOCATION.ConfigFileKey, this.GetStringValueOrDefault(this.ProfileLocation, DefinedCommandOptions.ARGUMENT_AWS_PROFILE_LOCATION, false));


                data.SetIfNotNull(DefinedCommandOptions.ARGUMENT_CONFIGURATION.ConfigFileKey, this.GetStringValueOrDefault(this.Configuration, DefinedCommandOptions.ARGUMENT_CONFIGURATION, false));
                data.SetIfNotNull(DefinedCommandOptions.ARGUMENT_FRAMEWORK.ConfigFileKey, this.GetStringValueOrDefault(this.TargetFramework, DefinedCommandOptions.ARGUMENT_FRAMEWORK, false));
                data.SetIfNotNull(DefinedCommandOptions.ARGUMENT_S3_BUCKET.ConfigFileKey, this.GetStringValueOrDefault(this.S3Bucket, DefinedCommandOptions.ARGUMENT_S3_BUCKET, false));
                data.SetIfNotNull(DefinedCommandOptions.ARGUMENT_S3_PREFIX.ConfigFileKey, this.GetStringValueOrDefault(this.S3Prefix, DefinedCommandOptions.ARGUMENT_S3_PREFIX, false));

                var template = this.GetStringValueOrDefault(this.CloudFormationTemplate, DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE, false);
                if(Path.IsPathRooted(template))
                {
                    string projectLocation = this.GetStringValueOrDefault(this.ProjectLocation, DefinedCommandOptions.ARGUMENT_PROJECT_LOCATION, false);
                    var projectRoot = Utilities.DetemineProjectLocation(this.WorkingDirectory, projectLocation);
                    if(template.StartsWith(projectRoot))
                    {
                        template = template.Substring(projectRoot.Length + 1);
                    }
                }
                data.SetIfNotNull(DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE.ConfigFileKey, template);

                data.SetIfNotNull(DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE_PARAMETER.ConfigFileKey, LambdaToolsDefaults.FormatKeyValue(this.GetKeyValuePairOrDefault(this.TemplateParameters, DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE_PARAMETER, false)));
                data.SetIfNotNull(DefinedCommandOptions.ARGUMENT_STACK_NAME.ConfigFileKey, this.GetStringValueOrDefault(this.StackName, DefinedCommandOptions.ARGUMENT_STACK_NAME, false));
                data.SetIfNotNull(DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_DISABLE_CAPABILITIES.ConfigFileKey, LambdaToolsDefaults.FormatCommaDelimitedList(this.GetStringValuesOrDefault(this.DisabledCapabilities, DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_DISABLE_CAPABILITIES, false)));
                data.SetIfNotNull(DefinedCommandOptions.ARGUMENT_STACK_WAIT.ConfigFileKey, this.GetBoolValueOrDefault(this.WaitForStackToComplete, DefinedCommandOptions.ARGUMENT_STACK_WAIT, false));

                StringBuilder sb = new StringBuilder();
                JsonWriter writer = new JsonWriter(sb);
                writer.PrettyPrint = true;
                JsonMapper.ToJson(data, writer);

                var json = sb.ToString();
                File.WriteAllText(this.DefaultConfig.SourceFile, json);
                this.Logger.WriteLine($"Config settings saved to {this.DefaultConfig.SourceFile}");
            }
            catch (Exception e)
            {
                throw new LambdaToolsException("Error persisting configuration file: " + e.Message, LambdaToolsException.ErrorCode.PersistConfigError);
            }
        }

    }
}
