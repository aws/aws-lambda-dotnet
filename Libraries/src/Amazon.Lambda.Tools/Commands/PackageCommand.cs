using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;


using Amazon.Lambda.Tools.Options;

namespace Amazon.Lambda.Tools.Commands
{
    public class PackageCommand : BaseCommand
    {
        public const string COMMAND_NAME = "package";
        public const string COMMAND_DESCRIPTION = "Command to package a Lambda project into a zip file ready for deployment";
        public const string COMMAND_ARGUMENTS = "<ZIP-FILE> The name of the zip file to package the project into";

        public static readonly IList<CommandOption> PackageCommandOptions = new List<CommandOption>
        {
            DefinedCommandOptions.ARGUMENT_PROJECT_LOCATION,
            DefinedCommandOptions.ARGUMENT_CONFIGURATION,
            DefinedCommandOptions.ARGUMENT_FRAMEWORK,
            DefinedCommandOptions.ARGUMENT_OUTPUT_PACKAGE
        };

        public string Configuration { get; set; }
        public string TargetFramework { get; set; }
        public string OutputPackageFileName { get; set; }

        /// <summary>
        /// If the value for Payload points to an existing file then the contents of the file is sent, otherwise
        /// value of Payload is sent as the input to the function.
        /// </summary>
        public string Payload { get; set; }

        public PackageCommand(IToolLogger logger, string workingDirectory, string[] args)
            : base(logger, workingDirectory, PackageCommandOptions, args)
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
                this.OutputPackageFileName = values.Arguments[0];
            }

            Tuple<CommandOption, CommandOptionValue> tuple;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_CONFIGURATION.Switch)) != null)
                this.Configuration = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FRAMEWORK.Switch)) != null)
                this.TargetFramework = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_OUTPUT_PACKAGE.Switch)) != null)
                this.OutputPackageFileName = tuple.Item2.StringValue;
        }

        public override Task<bool> ExecuteAsync()
        {
            return Task.Run(() =>
            {

                try
                {
                    string configuration = this.GetStringValueOrDefault(this.Configuration, DefinedCommandOptions.ARGUMENT_CONFIGURATION, true);
                    string targetFramework = this.GetStringValueOrDefault(this.TargetFramework, DefinedCommandOptions.ARGUMENT_FRAMEWORK, true);
                    string projectLocation = this.GetStringValueOrDefault(this.ProjectLocation, DefinedCommandOptions.ARGUMENT_PROJECT_LOCATION, false);

                    var zipArchivePath = GetStringValueOrDefault(this.OutputPackageFileName, DefinedCommandOptions.ARGUMENT_OUTPUT_PACKAGE, false);

                    string publishLocation;
                    bool success = Utilities.CreateApplicationBundle(this.DefaultConfig, this.Logger, this.WorkingDirectory, projectLocation, configuration, targetFramework, out publishLocation, ref zipArchivePath);
                    if (!success)
                    {
                        this.Logger.WriteLine("Failed to create application package");
                        return false;
                    }


                    this.Logger.WriteLine("Lambda project successfully packaged: " + zipArchivePath);
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
                    this.Logger.WriteLine($"Unknown error executing Lambda packaging: {e.Message}");
                    this.Logger.WriteLine(e.StackTrace);
                    return false;
                }
            });
        }
    }
}