using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

using Amazon.CloudFormation.Model;

using Amazon.Lambda.Tools.Options;

namespace Amazon.Lambda.Tools.Commands
{
    public class DeleteServerlessCommand : BaseCommand
    {
        public const string COMMAND_NAME = "delete-serverless";
        public const string COMMAND_DESCRIPTION = "Command to delete an AWS Serverless application";
        public const string COMMAND_ARGUMENTS = "<STACK-NAME> The CloudFormation stack for the AWS Serverless application";



        public static readonly IList<CommandOption> DeleteCommandOptions = BuildLineOptions(new List<CommandOption>
        {
            DefinedCommandOptions.ARGUMENT_STACK_NAME
        });

        public string StackName { get; set; }


        public DeleteServerlessCommand(IToolLogger logger, string workingDirectory, string[] args)
            : base(logger, workingDirectory, DeleteCommandOptions, args)
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
                this.StackName = values.Arguments[0];
            }

            Tuple<CommandOption, CommandOptionValue> tuple;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_STACK_NAME.Switch)) != null)
                this.StackName = tuple.Item2.StringValue;
        }

        public override async Task<bool> ExecuteAsync()
        {
            try
            {
                var deleteRequest = new DeleteStackRequest
                {
                    StackName = this.GetStringValueOrDefault(this.StackName, DefinedCommandOptions.ARGUMENT_STACK_NAME, true)
                };


                try
                {
                    await this.CloudFormationClient.DeleteStackAsync(deleteRequest);
                }
                catch (Exception e)
                {
                    throw new LambdaToolsException("Error deleting CloudFormation stack: " + e.Message);
                }

                this.Logger.WriteLine($"CloudFormation stack {deleteRequest.StackName} deleted");
            }
            catch (LambdaToolsException e)
            {
                this.Logger.WriteLine(e.Message);
                return false;
            }
            catch (Exception e)
            {
                this.Logger.WriteLine($"Unknown error deleting CloudFormation stack: {e.Message}");
                this.Logger.WriteLine(e.StackTrace);
                return false;
            }

            return true;
        }
    }
}
