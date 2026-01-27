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
            return Path.GetFullPath($"../../../../LambdaFunctions/{projectName}/{fileName}");
        }

        public static string GetLambdaFunctionBuildPath(string projectName)
        {
            return Path.GetFullPath($"../../../../LambdaFunctions/{projectName}/bin/Debug/{GetTargetFramework()}");
        }

        public static string GetTargetFramework()
        {
#if NET8_0
            return "net8.0";
#elif NET10_0
            return "net10.0";
#else
            Compile error you need to add a new target framework
#endif
        }
    }
}
