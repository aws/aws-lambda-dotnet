using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;


using Amazon.Lambda.Tools.Options;

namespace Amazon.Lambda.Tools.Commands
{
    public class PackageCICommand : BaseCommand
    {
        public const string COMMAND_NAME = "package-ci";
        public const string COMMAND_SYNOPSIS = "Command to use as part of a continuous integration system.";
        public const string COMMAND_DESCRIPTION =
            "Command for use as part of the build step in a continuous integration pipeline. To perform the deployment this command requires a CloudFormation template similar to the one used by Serverless projects. " +
            "The command performs the following actions: \n" +
            "\t 1) Build and package .NET Core project\n" +
            "\t 2) Upload build archive to Amazon S3\n" +
            "\t 3) Read in AWS CloudFormation template\n" +
            "\t 4) Update AWS::Lambda::Function and AWS::Serverless::Function resources to the location of the uploaded build archive\n" +
            "\t 5) Write out updated CloudFormation template\n\n" +
            "The output CloudFormation template should be used as the build step's output artifact. The deployment stage of the pipeline will use the outputted template to create a CloudFormation ChangeSet and then execute ChangeSet.";

        public static readonly IList<CommandOption> PackageCICommandOptions = BuildLineOptions(new List<CommandOption>
        {
            DefinedCommandOptions.ARGUMENT_CONFIGURATION,
            DefinedCommandOptions.ARGUMENT_FRAMEWORK,
            DefinedCommandOptions.ARGUMENT_MSBUILD_PARAMETERS,
            DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE,
            DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE_SUBSTITUTIONS,
            DefinedCommandOptions.ARGUMENT_OUTPUT_CLOUDFORMATION_TEMPLATE,
            DefinedCommandOptions.ARGUMENT_S3_BUCKET,
            DefinedCommandOptions.ARGUMENT_S3_PREFIX,
            DefinedCommandOptions.ARGUMENT_DISABLE_VERSION_CHECK
        });

        public string Configuration { get; set; }
        public string TargetFramework { get; set; }
        public string MSBuildParameters { get; set; }

        public string S3Bucket { get; set; }
        public string S3Prefix { get; set; }

        public string CloudFormationTemplate { get; set; }
        public Dictionary<string, string> TemplateSubstitutions { get; set; }

        public bool? DisableVersionCheck { get; set; }

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
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE_SUBSTITUTIONS.Switch)) != null)
                this.TemplateSubstitutions = tuple.Item2.KeyValuePairs;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_OUTPUT_CLOUDFORMATION_TEMPLATE.Switch)) != null)
                this.CloudFormationOutputTemplate = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_S3_BUCKET.Switch)) != null)
                this.S3Bucket = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_S3_PREFIX.Switch)) != null)
                this.S3Prefix = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_DISABLE_VERSION_CHECK.Switch)) != null)
                this.DisableVersionCheck = tuple.Item2.BoolValue;

            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_MSBUILD_PARAMETERS.Switch)) != null)
                this.MSBuildParameters = tuple.Item2.StringValue;

            if (!string.IsNullOrEmpty(values.MSBuildParameters))
            {
                if (this.MSBuildParameters == null)
                    this.MSBuildParameters = values.MSBuildParameters;
                else
                    this.MSBuildParameters += " " + values.MSBuildParameters;
            }
        }

        public PackageCICommand(IToolLogger logger, string workingDirectory, string[] args)
            : base(logger, workingDirectory, PackageCICommandOptions, args)
        {
        }

        public override async Task<bool> ExecuteAsync()
        {
            // Disable interactive since this command is intended to be run as part of a pipeline.
            DisableInteractive = true;

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
            string msbuildParameters = this.GetStringValueOrDefault(this.MSBuildParameters, DefinedCommandOptions.ARGUMENT_MSBUILD_PARAMETERS, false);
            bool disableVersionCheck = this.GetBoolValueOrDefault(this.DisableVersionCheck, DefinedCommandOptions.ARGUMENT_DISABLE_VERSION_CHECK, false).GetValueOrDefault();
            string publishLocation;
            LambdaPackager.CreateApplicationBundle(this.DefaultConfig, this.Logger, this.WorkingDirectory, projectLocation, configuration, targetFramework, msbuildParameters, disableVersionCheck, out publishLocation, ref zipArchivePath);
            if (string.IsNullOrEmpty(zipArchivePath))
                return false;

            string s3KeyApplicationBundle;
            using (var stream = new MemoryStream(File.ReadAllBytes(zipArchivePath)))
            {
                s3KeyApplicationBundle = await Utilities.UploadToS3Async(this.Logger, this.S3Client, s3Bucket, s3Prefix, Path.GetFileName(zipArchivePath), stream);
            }

            this.Logger.WriteLine($"Updating CloudFormation template to point to application bundle: s3://{s3Bucket}/{s3KeyApplicationBundle}");
            var templateBody = File.ReadAllText(templatePath);

            // Process any template substitutions
            templateBody = Utilities.ProcessTemplateSubstitions(this.Logger, templateBody, this.GetKeyValuePairOrDefault(this.TemplateSubstitutions, DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_TEMPLATE_SUBSTITUTIONS, false), Utilities.DetermineProjectLocation(this.WorkingDirectory, projectLocation));

            var transformedBody = Utilities.UpdateCodeLocationInTemplate(templateBody, s3Bucket, s3KeyApplicationBundle);

            this.Logger.WriteLine($"Writing updated template: {outputTemplatePath}");
            File.WriteAllText(outputTemplatePath, transformedBody);


            return true;
        }
    }
}
