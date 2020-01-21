using System;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Amazon.Lambda.TestTool.ExternalCommands
{
    public class PurgeQueueCommand
    {
        
        string Profile { get; }
        string Region { get; }
        private string QueueUrl { get; }

        public PurgeQueueCommand(string profile, string region, string queueUrl)
        {
            this.Profile = profile;
            this.Region = region;
            this.QueueUrl = queueUrl;
        }    
        
        public async Task ExecuteAsync()
        {
            try
            {
                var creds = Utils.GetCredentials(this.Profile);
                using (var client = new AmazonSQSClient(creds, RegionEndpoint.GetBySystemName(this.Region)))
                {
                    var request = new PurgeQueueRequest()
                    {
                        QueueUrl = this.QueueUrl
                    };
                    await client.PurgeQueueAsync(request);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error purging messages from queue: " + e.Message);
                throw;
            }
        }
    }
}