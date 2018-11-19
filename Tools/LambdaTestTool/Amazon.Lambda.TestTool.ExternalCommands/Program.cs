using System;
using Microsoft.Extensions.CommandLineUtils;

namespace Amazon.Lambda.TestTool.ExternalCommands
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "AWS .NET Lambda Test Tool External Commands";
            app.Description = "Commands the .NET Lambda Test Tool will call. These commands are separated from the .NET Lambda Test Tool process to avoid dependency collisions with the Lambda function being debugged.";
            
            app.HelpOption("-?|-h|--help");
            
            
            app.Command("list-profiles", (command) => {
                command.Description = "List all of the available AWS profiles registered on the machine.";
                command.HelpOption("-?|-h|--help");
                
 
                command.OnExecute(() => {
                    new ListProfilesCommand().Execute();
                    return 0;
                });
            });
            
            app.Command("list-queues", (command) => {
                command.Description = "List all of the SQS queues urls.";
                command.HelpOption("-?|-h|--help");
                
                var profileOption = command.Option("-p|--profile <profile>",
                    "The AWS profile identifying the AWS account to use.",
                    CommandOptionType.SingleValue);                
                var regionOption = command.Option("-r|--region <region>",
                    "The AWS region to use.",
                    CommandOptionType.SingleValue);                
 
                command.OnExecute(() => {
                    try
                    {
                        new ListQueuesCommand(profileOption.Value(), regionOption.Value()).ExecuteAsync().Wait();
                        return 0;
                    }
                    catch (Exception)
                    {
                        return -1;
                    }                    
                });
            });            

            app.Command("read-message", (command) => {
                command.Description = "Read message from SQS queue.";
                command.HelpOption("-?|-h|--help");
                
                var profileOption = command.Option("-p|--profile <profile>",
                    "The AWS profile identifying the AWS account to use.",
                    CommandOptionType.SingleValue);                
                var regionOption = command.Option("-r|--region <region>",
                    "The AWS region to use.",
                    CommandOptionType.SingleValue);                
                var queueOption = command.Option("-q|--queue <queue-url>",
                    "The SQS queue url to read a message from.",
                    CommandOptionType.SingleValue);                
 
                command.OnExecute(() => {
                    try
                    {
                        new ReadMessageCommand(profileOption.Value(), regionOption.Value(), queueOption.Value()).ExecuteAsync().Wait();
                        return 0;
                    }
                    catch (Exception)
                    {
                        return -1;
                    }
                });
            });            

            app.Command("delete-message", (command) => {
                command.Description = "Delete message from SQS queue.";
                command.HelpOption("-?|-h|--help");
                
                var profileOption = command.Option("-p|--profile <profile>",
                    "The AWS profile identifying the AWS account to use.",
                    CommandOptionType.SingleValue);                
                var regionOption = command.Option("-r|--region <region>",
                    "The AWS region to use.",
                    CommandOptionType.SingleValue);                
                var queueOption = command.Option("-q|--queue <queue-url>",
                    "The SQS queue url to read a message from.",
                    CommandOptionType.SingleValue);                
                var receiptHandleOption = command.Option("-rh|--receipt-handle <receipt-handle>",
                    "The last receipt handle read for the message.",
                    CommandOptionType.SingleValue);                
 
                command.OnExecute(() => {
                    try
                    {
                        new DeleteMessageCommand(profileOption.Value(), regionOption.Value(), queueOption.Value(), receiptHandleOption.Value()).ExecuteAsync().Wait();
                        return 0;
                    }
                    catch (Exception)
                    {
                        return -1;
                    }                    
                });
            });     
            
            app.Command("purge-queue", (command) => {
                command.Description = "Purge messages from SQS queue.";
                command.HelpOption("-?|-h|--help");
                
                var profileOption = command.Option("-p|--profile <profile>",
                    "The AWS profile identifying the AWS account to use.",
                    CommandOptionType.SingleValue);                
                var regionOption = command.Option("-r|--region <region>",
                    "The AWS region to use.",
                    CommandOptionType.SingleValue);                
                var queueOption = command.Option("-q|--queue <queue-url>",
                    "The SQS queue url to read a message from.",
                    CommandOptionType.SingleValue);                
 
                command.OnExecute(() => {
                    try
                    {
                        new PurgeQueueCommand(profileOption.Value(), regionOption.Value(), queueOption.Value()).ExecuteAsync().Wait();
                        return 0;
                    }
                    catch (Exception)
                    {
                        return -1;
                    }                    
                });
            });                
            
            app.Execute(args);
        }
    }
}
