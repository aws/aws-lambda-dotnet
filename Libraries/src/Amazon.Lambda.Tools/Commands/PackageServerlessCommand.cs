using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;


using Amazon.Lambda.Tools.Options;

namespace Amazon.Lambda.Tools.Commands
{
    public class PackageServerlessCommand : BaseCommand
    {
        public const string COMMAND_NAME = "package-serverless";
        public const string COMMAND_DESCRIPTION = "Command to servlerless pipeline.";

        public static readonly IList<CommandOption> ServerlessPackageCommandOptions = BuildLineOptions(new List<CommandOption>
        {
            DefinedCommandOptions.ARGUMENT_CONFIGURATION,
            DefinedCommandOptions.ARGUMENT_FRAMEWORK,
            DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE,
            DefinedCommandOptions.ARGUMENT_OUTPUT_CLOUDFORMATION_TEMPLATE,
            DefinedCommandOptions.ARGUMENT_S3_BUCKET,
            DefinedCommandOptions.ARGUMENT_S3_PREFIX
        });

        public string Configuration { get; set; }
        public string TargetFramework { get; set; }

        public string S3Bucket { get; set; }
        public string S3Prefix { get; set; }

        public string CloudFormationTemplate { get; set; }

        public string CloudFormationOutputTemplate { get; set; }

        /// <summary>
        /// Parse the CommandOptions into the Properties on the command.
        /// </summary>
        /// <param name="values"></param>
        protected override void ParseCommandArguments(CommandOptions values)
        {
            base.ParseCommandArguments(values);

            Tuple<CommandOption, CommandOptionValue> tuple;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_CONFIGURATION.Switch)) != null)
                this.Configuration = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FRAMEWORK.Switch)) != null)
                this.TargetFramework = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE.Switch)) != null)
                this.CloudFormationTemplate = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_OUTPUT_CLOUDFORMATION_TEMPLATE.Switch)) != null)
                this.CloudFormationOutputTemplate = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_S3_BUCKET.Switch)) != null)
                this.S3Bucket = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_S3_PREFIX.Switch)) != null)
                this.S3Prefix = tuple.Item2.StringValue;
        }

        public PackageServerlessCommand(IToolLogger logger, string workingDirectory, string[] args)
            : base(logger, workingDirectory, ServerlessPackageCommandOptions, args)
        {
        }

        public override async Task<bool> ExecuteAsync()
        {
            // Disable interactive since this command is intended to be run as part of a pipeline.
            EnableInteractive = false;

            string projectLocation = this.GetStringValueOrDefault(this.ProjectLocation, DefinedCommandOptions.ARGUMENT_PROJECT_LOCATION, false);
            string s3Bucket = this.GetStringValueOrDefault(this.S3Bucket, DefinedCommandOptions.ARGUMENT_S3_BUCKET, true);
            string s3Prefix = this.GetStringValueOrDefault(this.S3Prefix, DefinedCommandOptions.ARGUMENT_S3_PREFIX, false);
            string templatePath = this.GetStringValueOrDefault(this.CloudFormationTemplate, DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE, true);
            string outputTemplatePath = this.GetStringValueOrDefault(this.CloudFormationOutputTemplate, DefinedCommandOptions.ARGUMENT_OUTPUT_CLOUDFORMATION_TEMPLATE, true);

            if (!Path.IsPathRooted(templatePath))
            {
                templatePath = Path.Combine(Utilities.DetermineProjectLocation(this.WorkingDirectory, projectLocation), templatePath);
            }

            if (!File.Exists(templatePath))
                throw new LambdaToolsException($"Template file {templatePath} cannot be found.", LambdaToolsException.ErrorCode.ServerlessTemplateNotFound);

            await Utilities.ValidateBucketRegionAsync(this.S3Client, s3Bucket);

            string zipArchivePath = null;
            string configuration = this.GetStringValueOrDefault(this.Configuration, DefinedCommandOptions.ARGUMENT_CONFIGURATION, true);
            string targetFramework = this.GetStringValueOrDefault(this.TargetFramework, DefinedCommandOptions.ARGUMENT_FRAMEWORK, true);
            string publishLocation;
            LambdaPackager.CreateApplicationBundle(this.DefaultConfig, this.Logger, this.WorkingDirectory, projectLocation, configuration, targetFramework, out publishLocation, ref zipArchivePath);
            if (string.IsNullOrEmpty(zipArchivePath))
                return false;

            string s3KeyApplicationBundle;
            using (var stream = new MemoryStream(File.ReadAllBytes(zipArchivePath)))
            {
                s3KeyApplicationBundle = await Utilities.UploadToS3Async(this.Logger, this.S3Client, s3Bucket, s3Prefix, Path.GetFileName(zipArchivePath), stream);
            }

            this.Logger.WriteLine($"Updating cloudformation template to point to application bundle: s3://{s3Bucket}/{s3KeyApplicationBundle}");
            var templateBody = File.ReadAllText(templatePath);
            var transformedBody = DeployServerlessCommand.UpdateCodeLocationInTemplate(templateBody, s3Bucket, s3KeyApplicationBundle);

            this.Logger.WriteLine($"Writing updated template: {outputTemplatePath}");
            File.WriteAllText(outputTemplatePath, transformedBody);


            return true;
        }
    }
}
