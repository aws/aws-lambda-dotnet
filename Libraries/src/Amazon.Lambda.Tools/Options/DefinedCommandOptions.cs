using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amazon.Lambda.Tools.Options
{
    /// <summary>
    /// This class defines all the possible options across all the commands. The individual commands will then
    /// references the options that are appropiate.
    /// </summary>
    public static class DefinedCommandOptions
    {
        public static readonly CommandOption ARGUMENT_AWS_PROFILE =
            new CommandOption
            {
                Name = "AWS Profile",
                Switch = "--profile",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "Profile to use to look up AWS credentials, if not set environment credentials will be used"
            };

        public static readonly CommandOption ARGUMENT_AWS_PROFILE_LOCATION =
            new CommandOption
            {
                Name = "AWS Profile Location",
                Switch = "--profile-location",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "Optional override to the search location for Profiles, points at a shared credentials file"
            };

        public static readonly CommandOption ARGUMENT_AWS_REGION =
            new CommandOption
            {
                Name = "AWS Region",
                Switch = "--region",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "The region to connect to AWS services, if not set region will be detected from the environment"
            };


        public static readonly CommandOption ARGUMENT_PROJECT_LOCATION =
            new CommandOption
            {
                Name = "Project Location",
                ShortSwitch = "-pl",
                Switch = "--project-location",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "The location of the project, if not set the current directory will be assumed"
            };
        public static readonly CommandOption ARGUMENT_CONFIGURATION =
            new CommandOption
            {
                Name = "Build Configuration",
                ShortSwitch = "-c",
                Switch = "--configuration",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "Configuration to build with, for example Release or Debug",
            };
        public static readonly CommandOption ARGUMENT_FRAMEWORK =
            new CommandOption
            {
                Name = "Framework",
                ShortSwitch = "-f",
                Switch = "--framework",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "Target framework to compile, for example netcoreapp1.0",
            };
        public static readonly CommandOption ARGUMENT_PACKAGE =
            new CommandOption
            {
                Name = "Package",
                ShortSwitch = "-pac",
                Switch = "--package",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "Application package to use for deployment, skips building the project",
            };
        public static readonly CommandOption ARGUMENT_FUNCTION_NAME =
            new CommandOption
            {
                Name = "Function Name",
                ShortSwitch = "-fn",
                Switch = "--function-name",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "AWS Lambda function name"
            };
        public static readonly CommandOption ARGUMENT_FUNCTION_DESCRIPTION =
            new CommandOption
            {
                Name = "Function Description",
                ShortSwitch = "-fd",
                Switch = "--function-description",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "AWS Lambda function description"
            };
        public static readonly CommandOption ARGUMENT_FUNCTION_PUBLISH =
            new CommandOption
            {
                Name = "Publish",
                ShortSwitch = "-fp",
                Switch = "--function-publish",
                ValueType = CommandOption.CommandOptionValueType.BoolValue,
                Description = "Publish a new version as an atomic operation"
            };
        public static readonly CommandOption ARGUMENT_FUNCTION_HANDLER =
            new CommandOption
            {
                Name = "Handler",
                ShortSwitch = "-fh",
                Switch = "--function-handler",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "Handler for the function <assembly>::<type>::<method>"
            };
        public static readonly CommandOption ARGUMENT_FUNCTION_MEMORY_SIZE =
            new CommandOption
            {
                Name = "Memory Size",
                ShortSwitch = "-fms",
                Switch = "--function-memory-size",
                ValueType = CommandOption.CommandOptionValueType.IntValue,
                Description = "The amount of memory, in MB, your Lambda function is given",
            };
        public static readonly CommandOption ARGUMENT_FUNCTION_ROLE =
            new CommandOption
            {
                Name = "Role",
                ShortSwitch = "-frole",
                Switch = "--function-role",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "The IAM role that Lambda assumes when it executes your function"
            };
        public static readonly CommandOption ARGUMENT_FUNCTION_TIMEOUT =
            new CommandOption
            {
                Name = "Timeout",
                ShortSwitch = "-ft",
                Switch = "--function-timeout",
                ValueType = CommandOption.CommandOptionValueType.IntValue,
                Description = "The function execution timeout in seconds"
            };
        public static readonly CommandOption ARGUMENT_FUNCTION_RUNTIME =
            new CommandOption
            {
                Name = "Runtime",
                ShortSwitch = "-frun",
                Switch = "--function-runtime",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "The runtime environment for the Lambda function"
            };
        public static readonly CommandOption ARGUMENT_FUNCTION_SUBNETS =
            new CommandOption
            {
                Name = "Subnets",
                ShortSwitch = "-fsub",
                Switch = "--function-subnets",
                ValueType = CommandOption.CommandOptionValueType.CommaDelimitedList,
                Description = "Comma delimited list of subnet ids if your function references resources in a VPC"
            };
        public static readonly CommandOption ARGUMENT_FUNCTION_SECURITY_GROUPS =
            new CommandOption
            {
                Name = "Subnets",
                ShortSwitch = "-fsec",
                Switch = "--function-security-groups",
                ValueType = CommandOption.CommandOptionValueType.CommaDelimitedList,
                Description = "Comma delimited list of security group ids if your function references resources in a VPC"
            };
        public static readonly CommandOption ARGUMENT_DEADLETTER_TARGET_ARN =
            new CommandOption
            {
                Name = "Dead Letter Target ARN",
                ShortSwitch = "-dlta",
                Switch = "--dead-letter-target-arn",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "Target ARN of an SNS topic or SQS Queue for the Dead Letter Queue"
            };
        public static readonly CommandOption ARGUMENT_ENVIRONMENT_VARIABLES =
            new CommandOption
            {
                Name = "Environment Variables",
                ShortSwitch = "-ev",
                Switch = "--environment-variables",
                ValueType = CommandOption.CommandOptionValueType.KeyValuePairs,
                Description = "Environment variables set for the function. Format is <key1>=<value1>;<key2>=<value2>"
            };
        public static readonly CommandOption ARGUMENT_KMS_KEY_ARN =
            new CommandOption
            {
                Name = "KMS Key ARN",
                ShortSwitch = "-kk",
                Switch = "--kms-key",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "KMS Key ARN of a customer key used to encrypt the function's environment variables"
            };
        public static readonly CommandOption ARGUMENT_S3_BUCKET =
            new CommandOption
            {
                Name = "S3 Bucket",
                ShortSwitch = "-sb",
                Switch = "--s3-bucket",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "S3 bucket to upload the build output"
            };
        public static readonly CommandOption ARGUMENT_S3_PREFIX =
            new CommandOption
            {
                Name = "S3 Key Prefix",
                ShortSwitch = "-sp",
                Switch = "--s3-prefix",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "S3 prefix for for the build output"
            };
        public static readonly CommandOption ARGUMENT_STACK_NAME =
            new CommandOption
            {
                Name = "CloudFormation Stack Name",
                ShortSwitch = "-sn",
                Switch = "--stack-name",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "CloudFormation stack name for an AWS Serverless application"
            };
        public static readonly CommandOption ARGUMENT_CLOUDFORMATION_TEMPLATE =
            new CommandOption
            {
                Name = "CloudFormation Template",
                ShortSwitch = "-t",
                Switch = "--template",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "Path to the CloudFormation template"
            };
        public static readonly CommandOption ARGUMENT_CLOUDFORMATION_TEMPLATE_PARAMETER =
            new CommandOption
            {
                Name = "CloudFormation Template Parameters",
                ShortSwitch = "-tp",
                Switch = "--template-parameters",
                ValueType = CommandOption.CommandOptionValueType.KeyValuePairs,
                Description = "CloudFormation template parameters. Format is <key1>=<value1>;<key2>=<value2>"
            };
        public static readonly CommandOption ARGUMENT_CLOUDFORMATION_DISABLE_CAPABILITIES =
            new CommandOption
            {
                Name = "Disable Capabilities",
                ShortSwitch = "-dc",
                Switch = "--disable-capabilities",
                ValueType = CommandOption.CommandOptionValueType.CommaDelimitedList,
                Description = "Comma delimited list of capabilities to disable when creating a CloudFormation Stack."
            };
        public static readonly CommandOption ARGUMENT_STACK_WAIT =
            new CommandOption
            {
                Name = "Stack Wait",
                ShortSwitch = "-sw",
                Switch = "--stack-wait",
                ValueType = CommandOption.CommandOptionValueType.BoolValue,
                Description = "If true wait for the Stack to finish updating before exiting. Default is true."
            };


        public static readonly CommandOption ARGUMENT_PAYLOAD =
            new CommandOption
            {
                Name = "Payload for function",
                ShortSwitch = "-p",
                Switch = "--payload",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "The input payload to send to the Lambda function"
            };

        public static readonly CommandOption ARGUMENT_OUTPUT_PACKAGE =
            new CommandOption
            {
                Name = "Payload for function",
                ShortSwitch = "-o",
                Switch = "--output-package",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = "The output zip file name"
            };


        public static readonly CommandOption ARGUMENT_CONFIG_FILE =
            new CommandOption
            {
                Name = "Config File",
                ShortSwitch = "-cfg",
                Switch = "--config-file",
                ValueType = CommandOption.CommandOptionValueType.StringValue,
                Description = $"Configuration file storing default values for command line arguments. Default is {LambdaToolsDefaultsReader.DEFAULT_FILE_NAME}"
            };
        public static readonly CommandOption ARGUMENT_PERSIST_CONFIG_FILE =
            new CommandOption
            {
                Name = "Persist Config File",
                ShortSwitch = "-pcfg",
                Switch = "--persist-config-file",
                ValueType = CommandOption.CommandOptionValueType.BoolValue,
                Description = $"If true the arguments used for a successful deployment are persisted to a config file. Default config file is {LambdaToolsDefaultsReader.DEFAULT_FILE_NAME}"
            };
    }
}