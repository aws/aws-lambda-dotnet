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
    /// Invoke a function running in Lambda
    /// </summary>
    public class InvokeFunctionCommand : BaseCommand
    {
        public const string COMMAND_NAME = "invoke-function";
        public const string COMMAND_DESCRIPTION = "Command to invoke a function in Lambda with an optional input";
        public const string COMMAND_ARGUMENTS = "<FUNCTION-NAME> The name of the function to invoke";


        public static readonly IList<CommandOption> InvokeCommandOptions = BuildLineOptions(new List<CommandOption>
        {
            DefinedCommandOptions.ARGUMENT_FUNCTION_NAME,
            DefinedCommandOptions.ARGUMENT_PAYLOAD
        });

        public string FunctionName { get; set; }

        /// <summary>
        /// If the value for Payload points to an existing file then the contents of the file is sent, otherwise
        /// the value of Payload is sent as the input to the function.
        /// </summary>
        public string Payload { get; set; }

        public InvokeFunctionCommand(IToolLogger logger, string workingDirectory, string[] args)
            : base(logger, workingDirectory, InvokeCommandOptions, args)
        {
        }


        /// <summary>
        /// Parse the CommandOptions into the Properties on the command.
        /// </summary>
        /// <param name="values"></param>
        protected override void ParseCommandArguments(CommandOptions values)
        {
            base.ParseCommandArguments(values);

            if(values.Arguments.Count > 0)
            {
                this.FunctionName = values.Arguments[0];
            }

            Tuple<CommandOption, CommandOptionValue> tuple;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_FUNCTION_NAME.Switch)) != null)
                this.FunctionName = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_PAYLOAD.Switch)) != null)
                this.Payload = tuple.Item2.StringValue;
        }

        public override async Task<bool> ExecuteAsync()
        {
            try
            {
                var invokeRequest = new InvokeRequest
                {
                    FunctionName = this.GetStringValueOrDefault(this.FunctionName, DefinedCommandOptions.ARGUMENT_FUNCTION_NAME, true),
                    LogType = LogType.Tail
                };

                if (!string.IsNullOrWhiteSpace(this.Payload))
                {
                    if (File.Exists(this.Payload))
                    {
                        Logger.WriteLine($"Reading {Path.GetFullPath(this.Payload)} as input to Lambda function");
                        invokeRequest.Payload = File.ReadAllText(this.Payload);
                    }
                    else
                    {
                        invokeRequest.Payload = this.Payload.Trim();
                    }

                    if(!invokeRequest.Payload.StartsWith("{"))
                    {
                        invokeRequest.Payload = "\"" + invokeRequest.Payload + "\"";
                    }
                }

                InvokeResponse response = null;
                try
                {
                    response = await this.LambdaClient.InvokeAsync(invokeRequest);
                }
                catch(Exception e)
                {
                    throw new LambdaToolsException("Error invoking Lambda function: " + e.Message);
                }

                this.Logger.WriteLine("Payload:");

                PrintPayload(response);

                this.Logger.WriteLine("");
                this.Logger.WriteLine("Log Tail:");
                var log = System.Text.UTF8Encoding.UTF8.GetString(Convert.FromBase64String(response.LogResult));
                this.Logger.WriteLine(log);

            }
            catch (LambdaToolsException e)
            {
                this.Logger.WriteLine(e.Message);
                return false;
            }
            catch (Exception e)
            {
                this.Logger.WriteLine($"Unknown error invoking Lambda function: {e.Message}");
                this.Logger.WriteLine(e.StackTrace);
                return false;
            }

            return true;
        }


        private void PrintPayload(InvokeResponse response)
        {
            try
            {
                var payload = new StreamReader(response.Payload).ReadToEnd();
                this.Logger.WriteLine(payload);
            }
            catch (Exception)
            {
                this.Logger.WriteLine("<unparseable data>");
            }
        }
    }
}
