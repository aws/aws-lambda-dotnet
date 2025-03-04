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
        public CloudFormationHelper CloudFormationHelper { get; private set; }
        public ApiGatewayHelper ApiGatewayHelper { get; private set; }
        public ApiGatewayTestHelper ApiGatewayTestHelper { get; private set; }

        public string StackName { get; private set; }

        // Base API Gateways
        public string BaseRestApiId { get; private set; }
        public string BaseHttpApiV1Id { get; private set; }
        public string BaseHttpApiV2Id { get; private set; }
        public string BaseRestApiUrl { get; private set; }
        public string BaseHttpApiV1Url { get; private set; }
        public string BaseHttpApiV2Url { get; private set; }

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

            StackName = string.Empty;

            // Initialize properties
            BaseRestApiId = string.Empty;
            BaseHttpApiV1Id = string.Empty;
            BaseHttpApiV2Id = string.Empty;
            BaseRestApiUrl = string.Empty;
            BaseHttpApiV1Url = string.Empty;
            BaseHttpApiV2Url = string.Empty;

            ParseAndReturnBodyLambdaFunctionArn = string.Empty;
            ReturnRawBodyLambdaFunctionArn = string.Empty;
            ReturnFullEventLambdaFunctionArn = string.Empty;
            ReturnDecodedParseBinLambdaFunctionArn = string.Empty;
        }

        public async Task InitializeAsync()
        {
            StackName = $"Test-{Guid.NewGuid().ToString("N").Substring(0, 5)}";

            string templateBody = ReadCloudFormationTemplate("cloudformation-template-apigateway.yaml");
            await CloudFormationHelper.CreateStackAsync(StackName, templateBody);

            await WaitForStackCreationComplete();
            await RetrieveStackOutputs();
            await WaitForApisAvailability();
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
            // Base APIs
            BaseRestApiId = await CloudFormationHelper.GetOutputValueAsync(StackName, "BaseRestApiId");
            BaseHttpApiV1Id = await CloudFormationHelper.GetOutputValueAsync(StackName, "BaseHttpApiV1Id");
            BaseHttpApiV2Id = await CloudFormationHelper.GetOutputValueAsync(StackName, "BaseHttpApiV2Id");
            BaseRestApiUrl = await CloudFormationHelper.GetOutputValueAsync(StackName, "BaseRestApiUrl");
            BaseHttpApiV1Url = await CloudFormationHelper.GetOutputValueAsync(StackName, "BaseHttpApiV1Url");
            BaseHttpApiV2Url = await CloudFormationHelper.GetOutputValueAsync(StackName, "BaseHttpApiV2Url");

            // Lambda Function ARNs
            ParseAndReturnBodyLambdaFunctionArn = await CloudFormationHelper.GetOutputValueAsync(StackName, "ParseAndReturnBodyLambdaFunctionArn");
            ReturnRawBodyLambdaFunctionArn = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnRawBodyLambdaFunctionArn");
            ReturnFullEventLambdaFunctionArn = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnFullEventLambdaFunctionArn");
            ReturnDecodedParseBinLambdaFunctionArn = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnDecodedParseBinLambdaFunctionArn");
        }

        private async Task WaitForApisAvailability()
        {
            await ApiGatewayHelper.WaitForApiAvailability(BaseRestApiId, BaseRestApiUrl, false);
            await ApiGatewayHelper.WaitForApiAvailability(BaseHttpApiV1Id, BaseHttpApiV1Url, true);
            await ApiGatewayHelper.WaitForApiAvailability(BaseHttpApiV2Id, BaseHttpApiV2Url, true);
        }

        public async Task DisposeAsync()
        {
            await CloudFormationHelper.DeleteStackAsync(StackName);
        }
    }
}
