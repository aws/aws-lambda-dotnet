using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda;
using Amazon.Lambda.Model;

using Amazon.Lambda.Tools.Options;

namespace Amazon.Lambda.Tools.Commands
{
    /// <summary>
    /// List all the functions currently deployed to Lambda
    /// </summary>
    public class ListFunctionCommand : BaseCommand
    {
        public const string COMMAND_NAME = "list-functions";
        public const string COMMAND_DESCRIPTION = "Command to list all your Lambda functions";


        public static readonly IList<CommandOption> ListCommandOptions = BuildLineOptions(new List<CommandOption>
        {
        });

        public ListFunctionCommand(IToolLogger logger, string workingDirectory, string[] args)
            : base(logger, workingDirectory, ListCommandOptions, args)
        {
        }

        public override async Task<bool> ExecuteAsync()
        {
            try
            {
                ListFunctionsRequest request = new ListFunctionsRequest();
                ListFunctionsResponse response = null;
                do
                {
                    if (response != null)
                        request.Marker = response.NextMarker;

                    try
                    {
                        response = await this.LambdaClient.ListFunctionsAsync(request);
                    }
                    catch (Exception e)
                    {
                        throw new LambdaToolsException("Error listing Lambda functions: " + e.Message, LambdaToolsException.ErrorCode.LambdaListFunctions, e);
                    }

                    foreach (var function in response.Functions)
                    {
                        this.Logger.WriteLine((function.FunctionName.PadRight(40) + " (" + function.Runtime + ")").PadRight(10) + "\t" + function.Description);
                    }

                } while (!string.IsNullOrEmpty(response.NextMarker));
            }
            catch (LambdaToolsException e)
            {
                this.Logger.WriteLine(e.Message);
                return false;
            }
            catch (Exception e)
            {
                this.Logger.WriteLine($"Unknown error listing Lambda functions: {e.Message}");
                this.Logger.WriteLine(e.StackTrace);
                return false;
            }

            return true;
        }
    }
}