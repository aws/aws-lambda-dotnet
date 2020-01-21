using System;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;

namespace Amazon.Lambda.TestTool.ExternalCommands
{
    public class DeleteMessageCommand
    {
        
        string Profile { get; }
        string Region { get; }
        private string QueueUrl { get; }
        private string ReceiptHandle { get; }

        public DeleteMessageCommand(string profile, string region, string queueUrl, string receiptHandle)
        {
            this.Profile = profile;
            this.Region = region;
            this.QueueUrl = queueUrl;
            this.ReceiptHandle = receiptHandle;
        }    
        
        public async Task ExecuteAsync()
        {
            try
            {
                var creds = Utils.GetCredentials(this.Profile);
                using (var client = new AmazonSQSClient(creds, RegionEndpoint.GetBySystemName(this.Region)))
                {
                    var request = new DeleteMessageRequest
                    {
                        QueueUrl = this.QueueUrl,
                        ReceiptHandle = this.ReceiptHandle
                    };
                    await client.DeleteMessageAsync(request);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error deleting message from queue: " + e.Message);
                throw;
            }
        }
    }
}