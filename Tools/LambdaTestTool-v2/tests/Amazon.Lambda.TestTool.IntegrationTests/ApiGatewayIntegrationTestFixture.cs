// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.CloudFormation;
using Amazon.APIGateway;
using Amazon.ApiGatewayV2;
using Amazon.Lambda.TestTool.IntegrationTests.Helpers;
using Amazon.Lambda.TestTool.Models;
using System.Reflection;
using Xunit;

namespace Amazon.Lambda.TestTool.IntegrationTests
{
    public class ApiGatewayIntegrationTestFixture : IAsyncLifetime
    {
        private readonly Dictionary<string, TestRouteConfig> _testRoutes;
        
        public CloudFormationHelper CloudFormationHelper { get; private set; }
        public ApiGatewayHelper ApiGatewayHelper { get; private set; }
        public ApiGatewayTestHelper ApiGatewayTestHelper { get; private set; }

        public string StackName { get; private set; }

        // Main API Gateway IDs and Base URLs
        public string MainRestApiId { get; private set; }
        public string MainHttpApiV1Id { get; private set; }
        public string MainHttpApiV2Id { get; private set; }
        public string BinaryMediaTypeRestApiId { get; private set; } // this is the rest api that has binary media types of */* enabled

        public string MainRestApiBaseUrl { get; private set; }
        public string MainHttpApiV1BaseUrl { get; private set; }
        public string MainHttpApiV2BaseUrl { get; private set; }
        public string BinaryMediaTypeRestApiBaseUrl { get; private set; }

        // Lambda Function ARNs
        public string ParseAndReturnBodyLambdaFunctionArn { get; private set; }
        public string ReturnRawBodyLambdaFunctionArn { get; private set; }
        public string ReturnFullEventLambdaFunctionArn { get; private set; }
        public string ReturnDecodedParseBinLambdaFunctionArn { get; private set; }

        public ApiGatewayIntegrationTestFixture()
        {
            var regionEndpoint = RegionEndpoint.USWest2;
            CloudFormationHelper = new CloudFormationHelper(new AmazonCloudFormationClient(regionEndpoint));
            ApiGatewayHelper = new ApiGatewayHelper(
                new AmazonAPIGatewayClient(regionEndpoint),
                new AmazonApiGatewayV2Client(regionEndpoint)
            );
            ApiGatewayTestHelper = new ApiGatewayTestHelper();
            _testRoutes = new Dictionary<string, TestRouteConfig>();

            StackName = string.Empty;
            MainRestApiId = string.Empty;
            MainHttpApiV1Id = string.Empty;
            MainHttpApiV2Id = string.Empty;
            BinaryMediaTypeRestApiId = string.Empty;
            MainRestApiBaseUrl = string.Empty;
            MainHttpApiV1BaseUrl = string.Empty;
            MainHttpApiV2BaseUrl = string.Empty;
            BinaryMediaTypeRestApiBaseUrl = string.Empty;
            ParseAndReturnBodyLambdaFunctionArn = string.Empty;
            ReturnRawBodyLambdaFunctionArn = string.Empty;
            ReturnFullEventLambdaFunctionArn = string.Empty;
            ReturnDecodedParseBinLambdaFunctionArn = string.Empty;
        }

