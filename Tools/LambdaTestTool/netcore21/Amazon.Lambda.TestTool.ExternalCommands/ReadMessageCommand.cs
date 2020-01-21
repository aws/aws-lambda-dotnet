using System;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Amazon.Lambda.TestTool.ExternalCommands
{
    public class ReadMessageCommand
    {
        string AWSProfile { get; set; }
        string AWSRegion { get; set; }
        private string QueueUrl { get; set; }

        public ReadMessageCommand(string awsProfile, string awsRegion, string queueUrl)
        {
            this.AWSProfile = awsProfile;
            this.AWSRegion = awsRegion;
            this.QueueUrl = queueUrl;
        }

        public async Task ExecuteAsync()
        {
            try
            {
                var creds = Utils.GetCredentials(this.AWSProfile);
                using (var client = new AmazonSQSClient(creds, RegionEndpoint.GetBySystemName(this.AWSRegion)))
                {
                    var request = new ReceiveMessageRequest
                    {
                        QueueUrl = this.QueueUrl,
                        WaitTimeSeconds = 20,
                        MaxNumberOfMessages = 1,
                        VisibilityTimeout = 60
                    };
                    var response = await client.ReceiveMessageAsync(request);
                    if (response.Messages.Count == 1)
                    {
                        var message = response.Messages[0];
                        var str = JsonConvert.SerializeObject(message);
                        Console.WriteLine(str);
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error receiving message from queue: " + e.Message);
                throw;
            }
        }
        
    }
}