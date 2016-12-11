using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

using Amazon.Lambda;
using Amazon.Lambda.Model;

using Amazon.Lambda.Tools.Options;

namespace Amazon.Lambda.Tools.Commands
{
    /// <summary>
    /// Command to delete a function
    /// </summary>
    public class DeleteFunctionCommand : BaseCommand
    {
        public const string COMMAND_NAME = "delete-function";
        public const string COMMAND_DESCRIPTION = "Command to delete an AWS Lambda function";
        public const string COMMAND_ARGUMENTS = "<FUNCTION-NAME> The name of the function to delete";



        public static readonly IList<CommandOption> DeleteCommandOptions = BuildLineOptions(new List<CommandOption>
        {
            DefinedCommandOptions.ARGUMENT_FUNCTION_NAME
        });

        public string FunctionName { get; set; }


        public DeleteFunctionCommand(IToolLogger logger, string workingDirectory, string[] args)
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
                this.FunctionName = values.Arguments[0];
            }

            Tuple<CommandOption, CommandOptionValue> tuple;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_NAME.Switch)) != null)
                this.FunctionName = tuple.Item2.StringValue;
        }

        public override async Task<bool> ExecuteAsync()
        {
            try
            {
                var deleteRequest = new DeleteFunctionRequest
                {
                    FunctionName = this.GetStringValueOrDefault(this.FunctionName, DefinedCommandOptions.ARGUMENT_FUNCTION_NAME, true)
                };


                try
                {
                    await this.LambdaClient.DeleteFunctionAsync(deleteRequest);
                }
                catch(Exception e)
                {
                    throw new LambdaToolsException("Error deleting Lambda function: " + e.Message, LambdaToolsException.ErrorCode.LambdaDeleteFunction, e);
                }

                this.Logger.WriteLine($"Lambda function {deleteRequest.FunctionName} deleted");
            }
            catch (LambdaToolsException e)
            {
                this.Logger.WriteLine(e.Message);
                this.LastToolsException = e;
                return false;
            }
            catch (Exception e)
            {
                this.Logger.WriteLine($"Unknown error deleting Lambda function: {e.Message}");
                this.Logger.WriteLine(e.StackTrace);
                return false;
            }

            return true;
        }
    }
}
