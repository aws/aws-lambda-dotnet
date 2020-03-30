using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

using Amazon.SQS;
using Amazon.SQS.Model;

namespace Amazon.Lambda.TestTool.Services
{
    public class AWSServiceImpl : IAWSService
    {
        public IList<string> ListProfiles()
        {
            var profileNames = new List<string>();

            foreach (var profile in new CredentialProfileStoreChain().ListProfiles())
            {
                // Guard against the same profile existing in both the .NET SDK encrypted store
                // and the shared credentials file. Lambda test tool does not have a mechanism
                // to specify the which store to use the profile from. 
                if (!profileNames.Contains(profile.Name))
                {
                    profileNames.Add(profile.Name);
                }
            }

            return profileNames;
        }

        public async Task<IList<string>> ListQueuesAsync(string profile, string region)
        {
            var creds = GetCredentials(profile);

            var queues = new List<string>();
            using (var client = new AmazonSQSClient(creds, RegionEndpoint.GetBySystemName(region)))
            {
                var response = await client.ListQueuesAsync(new ListQueuesRequest());
                return response.QueueUrls;
            }
        }

        public async Task<Message> ReadMessageAsync(string profile, string region, string queueUrl)
        {
            var creds = GetCredentials(profile);

            using (var client = new AmazonSQSClient(creds, RegionEndpoint.GetBySystemName(region)))
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    WaitTimeSeconds = 20,
                    MaxNumberOfMessages = 1,
                    VisibilityTimeout = 60
                };
                var response = await client.ReceiveMessageAsync(request);
                if (response.Messages.Count == 0)
                {
                    return null;
                }

                return response.Messages[0];
            }
        }

        public async Task DeleteMessageAsync(string profile, string region, string queueUrl, string receiptHandle)
        {
            var creds = GetCredentials(profile);

            using (var client = new AmazonSQSClient(creds, RegionEndpoint.GetBySystemName(region)))
            {
                var request = new DeleteMessageRequest
                {
                    QueueUrl = queueUrl,
                    ReceiptHandle = receiptHandle
                };
                await client.DeleteMessageAsync(request);
            }
        }

        public async Task PurgeQueueAsync(string profile, string region, string queueUrl)
        {
            var creds = GetCredentials(profile);

            using (var client = new AmazonSQSClient(creds, RegionEndpoint.GetBySystemName(region)))
            {
                var request = new PurgeQueueRequest()
                {
                    QueueUrl = queueUrl
                };
                await client.PurgeQueueAsync(request);
            }
        }


        static AWSCredentials GetCredentials(string profileName)
        {
            AWSCredentials credentials = null;
            if (!string.IsNullOrEmpty(profileName))
            {
                var chain = new CredentialProfileStoreChain();
                chain.TryGetAWSCredentials(profileName, out credentials);
            }
            else
            {
                credentials = FallbackCredentialsFactory.GetCredentials();
            }
            return credentials;
        }
    }
}
