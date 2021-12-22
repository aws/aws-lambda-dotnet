using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudWatchLogs;
using Amazon.Lambda;
using Amazon.S3;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestServerlessApp.IntegrationTests.Helpers;
using Xunit;

namespace TestServerlessApp.IntegrationTests
{
    public class IntegrationTestContextFixture : IAsyncLifetime
    {
        private readonly CloudFormationHelper _cloudFormationHelper;
        private readonly S3Helper _s3Helper;

        private string _stackName;
        private string _bucketName;

        public readonly LambdaHelper LambdaHelper;
        public readonly CloudWatchHelper CloudWatchHelper;
        public readonly HttpClient HttpClient;

        public string RestApiUrlPrefix;
        public string HttpApiUrlPrefix;
        public List<LambdaFunction> LambdaFunctions;

        public IntegrationTestContextFixture()
        {
            _cloudFormationHelper = new CloudFormationHelper(new AmazonCloudFormationClient(Amazon.RegionEndpoint.USWest2));
            _s3Helper = new S3Helper(new AmazonS3Client(Amazon.RegionEndpoint.USWest2));
            LambdaHelper = new LambdaHelper(new AmazonLambdaClient(Amazon.RegionEndpoint.USWest2));
            CloudWatchHelper = new CloudWatchHelper(new AmazonCloudWatchLogsClient(Amazon.RegionEndpoint.USWest2));
            HttpClient = new HttpClient();
        }

        public async Task InitializeAsync()
        {
            var scriptPath = Path.Combine("..", "..", "..", "DeploymentScript.ps1");
            await CommandLineWrapper.RunAsync($"pwsh {scriptPath}");

            _stackName = GetStackName();
            _bucketName = GetBucketName();
            Assert.False(string.IsNullOrEmpty(_stackName));
            Assert.False(string.IsNullOrEmpty(_bucketName));

            RestApiUrlPrefix = await _cloudFormationHelper.GetOutputValueAsync(_stackName, "RestApiURL");
            HttpApiUrlPrefix = await _cloudFormationHelper.GetOutputValueAsync(_stackName, "HttpApiURL");
            LambdaFunctions = await LambdaHelper.FilterByCloudFormationStackAsync(_stackName);

            Assert.Equal(StackStatus.CREATE_COMPLETE, await _cloudFormationHelper.GetStackStatusAsync(_stackName));
            Assert.True(await _s3Helper.BucketExistsAsync(_bucketName));
            Assert.Equal(11, LambdaFunctions.Count);
            Assert.False(string.IsNullOrEmpty(RestApiUrlPrefix));
            Assert.False(string.IsNullOrEmpty(RestApiUrlPrefix));

            await LambdaHelper.WaitTillNotPending(LambdaFunctions.Select(x => x.Name).ToList());
        }

        public async Task DisposeAsync()
        {
            await _cloudFormationHelper.DeleteStackAsync(_stackName);
            Assert.True(await _cloudFormationHelper.IsDeletedAsync(_stackName), $"The stack '{_stackName}' still exists and will have to be manually deleted from the AWS console.");

            await _s3Helper.DeleteBucketAsync(_bucketName);
            Assert.False(await _s3Helper.BucketExistsAsync(_bucketName), $"The bucket '{_bucketName}' still exists and will have to be manually deleted from the AWS console.");

            var filePath = Path.Combine("..", "..", "..", "..", "TestServerlessApp", "aws-lambda-tools-defaults.json");
            var token = JObject.Parse(await File.ReadAllTextAsync(filePath));
            token["s3-bucket"] = "test-serverless-app";
            token["stack-name"] = "test-serverless-app";
            await File.WriteAllTextAsync(filePath, token.ToString(Formatting.Indented));
        }

        private string GetStackName()
        {
            var filePath = Path.Combine("..", "..", "..", "..", "TestServerlessApp", "aws-lambda-tools-defaults.json");
            var token = JObject.Parse(File.ReadAllText(filePath))["stack-name"];
            return token.ToObject<string>();
        }

        private string GetBucketName()
        {
            var filePath = Path.Combine("..", "..", "..", "..", "TestServerlessApp", "aws-lambda-tools-defaults.json");
            var token = JObject.Parse(File.ReadAllText(filePath))["s3-bucket"];
            return token.ToObject<string>();
        }
    }
}