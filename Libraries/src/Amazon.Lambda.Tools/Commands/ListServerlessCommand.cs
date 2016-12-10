using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.CloudFormation.Model;

using Amazon.Lambda.Tools.Options;

namespace Amazon.Lambda.Tools.Commands
{

    /// <summary>
    /// List all the CloudFormation Stacks deployed as AWS Serverless applications.
    /// AWS Serverless applications are identified by the tag AWSServerlessAppNETCore which 
    /// was automatically assigned to the stacks by the deploy-serverless command.
    /// </summary>
    public class ListServerlessCommand : BaseCommand
    {
        public const string COMMAND_NAME = "list-serverless";
        public const string COMMAND_DESCRIPTION = "Command to list all your AWS Serverless applications";


        public static readonly IList<CommandOption> ListCommandOptions = BuildLineOptions(new List<CommandOption>
        {
        });

        public ListServerlessCommand(IToolLogger logger, string workingDirectory, string[] args)
            : base(logger, workingDirectory, ListCommandOptions, args)
        {
        }

        public override async Task<bool> ExecuteAsync()
        {
            try
            {
                const int TIMESTAMP_WIDTH = 20;
                const int STACK_NAME_WIDTH = 30;
                const int STACK_STATUS_WIDTH = 20;

                this.Logger.WriteLine("Name".PadRight(STACK_NAME_WIDTH) + " " + 
                    "Status".PadRight(STACK_STATUS_WIDTH) + " " +
                    "Created".PadRight(TIMESTAMP_WIDTH) + " " +
                    "Last Modifed".PadRight(TIMESTAMP_WIDTH)
                    );
                this.Logger.WriteLine($"{new string('-', STACK_NAME_WIDTH)} {new string('-', STACK_STATUS_WIDTH)} {new string('-', TIMESTAMP_WIDTH)} {new string('-', TIMESTAMP_WIDTH)}");

                var request = new DescribeStacksRequest();
                DescribeStacksResponse response = null;
                do
                {
                    if (response != null)
                        request.NextToken = response.NextToken;

                    try
                    {
                        response = await this.CloudFormationClient.DescribeStacksAsync(request);
                    }
                    catch (Exception e)
                    {
                        throw new LambdaToolsException("Error listing AWS Serverless applications: " + e.Message, LambdaToolsException.ErrorCode.CloudFormationDescribeStack, e);
                    }

                    foreach (var stack in response.Stacks)
                    {
                        if (stack.Tags.Any(x => string.Equals(x.Key, Constants.SERVERLESS_TAG_NAME)))
                        {
                            this.Logger.WriteLine(
                                stack.StackName.PadRight(STACK_NAME_WIDTH) + " " + 
                                stack.StackStatus.ToString().PadRight(STACK_STATUS_WIDTH) + " " + 
                                stack.CreationTime.ToString("g").PadRight(STACK_STATUS_WIDTH) + " " +
                                stack.LastUpdatedTime.ToString("g").PadRight(TIMESTAMP_WIDTH)
                                );
                        }
                    }

                } while (!string.IsNullOrEmpty(response.NextToken));
            }
            catch (LambdaToolsException e)
            {
                this.Logger.WriteLine(e.Message);
                return false;
            }
            catch (Exception e)
            {
                this.Logger.WriteLine($"Unknown error listing CloudFormation stacks: {e.Message}");
                this.Logger.WriteLine(e.StackTrace);
                return false;
            }

            return true;
        }
    }
}
