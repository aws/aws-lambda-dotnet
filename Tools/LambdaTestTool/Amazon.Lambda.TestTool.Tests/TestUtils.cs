using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Amazon.Lambda.TestTool.Tests
{
    public static class TestUtils
    {
        public static string TestProfile
        {
            get
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LAMBDA_TEST_TOOL_TEST_PROFILE")))
                    return "default";
                
                return Environment.GetEnvironmentVariable("LAMBDA_TEST_TOOL_TEST_PROFILE");

            }
        }
        
        
        public static RegionEndpoint TestRegion
        {
            get
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LAMBDA_TEST_TOOL_TEST_REGION")))
                    return RegionEndpoint.USEast1;
                var region = Environment.GetEnvironmentVariable("LAMBDA_TEST_TOOL_TEST_REGION");
                return RegionEndpoint.GetBySystemName(region);

            }
        }

        public static AWSCredentials GetAWSCredentials()
        {
            var chain = new CredentialProfileStoreChain();
            if (!chain.TryGetAWSCredentials(TestProfile, out var awsCredentials))
            {
                throw new Exception("These tests assume a profile with the name \"default\" is registered");
            }

            return awsCredentials;
        }

        public static async Task WaitTillQueueIsCreatedAsync(IAmazonSQS sqsClient, string queueUrl)
        {
            while (true)
            {
                var response = await sqsClient.ListQueuesAsync(new ListQueuesRequest());
                if(response.QueueUrls.Contains(queueUrl))
                    break;
                else
                    Thread.Sleep(100);
            }
        }
    }
}