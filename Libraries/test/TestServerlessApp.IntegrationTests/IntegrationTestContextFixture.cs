using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudWatchLogs;
using Amazon.Lambda;
using Amazon.S3;
using IntegrationTests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        public string TestQueueARN;
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
            Console.WriteLine($"[IntegrationTest] Running deployment script: {scriptPath}");
            await CommandLineWrapper.RunAsync($"pwsh {scriptPath}");
            Console.WriteLine("[IntegrationTest] Deployment script completed successfully.");

            _stackName = GetStackName();
            _bucketName = GetBucketName();
            Console.WriteLine($"[IntegrationTest] Stack name: '{_stackName}', Bucket name: '{_bucketName}'");
            Assert.False(string.IsNullOrEmpty(_stackName), "Stack name should not be empty");
            Assert.False(string.IsNullOrEmpty(_bucketName), "Bucket name should not be empty");

            // Check stack status before querying resources
            var stackStatus = await _cloudFormationHelper.GetStackStatusAsync(_stackName);
            Console.WriteLine($"[IntegrationTest] Stack status after deployment: {stackStatus}");
            Assert.NotNull(stackStatus);
            Assert.Equal(StackStatus.CREATE_COMPLETE, stackStatus);

            // Dynamically construct API URLs from stack resource physical IDs
            // since the serverless.template is managed by the source generator and may not have Outputs
            var region = "us-west-2";
            Console.WriteLine($"[IntegrationTest] Querying stack resources for '{_stackName}'...");
            var httpApiId = await _cloudFormationHelper.GetResourcePhysicalIdAsync(_stackName, "AnnotationsHttpApi");
            var restApiId = await _cloudFormationHelper.GetResourcePhysicalIdAsync(_stackName, "AnnotationsRestApi");
            Console.WriteLine($"[IntegrationTest] AnnotationsHttpApi: {httpApiId}, AnnotationsRestApi: {restApiId}");
            Assert.False(string.IsNullOrEmpty(httpApiId), $"CloudFormation resource 'AnnotationsHttpApi' was not found or has an empty physical ID for stack '{_stackName}'.");
            Assert.False(string.IsNullOrEmpty(restApiId), $"CloudFormation resource 'AnnotationsRestApi' was not found or has an empty physical ID for stack '{_stackName}'.");
            HttpApiUrlPrefix = $"https://{httpApiId}.execute-api.{region}.amazonaws.com";
            RestApiUrlPrefix = $"https://{restApiId}.execute-api.{region}.amazonaws.com/Prod";

            // Get the SQS queue ARN from the physical resource ID (which is the queue URL)
            var queueUrl = await _cloudFormationHelper.GetResourcePhysicalIdAsync(_stackName, "TestQueue");
            Console.WriteLine($"[IntegrationTest] TestQueue URL: {queueUrl}");
            Assert.False(string.IsNullOrEmpty(queueUrl), $"CloudFormation resource 'TestQueue' was not found in stack '{_stackName}'.");
            TestQueueARN = ConvertSqsUrlToArn(queueUrl);
            LambdaFunctions = await LambdaHelper.FilterByCloudFormationStackAsync(_stackName);
            Console.WriteLine($"[IntegrationTest] Found {LambdaFunctions.Count} Lambda functions: {string.Join(", ", LambdaFunctions.Select(f => f.Name ?? "(null)"))}");

            Assert.True(await _s3Helper.BucketExistsAsync(_bucketName), $"S3 bucket {_bucketName} should exist");
            Assert.Equal(36, LambdaFunctions.Count);
            Assert.False(string.IsNullOrEmpty(RestApiUrlPrefix), "RestApiUrlPrefix should not be empty");
            Assert.False(string.IsNullOrEmpty(HttpApiUrlPrefix), "HttpApiUrlPrefix should not be empty");

            await LambdaHelper.WaitTillNotPending(LambdaFunctions.Where(x => x.Name != null).Select(x => x.Name).ToList());

            // Wait an additional 10 seconds for any other eventually consistency state to finish up.
            await Task.Delay(10000);
        }

        public async Task DisposeAsync()
        {
            if (!string.IsNullOrEmpty(_stackName))
            {
                Console.WriteLine($"[IntegrationTest] Cleaning up stack '{_stackName}'...");
                await _cloudFormationHelper.DeleteStackAsync(_stackName);
                Assert.True(await _cloudFormationHelper.IsDeletedAsync(_stackName), $"The stack '{_stackName}' still exists and will have to be manually deleted from the AWS console.");
            }
            else
            {
                Console.WriteLine("[IntegrationTest] WARNING: Stack name is null/empty, skipping stack deletion.");
            }

            if (!string.IsNullOrEmpty(_bucketName))
            {
                Console.WriteLine($"[IntegrationTest] Cleaning up bucket '{_bucketName}'...");
                await _s3Helper.DeleteBucketAsync(_bucketName);
                Assert.False(await _s3Helper.BucketExistsAsync(_bucketName), $"The bucket '{_bucketName}' still exists and will have to be manually deleted from the AWS console.");
            }
            else
            {
                Console.WriteLine("[IntegrationTest] WARNING: Bucket name is null/empty, skipping bucket deletion.");
            }

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

        /// <summary>
        /// Converts an SQS queue URL (e.g. https://sqs.us-west-2.amazonaws.com/123456789012/queue-name)
        /// to an ARN (e.g. arn:aws:sqs:us-west-2:123456789012:queue-name).
        /// </summary>
        private static string ConvertSqsUrlToArn(string queueUrl)
        {
            if (string.IsNullOrEmpty(queueUrl))
            {
                throw new ArgumentException("Queue URL cannot be null or empty. Ensure the CloudFormation resource exists and has a valid physical ID.", nameof(queueUrl));
            }

            // SQS URL format: https://sqs.{region}.amazonaws.com/{account-id}/{queue-name}
            var uri = new Uri(queueUrl);
            var host = uri.Host; // sqs.us-west-2.amazonaws.com
            var segments = uri.AbsolutePath.Trim('/').Split('/'); // [account-id, queue-name]
            var region = host.Split('.')[1]; // us-west-2
            var accountId = segments[0];
            var queueName = segments[1];
            return $"arn:aws:sqs:{region}:{accountId}:{queueName}";
        }
    }
}
