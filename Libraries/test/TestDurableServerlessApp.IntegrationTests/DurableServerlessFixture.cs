// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.Lambda;
using Amazon.S3;
using IntegrationTests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestDurableServerlessApp.IntegrationTests
{
    /// <summary>
    /// Deploys the durable serverless app through <c>dotnet lambda deploy-serverless</c> ->
    /// CloudFormation (via DeploymentScript.ps1), then exposes the deployed durable function so tests can
    /// invoke it in durable mode. This is the coverage the durable unit/CreateFunction-based integ tests
    /// lack: it actually pushes the source-generator-produced serverless.template (DurableConfig block)
    /// through CloudFormation, which would have caught an invalid/empty DurableConfig at deploy time.
    /// Durable execution requires the managed dotnet10 Zip runtime and region us-east-1.
    /// </summary>
    public class DurableServerlessFixture : IAsyncLifetime
    {
        private static readonly Amazon.RegionEndpoint Region = Amazon.RegionEndpoint.USEast1;

        private readonly CloudFormationHelper _cloudFormationHelper;
        private readonly S3Helper _s3Helper;
        private readonly LambdaHelper _lambdaHelper;

        private string? _stackName;
        private string? _bucketName;

        public string DurableFunctionName = string.Empty;
        public readonly IAmazonLambda LambdaClient;

        public DurableServerlessFixture()
        {
            var cloudFormationClient = new AmazonCloudFormationClient(Region);
            _cloudFormationHelper = new CloudFormationHelper(cloudFormationClient);
            _s3Helper = new S3Helper(new AmazonS3Client(Region));
            LambdaClient = new AmazonLambdaClient(Region);
            _lambdaHelper = new LambdaHelper(LambdaClient, cloudFormationClient);
        }

        public async Task InitializeAsync()
        {
            var scriptPath = Path.Combine("..", "..", "..", "DeploymentScript.ps1");
            Console.WriteLine($"[DurableIntegrationTest] Running deployment script: {scriptPath}");
            await CommandLineWrapper.RunAsync($"pwsh {scriptPath}");
            Console.WriteLine("[DurableIntegrationTest] Deployment script completed.");

            _stackName = GetToolsDefault("stack-name");
            _bucketName = GetToolsDefault("s3-bucket");
            Assert.False(string.IsNullOrEmpty(_stackName), "Stack name should not be empty");
            Assert.False(string.IsNullOrEmpty(_bucketName), "Bucket name should not be empty");

            var stackStatus = await _cloudFormationHelper.GetStackStatusAsync(_stackName);
            Console.WriteLine($"[DurableIntegrationTest] Stack status: {stackStatus}");
            Assert.Equal(StackStatus.CREATE_COMPLETE, stackStatus);

            var functions = await _lambdaHelper.FilterByCloudFormationStackAsync(_stackName);
            Console.WriteLine($"[DurableIntegrationTest] Found {functions.Count} Lambda function(s): {string.Join(", ", functions.Select(f => f.Name ?? "(null)"))}");
            Assert.Single(functions);
            DurableFunctionName = functions[0].Name ?? string.Empty;
            Assert.False(string.IsNullOrEmpty(DurableFunctionName), "Durable function name should not be empty");

            await _lambdaHelper.WaitTillNotPending(functions.Where(x => x.Name != null).Select(x => x.Name!).ToList());

            // Give the durable configuration a moment to settle after the function goes Active.
            await Task.Delay(10000);
        }

        public async Task DisposeAsync()
        {
            if (!string.IsNullOrEmpty(_stackName))
            {
                Console.WriteLine($"[DurableIntegrationTest] Cleaning up stack '{_stackName}'...");
                await _cloudFormationHelper.DeleteStackAsync(_stackName);
                Assert.True(await _cloudFormationHelper.IsDeletedAsync(_stackName), $"The stack '{_stackName}' still exists and will have to be manually deleted.");
            }

            if (!string.IsNullOrEmpty(_bucketName))
            {
                Console.WriteLine($"[DurableIntegrationTest] Cleaning up bucket '{_bucketName}'...");
                await _s3Helper.DeleteBucketAsync(_bucketName);
            }

            // Reset the tools-defaults bucket/stack names so the next run starts from a clean file.
            var filePath = ToolsDefaultPath();
            var token = JObject.Parse(await File.ReadAllTextAsync(filePath));
            token["s3-bucket"] = "test-durable-serverless-app";
            token["stack-name"] = "test-durable-serverless-app";
            await File.WriteAllTextAsync(filePath, token.ToString(Formatting.Indented));
        }

        private static string ToolsDefaultPath() =>
            Path.Combine("..", "..", "..", "..", "TestDurableServerlessApp", "aws-lambda-tools-defaults.json");

        private static string GetToolsDefault(string key) =>
            JObject.Parse(File.ReadAllText(ToolsDefaultPath()))[key]?.ToObject<string>() ?? string.Empty;
    }
}
