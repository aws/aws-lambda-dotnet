// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.CloudFormation;
using Amazon.APIGateway;
using Amazon.ApiGatewayV2;
using Amazon.Lambda.TestTool.IntegrationTests.Helpers;
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

        // Main API Gateway IDs
        public string MainRestApiId { get; private set; }
        public string MainHttpApiV1Id { get; private set; }
        public string MainHttpApiV2Id { get; private set; }

        // Base URLs
        public string MainRestApiBaseUrl { get; private set; }
        public string MainHttpApiV1BaseUrl { get; private set; }
        public string MainHttpApiV2BaseUrl { get; private set; }

        // Lambda Function ARNs
        public string ParseAndReturnBodyLambdaFunctionArn { get; private set; }
        public string ReturnRawBodyLambdaFunctionArn { get; private set; }
        public string ReturnFullEventLambdaFunctionArn { get; private set; }

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
            MainRestApiBaseUrl = string.Empty;
            MainHttpApiV1BaseUrl = string.Empty;
            MainHttpApiV2BaseUrl = string.Empty;
            ParseAndReturnBodyLambdaFunctionArn = string.Empty;
            ReturnRawBodyLambdaFunctionArn = string.Empty;
            ReturnFullEventLambdaFunctionArn = string.Empty;
        }

        public void RegisterTestRoute(string routeId, TestRouteConfig config)
        {
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

        public async Task InitializeAsync()
        {
            StackName = $"Test-{Guid.NewGuid().ToString("N").Substring(0, 5)}";

            string templateBody = ReadCloudFormationTemplate("cloudformation-template-apigateway.yaml");
            await CloudFormationHelper.CreateStackAsync(StackName, templateBody);

            await WaitForStackCreationComplete();
            await RetrieveStackOutputs();

            // Register all test routes
            foreach (var route in TestRoutes.GetDefaultRoutes(this))
            {
                RegisterTestRoute(route.Key, route.Value);
            }

            // Setup all routes
            await SetupTestRoutes();
            await WaitForRoutesAvailability();
        }

        private async Task SetupTestRoutes()
        {
            foreach (var (routeId, config) in _testRoutes)
            {
                // Add route to REST API
                await ApiGatewayHelper.AddRouteToRestApi(
                    MainRestApiId,
                    config.LambdaFunctionArn,
                    config.Path,
                    config.HttpMethod);

                // Add route to HTTP API v1
                await ApiGatewayHelper.AddRouteToHttpApi(
                    MainHttpApiV1Id,
                    config.LambdaFunctionArn,
                    "1.0",
                    config.Path,
                    config.HttpMethod);

                // Add route to HTTP API v2
                await ApiGatewayHelper.AddRouteToHttpApi(
                    MainHttpApiV2Id,
                    config.LambdaFunctionArn,
                    "2.0",
                    config.Path,
                    config.HttpMethod);
            }
        }

        private async Task WaitForRoutesAvailability()
        {
            foreach (var config in _testRoutes.Values)
            {
                var restUrl = MainRestApiBaseUrl.TrimEnd('/') + config.Path;
                var httpV1Url = MainHttpApiV1BaseUrl.TrimEnd('/') + config.Path;
                var httpV2Url = MainHttpApiV2BaseUrl.TrimEnd('/') + config.Path;

                await ApiGatewayHelper.WaitForApiAvailability(MainRestApiId, restUrl, false);
                await ApiGatewayHelper.WaitForApiAvailability(MainHttpApiV1Id, httpV1Url, true);
                await ApiGatewayHelper.WaitForApiAvailability(MainHttpApiV2Id, httpV2Url, true);
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
            
            MainRestApiBaseUrl = await CloudFormationHelper.GetOutputValueAsync(StackName, "MainRestApiBaseUrl");
            MainHttpApiV1BaseUrl = await CloudFormationHelper.GetOutputValueAsync(StackName, "MainHttpApiV1BaseUrl");
            MainHttpApiV2BaseUrl = await CloudFormationHelper.GetOutputValueAsync(StackName, "MainHttpApiV2BaseUrl");

            ParseAndReturnBodyLambdaFunctionArn = await CloudFormationHelper.GetOutputValueAsync(StackName, "ParseAndReturnBodyLambdaFunctionArn");
            ReturnRawBodyLambdaFunctionArn = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnRawBodyLambdaFunctionArn");
            ReturnFullEventLambdaFunctionArn = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnFullEventLambdaFunctionArn");
        }

        public async Task DisposeAsync()
        {
            await CloudFormationHelper.DeleteStackAsync(StackName);
        }
    }
}
