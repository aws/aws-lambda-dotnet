// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Lambda.RuntimeSupport.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests
{
    /// <summary>
    /// Integration tests for ASP.NET Core response streaming through API Gateway.
    /// Deploys an ASP.NET Core app using "dotnet lambda deploy-serverless" and
    /// validates streaming responses through the API Gateway endpoint.
    /// </summary>
    public class ApiGatewayStreamingTests : IClassFixture<ApiGatewayStreamingFixture>
    {
        private readonly ApiGatewayStreamingFixture _fixture;
        private readonly ITestOutputHelper _output;

        public ApiGatewayStreamingTests(ApiGatewayStreamingFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task RootEndpoint_ReturnsWelcomeMessage()
        {
            var apiUrl = await _fixture.GetApiUrlAsync();
            using var httpClient = new HttpClient();

            var response = await httpClient.GetWithRetryAsync(apiUrl);

            _output.WriteLine($"Status: {response.StatusCode}");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Body: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Welcome to ASP.NET Core streaming on Lambda", body);
        }

        [Fact]
        public async Task StreamingEndpoint_ReturnsAllLines()
        {
            var apiUrl = await _fixture.GetApiUrlAsync();
            using var httpClient = new HttpClient();

            var response = await httpClient.GetWithRetryAsync($"{apiUrl}streaming-test");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Body length: {body.Length}");

            // Verify all 100 lines are present
            Assert.Contains("Line 1", body);
            Assert.Contains("Line 50", body);
            Assert.Contains("Line 100", body);
        }

        [Fact]
        public async Task StreamingEndpoint_ContentTypeIsTextPlain()
        {
            var apiUrl = await _fixture.GetApiUrlAsync();
            using var httpClient = new HttpClient();

            var response = await httpClient.GetWithRetryAsync($"{apiUrl}streaming-test");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task JsonEndpoint_ReturnsValidJson()
        {
            var apiUrl = await _fixture.GetApiUrlAsync();
            using var httpClient = new HttpClient();

            var response = await httpClient.GetWithRetryAsync($"{apiUrl}json-response");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Body: {body}");

            var doc = JsonDocument.Parse(body);
            Assert.True(doc.RootElement.TryGetProperty("message", out var msg));
            Assert.Equal("Hello from streaming Lambda", msg.GetString());
        }

        [Fact]
        public async Task StreamingErrorEndpoint_StreamIsTruncated()
        {
            var apiUrl = await _fixture.GetApiUrlAsync();
            using var httpClient = new HttpClient();

            // When a midstream error occurs through API Gateway, the stream is truncated.
            // The client may receive a partial response or an error depending on timing.
            try
            {
                var response = await httpClient.GetWithRetryAsync($"{apiUrl}streaming-error");
                var body = await response.Content.ReadAsStringAsync();
                _output.WriteLine($"Status: {response.StatusCode}");
                _output.WriteLine($"Body: {body}");

                // If we got a response, verify we got at least some of the streamed data
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Assert.Contains("Line 1", body);
                }
            }
            catch (HttpRequestException ex)
            {
                // Expected — the stream may be truncated causing an HTTP protocol error
                _output.WriteLine($"Expected error: {ex.Message}");
            }
        }
    }

    internal static class HttpClientExtension
    {
        public static async Task<HttpResponseMessage> GetWithRetryAsync(this HttpClient httpClient, string url, HttpStatusCode expectedCode = HttpStatusCode.OK, int maxRetries = 5, int delaySeconds = 5)
        {
            for (var i = 0; i < maxRetries; i++)
            {
                try
                {
                    var response = await httpClient.GetAsync(url);
                    if (response.StatusCode == expectedCode)
                    {
                        return response;
                    }
                }
                catch
                {
                    // Ignore and retry
                }
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
            throw new Exception($"Failed to get expected status code {expectedCode} from {url} after {maxRetries} attempts");
        }
    }

    /// <summary>
    /// Fixture that deploys the ASP.NET Core streaming test app to AWS using
    /// "dotnet lambda deploy-serverless" and tears it down after tests complete.
    /// </summary>
    public class ApiGatewayStreamingFixture : IAsyncLifetime
    {
        private static readonly RegionEndpoint TestRegion = BaseCustomRuntimeTest.TestRegion;
        private static readonly string StackName = "IntegTest-AspNetCoreStreaming-" + DateTime.UtcNow.Ticks;

        private string _apiUrl;
        private string _toolPath;
        private string _testAppPath;
        private bool _deployed;
        private string _s3BucketName;

        public async Task<string> GetApiUrlAsync()
        {
            if (!_deployed)
            {
                throw new System.InvalidOperationException("Test infrastructure not deployed. InitializeAsync must complete first.");
            }
            return _apiUrl;
        }

        public async Task InitializeAsync()
        {
            _toolPath = await LambdaToolsHelper.InstallLambdaTools();

            _testAppPath = LambdaToolsHelper.GetTempTestAppDirectory(
                "../../../../../../..",
                "Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/AspNetCoreStreamingApiGatewayTest");

            // Deploy using dotnet lambda deploy-serverless
            var lambdaToolPath = Path.Combine(_toolPath, "dotnet-lambda");
            _s3BucketName = await GetOrCreateDeploymentBucketAsync();
            await CommandLineWrapper.Run(
                lambdaToolPath,
                $"deploy-serverless --stack-name {StackName} --s3-bucket {_s3BucketName} --region {TestRegion.SystemName} --disable-interactive true",
                _testAppPath);

            // Get the API URL from CloudFormation outputs
            _apiUrl = await GetStackOutputAsync(StackName, "ApiURL");
            if (!_apiUrl.EndsWith("/"))
            {
                _apiUrl += "/";
            }

            _deployed = true;

            // Wait for API Gateway to be fully ready
            await WaitForApiGatewayAsync();
        }

        public async Task DisposeAsync()
        {
            if (_deployed)
            {
                try
                {
                    var lambdaToolPath = Path.Combine(_toolPath, "dotnet-lambda");
                    await CommandLineWrapper.Run(
                        lambdaToolPath,
                        $"delete-serverless --stack-name {StackName} --region {TestRegion.SystemName}",
                        _testAppPath);

                    if (_s3BucketName != null)
                    {
                        // Clean up the S3 bucket used for deployment artifacts
                        using var s3Client = new Amazon.S3.AmazonS3Client(TestRegion);
                        await Amazon.S3.Util.AmazonS3Util.DeleteS3BucketWithObjectsAsync(s3Client, _s3BucketName);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to delete stack {StackName}: {ex.Message}");
                }
            }

#if !DEBUG
            LambdaToolsHelper.CleanUp(_toolPath);
            LambdaToolsHelper.CleanUp(_testAppPath);
#endif
        }

        private async Task<string> GetStackOutputAsync(string stackName, string outputKey)
        {
            using var cfnClient = new AmazonCloudFormationClient(TestRegion);
            var response = await cfnClient.DescribeStacksAsync(new DescribeStacksRequest
            {
                StackName = stackName
            });

            var stack = response.Stacks.FirstOrDefault()
                ?? throw new Exception($"Stack {stackName} not found");

            var output = stack.Outputs.FirstOrDefault(o => o.OutputKey == outputKey)
                ?? throw new Exception($"Output {outputKey} not found in stack {stackName}");

            return output.OutputValue;
        }

        private async Task<string> GetOrCreateDeploymentBucketAsync()
        {
            using var stsClient = new Amazon.SecurityToken.AmazonSecurityTokenServiceClient(TestRegion);
            var identity = await stsClient.GetCallerIdentityAsync(new Amazon.SecurityToken.Model.GetCallerIdentityRequest());
            var name = $"integ-test-streaming-{identity.Account}-{TestRegion.SystemName}";
            using var s3Client = new Amazon.S3.AmazonS3Client(TestRegion);
            await s3Client.PutBucketAsync(new Amazon.S3.Model.PutBucketRequest
            {
                BucketName = name,
                UseClientRegion = true
            });


            return name;
        }

        private async Task WaitForApiGatewayAsync()
        {
            using var httpClient = new HttpClient();
            var maxRetries = 10;
            for (var i = 0; i < maxRetries; i++)
            {
                try
                {
                    var response = await httpClient.GetAsync(_apiUrl);
                    if (response.StatusCode != HttpStatusCode.InternalServerError)
                    {
                        return;
                    }
                }
                catch
                {
                    // Ignore — API Gateway may not be ready yet
                }
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}
