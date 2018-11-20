using System;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Amazon.Lambda.TestTool.ExternalCommands
{
    public class ListQueuesCommand
    {
        string AWSProfile { get; set; }
        string AWSRegion { get; set; }

        public ListQueuesCommand(string awsProfile, string awsRegion)
        {
            this.AWSProfile = awsProfile;
            this.AWSRegion = awsRegion;
        }

        public async Task ExecuteAsync()
        {
            try
            {
                var creds = Utils.GetCredentials(this.AWSProfile);
                using (var client = new AmazonSQSClient(creds, RegionEndpoint.GetBySystemName(this.AWSRegion)))
                {
                    var response = await client.ListQueuesAsync(new ListQueuesRequest());
                    foreach (var queue in response.QueueUrls)
                    {
                        Console.WriteLine(queue);
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error listing queues: " + e.Message);
                throw;
            }
        }
    }
}