        public void RegisterTestRoute(string routeId, TestRouteConfig config)
        {
            if (string.IsNullOrEmpty(routeId))
            {
                throw new ArgumentNullException(nameof(routeId));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (string.IsNullOrEmpty(config.Path))
            {
                throw new ArgumentException("Route path cannot be empty", nameof(config));
            }

            if (string.IsNullOrEmpty(config.HttpMethod))
            {
                throw new ArgumentException("HTTP method cannot be empty", nameof(config));
            }

            if (string.IsNullOrEmpty(config.LambdaFunctionArn))
            {
                throw new ArgumentException("Lambda function ARN cannot be empty", nameof(config));
            }

            _testRoutes[routeId] = config;
        }

        public string GetRouteUrl(string baseUrl, string routeId)
        {
            if (!_testRoutes.TryGetValue(routeId, out var config))
            {
                throw new KeyNotFoundException($"Route {routeId} not found");
            }
            return baseUrl.TrimEnd('/') + config.Path;
        }

        public string GetAppropriateBaseUrl(ApiGatewayType gatewayType)
        {
            return gatewayType switch
            {
                ApiGatewayType.Rest => MainRestApiBaseUrl,
                ApiGatewayType.RestWithBinarySupport => BinaryMediaTypeRestApiBaseUrl,
                ApiGatewayType.HttpV1 => MainHttpApiV1BaseUrl,
                ApiGatewayType.HttpV2 => MainHttpApiV2BaseUrl,
                _ => throw new ArgumentException($"Unsupported gateway type: {gatewayType}")
            };
        }

        public string GetAppropriateApiId(ApiGatewayType gatewayType)
        {
            return gatewayType switch
            {
                ApiGatewayType.Rest => MainRestApiId,
                ApiGatewayType.RestWithBinarySupport => BinaryMediaTypeRestApiId,
                ApiGatewayType.HttpV1 => MainHttpApiV1Id,
                ApiGatewayType.HttpV2 => MainHttpApiV2Id,
                _ => throw new ArgumentException($"Unsupported gateway type: {gatewayType}")
            };
        }

        public static ApiGatewayEmulatorMode GetEmulatorMode(ApiGatewayType gatewayType)
        {
            return gatewayType switch
            {
                ApiGatewayType.Rest or ApiGatewayType.RestWithBinarySupport => ApiGatewayEmulatorMode.Rest,
                ApiGatewayType.HttpV1 => ApiGatewayEmulatorMode.HttpV1,
                ApiGatewayType.HttpV2 => ApiGatewayEmulatorMode.HttpV2,
                _ => throw new ArgumentException($"Unsupported gateway type: {gatewayType}")
            };
        }
        

        public async Task InitializeAsync()
        {
            StackName = $"Test-{Guid.NewGuid().ToString("N").Substring(0, 5)}";

            string templateBody = ReadCloudFormationTemplate("cloudformation-template-apigateway.yaml");
            await CloudFormationHelper.CreateStackAsync(StackName, templateBody);

            await WaitForStackCreationComplete();
            await RetrieveStackOutputs();

            // Register all test routes using RegisterTestRoute
            foreach (var (routeId, config) in TestRoutes.GetDefaultRoutes(this))
            {
                RegisterTestRoute(routeId, config);
            }

            // Setup all routes
            await SetupTestRoutes();
            await WaitForRoutesAvailability();
        }

        private async Task SetupTestRoutes()
        {
            foreach (var (routeId, config) in _testRoutes)
            {
                if (config.UsesBinaryMediaTypes)
                {
                    // Add route only to binary media type REST API
                    await ApiGatewayHelper.AddRouteToRestApi(
                        BinaryMediaTypeRestApiId,
                        config.LambdaFunctionArn,
                        config.Path,
                        config.HttpMethod);
                }
                else
                {
                    // Add route to all main APIs
                    await ApiGatewayHelper.AddRouteToRestApi(
                        MainRestApiId,
                        config.LambdaFunctionArn,
                        config.Path,
                        config.HttpMethod);

                    await ApiGatewayHelper.AddRouteToHttpApi(
                        MainHttpApiV1Id,
                        config.LambdaFunctionArn,
                        "1.0",
                        config.Path,
                        config.HttpMethod);

                    await ApiGatewayHelper.AddRouteToHttpApi(
                        MainHttpApiV2Id,
                        config.LambdaFunctionArn,
                        "2.0",
                        config.Path,
                        config.HttpMethod);
                }
            }
        }

        private async Task WaitForRoutesAvailability()
        {
            foreach (var config in _testRoutes.Values)
            {
                if (config.UsesBinaryMediaTypes)
                {
                    var binaryUrl = BinaryMediaTypeRestApiBaseUrl.TrimEnd('/') + config.Path;
                    await ApiGatewayHelper.WaitForApiAvailability(BinaryMediaTypeRestApiId, binaryUrl, false);
                }
                else
                {
                    var restUrl = MainRestApiBaseUrl.TrimEnd('/') + config.Path;
                    var httpV1Url = MainHttpApiV1BaseUrl.TrimEnd('/') + config.Path;
                    var httpV2Url = MainHttpApiV2BaseUrl.TrimEnd('/') + config.Path;

                    await ApiGatewayHelper.WaitForApiAvailability(MainRestApiId, restUrl, false);
                    await ApiGatewayHelper.WaitForApiAvailability(MainHttpApiV1Id, httpV1Url, true);
                    await ApiGatewayHelper.WaitForApiAvailability(MainHttpApiV2Id, httpV2Url, true);
                }
            }
        }

        private string ReadCloudFormationTemplate(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"{assembly.GetName().Name}.{fileName}";
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException($"CloudFormation template file '{fileName}' not found in assembly resources.");
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private async Task WaitForStackCreationComplete()
        {
            while (true)
            {
                var status = await CloudFormationHelper.GetStackStatusAsync(StackName);
                if (status == StackStatus.CREATE_COMPLETE)
                {
                    break;
                }
                if (status.ToString().EndsWith("FAILED") || status == StackStatus.DELETE_COMPLETE)
                {
                    throw new Exception($"Stack creation failed. Status: {status}");
                }
                await Task.Delay(10000);
            }
        }

        private async Task RetrieveStackOutputs()
        {
            MainRestApiId = await CloudFormationHelper.GetOutputValueAsync(StackName, "MainRestApiId");
            MainHttpApiV1Id = await CloudFormationHelper.GetOutputValueAsync(StackName, "MainHttpApiV1Id");
            MainHttpApiV2Id = await CloudFormationHelper.GetOutputValueAsync(StackName, "MainHttpApiV2Id");
            BinaryMediaTypeRestApiId = await CloudFormationHelper.GetOutputValueAsync(StackName, "BinaryMediaTypeRestApiId");

            MainRestApiBaseUrl = await CloudFormationHelper.GetOutputValueAsync(StackName, "MainRestApiBaseUrl");
            MainHttpApiV1BaseUrl = await CloudFormationHelper.GetOutputValueAsync(StackName, "MainHttpApiV1BaseUrl");
            MainHttpApiV2BaseUrl = await CloudFormationHelper.GetOutputValueAsync(StackName, "MainHttpApiV2BaseUrl");
            BinaryMediaTypeRestApiBaseUrl = await CloudFormationHelper.GetOutputValueAsync(StackName, "BinaryMediaTypeRestApiBaseUrl");

            ParseAndReturnBodyLambdaFunctionArn = await CloudFormationHelper.GetOutputValueAsync(StackName, "ParseAndReturnBodyLambdaFunctionArn");
            ReturnRawBodyLambdaFunctionArn = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnRawBodyLambdaFunctionArn");
            ReturnFullEventLambdaFunctionArn = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnFullEventLambdaFunctionArn");
            ReturnDecodedParseBinLambdaFunctionArn = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnDecodedParseBinLambdaFunctionArn");
        }

        public async Task DisposeAsync()
        {
            await CloudFormationHelper.DeleteStackAsync(StackName);
        }
    }

    public enum ApiGatewayType
    {
        Rest,
        RestWithBinarySupport,
        HttpV1,
        HttpV2
    }

    public class ApiGatewayTestConfig
    {
        public required string RouteId { get; init; }
        public required ApiGatewayType GatewayType { get; init; }
    }
}
