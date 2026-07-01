// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
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
        public string LoadBalancerArn;

        public ALBIntegrationTestContextFixture()
        {
            var cloudFormationClient = new AmazonCloudFormationClient(Amazon.RegionEndpoint.USWest2);
            _cloudFormationHelper = new CloudFormationHelper(cloudFormationClient);
            _s3Helper = new S3Helper(new AmazonS3Client(Amazon.RegionEndpoint.USWest2));
            LambdaHelper = new LambdaHelper(new AmazonLambdaClient(Amazon.RegionEndpoint.USWest2), cloudFormationClient);
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

            // Resolve the LoadBalancerArn from DNS name for scoped queries
            var lbResponse = await ELBv2Client.DescribeLoadBalancersAsync(new DescribeLoadBalancersRequest());
            var loadBalancer = lbResponse.LoadBalancers.FirstOrDefault(lb => lb.DNSName == ALBDnsName);
            if (loadBalancer != null)
            {
                LoadBalancerArn = loadBalancer.LoadBalancerArn;
                Console.WriteLine($"[ALB IntegrationTest] LoadBalancer ARN: {LoadBalancerArn}");
            }

            // Wait for Lambda targets to become healthy by polling target health
            Console.WriteLine("[ALB IntegrationTest] Waiting for targets to become healthy...");
            await WaitForTargetsHealthy(timeoutSeconds: 120, pollIntervalSeconds: 10);
        }

        /// <summary>
        /// Polls ALB target group health until at least one target is healthy or the timeout is reached.
        /// </summary>
        private async Task WaitForTargetsHealthy(int timeoutSeconds, int pollIntervalSeconds)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    if (!string.IsNullOrEmpty(LoadBalancerArn))
                    {
                        var tgResponse = await ELBv2Client.DescribeTargetGroupsAsync(new DescribeTargetGroupsRequest
                        {
                            LoadBalancerArn = LoadBalancerArn
                        });

                        var lambdaTgs = tgResponse.TargetGroups.Where(tg => tg.TargetType == TargetTypeEnum.Lambda).ToList();
                        if (lambdaTgs.Count >= 2)
                        {
                            var allHealthy = true;
                            foreach (var tg in lambdaTgs)
                            {
                                var healthResponse = await ELBv2Client.DescribeTargetHealthAsync(new DescribeTargetHealthRequest
                                {
                                    TargetGroupArn = tg.TargetGroupArn
                                });
                                if (!healthResponse.TargetHealthDescriptions.Any(t => t.TargetHealth.State == TargetHealthStateEnum.Healthy))
                                {
                                    allHealthy = false;
                                    break;
                                }
                            }

                            if (allHealthy)
                            {
                                Console.WriteLine("[ALB IntegrationTest] All targets are healthy.");
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ALB IntegrationTest] Polling error (will retry): {ex.Message}");
                }

                Console.WriteLine($"[ALB IntegrationTest] Targets not yet healthy, retrying in {pollIntervalSeconds}s...");
                await Task.Delay(pollIntervalSeconds * 1000);
            }

            Console.WriteLine("[ALB IntegrationTest] Warning: Timed out waiting for targets to become healthy. Proceeding anyway.");
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

            // Reset aws-lambda-tools-defaults.json to original values
            var filePath = Path.Combine("..", "..", "..", "..", "TestServerlessApp.ALB", "aws-lambda-tools-defaults.json");
            var token = JObject.Parse(await File.ReadAllTextAsync(filePath));
            token["s3-bucket"] = "test-serverless-app-alb";
            token["stack-name"] = "test-serverless-app-alb";
            token["function-architecture"] = "x86_64";
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
