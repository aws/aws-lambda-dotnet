using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.ElasticLoadBalancingV2;
using Amazon.Lambda;
using Amazon.S3;
using IntegrationTests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestServerlessApp.ALB.IntegrationTests
{
    public class ALBIntegrationTestContextFixture : IAsyncLifetime
    {
        private readonly CloudFormationHelper _cloudFormationHelper;
        private readonly S3Helper _s3Helper;

        private string _stackName;
        private string _bucketName;

        public readonly AmazonElasticLoadBalancingV2Client ELBv2Client;
        public readonly LambdaHelper LambdaHelper;
        public readonly HttpClient HttpClient;

        public string ALBDnsName;

        public ALBIntegrationTestContextFixture()
        {
            _cloudFormationHelper = new CloudFormationHelper(new AmazonCloudFormationClient(Amazon.RegionEndpoint.USWest2));
            _s3Helper = new S3Helper(new AmazonS3Client(Amazon.RegionEndpoint.USWest2));
            LambdaHelper = new LambdaHelper(new AmazonLambdaClient(Amazon.RegionEndpoint.USWest2));
            ELBv2Client = new AmazonElasticLoadBalancingV2Client(Amazon.RegionEndpoint.USWest2);
            HttpClient = new HttpClient();
        }

        public async Task InitializeAsync()
        {
            var scriptPath = Path.Combine("..", "..", "..", "DeploymentScript.ps1");
            Console.WriteLine($"[ALB IntegrationTest] Running deployment script: {scriptPath}");
            await CommandLineWrapper.RunAsync($"pwsh {scriptPath}");
            Console.WriteLine("[ALB IntegrationTest] Deployment script completed successfully.");

            _stackName = GetConfigValue("stack-name");
            _bucketName = GetConfigValue("s3-bucket");
            Console.WriteLine($"[ALB IntegrationTest] Stack name: '{_stackName}', Bucket name: '{_bucketName}'");
            Assert.False(string.IsNullOrEmpty(_stackName), "Stack name should not be empty");
            Assert.False(string.IsNullOrEmpty(_bucketName), "Bucket name should not be empty");

            // Check stack status
            var stackStatus = await _cloudFormationHelper.GetStackStatusAsync(_stackName);
            Console.WriteLine($"[ALB IntegrationTest] Stack status: {stackStatus}");
            Assert.NotNull(stackStatus);
            Assert.Equal(StackStatus.CREATE_COMPLETE, stackStatus);

            // Get ALB DNS name from stack outputs
            ALBDnsName = await _cloudFormationHelper.GetOutputValueAsync(_stackName, "ALBDnsName");
            Console.WriteLine($"[ALB IntegrationTest] ALB DNS Name: {ALBDnsName}");
            Assert.False(string.IsNullOrEmpty(ALBDnsName), "ALB DNS Name should not be empty");

            // Wait for Lambda targets to become healthy
            Console.WriteLine("[ALB IntegrationTest] Waiting for targets to become healthy...");
            await Task.Delay(30000); // Wait 30s for targets to register and become healthy
        }

        public async Task DisposeAsync()
        {
            if (!string.IsNullOrEmpty(_stackName))
            {
                Console.WriteLine($"[ALB IntegrationTest] Cleaning up stack '{_stackName}'...");
                await _cloudFormationHelper.DeleteStackAsync(_stackName);
                Assert.True(await _cloudFormationHelper.IsDeletedAsync(_stackName),
                    $"The stack '{_stackName}' still exists and will have to be manually deleted.");
            }

            if (!string.IsNullOrEmpty(_bucketName))
            {
                Console.WriteLine($"[ALB IntegrationTest] Cleaning up bucket '{_bucketName}'...");
                await _s3Helper.DeleteBucketAsync(_bucketName);
                Assert.False(await _s3Helper.BucketExistsAsync(_bucketName),
                    $"The bucket '{_bucketName}' still exists and will have to be manually deleted.");
            }

            // Reset aws-lambda-tools-defaults.json
            var filePath = Path.Combine("..", "..", "..", "..", "TestServerlessApp.ALB", "aws-lambda-tools-defaults.json");
            var token = JObject.Parse(await File.ReadAllTextAsync(filePath));
            token["s3-bucket"] = "test-serverless-app-alb";
            token["stack-name"] = "test-serverless-app-alb";
            await File.WriteAllTextAsync(filePath, token.ToString(Formatting.Indented));
        }

        private string GetConfigValue(string key)
        {
            var filePath = Path.Combine("..", "..", "..", "..", "TestServerlessApp.ALB", "aws-lambda-tools-defaults.json");
            var token = JObject.Parse(File.ReadAllText(filePath))[key];
            return token?.ToObject<string>();
        }
    }
}
