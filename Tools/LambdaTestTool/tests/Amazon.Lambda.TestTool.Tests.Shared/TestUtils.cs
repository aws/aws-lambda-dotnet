using System;
using System.IO;
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

        public static bool ProfileTestsEnabled
        {
            get
            {
                var profiles = new CredentialProfileStoreChain().ListProfiles();
                return profiles.Count > 0;
            }
        }

        public static string GetLambdaFunctionSourceFile(string projectName, string fileName)
        {
#if NETCORE_2_1
            return Path.GetFullPath($"../../../../LambdaFunctions/netcore21/{projectName}/{fileName}");
#elif NETCORE_3_1
            return Path.GetFullPath($"../../../../LambdaFunctions/netcore31/{projectName}/{fileName}");
#endif            
        }

        public static string GetLambdaFunctionBuildPath(string projectName)
        {
#if NETCORE_2_1
            return Path.GetFullPath($"../../../../LambdaFunctions/netcore21/{projectName}/bin/debug/netcoreapp2.1");
#elif NETCORE_3_1
            return Path.GetFullPath($"../../../../LambdaFunctions/netcore31/{projectName}/bin/debug/netcoreapp3.1");
#endif            
        }
    }
}