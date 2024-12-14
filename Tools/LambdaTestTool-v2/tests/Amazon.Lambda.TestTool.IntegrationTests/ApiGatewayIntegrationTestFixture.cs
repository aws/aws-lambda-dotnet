// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.CloudFormation;
using Amazon.APIGateway;
using Amazon.ApiGatewayV2;
using Amazon.Lambda.TestTool.IntegrationTests.Helpers;
using System.Reflection;

namespace Amazon.Lambda.TestTool.IntegrationTests
{
    public class ApiGatewayIntegrationTestFixture : IAsyncLifetime
    {
        public CloudFormationHelper CloudFormationHelper { get; private set; }
        public ApiGatewayHelper ApiGatewayHelper { get; private set; }
        public ApiGatewayTestHelper ApiGatewayTestHelper { get; private set; }

        public string StackName { get; private set; }
        public string RestApiId { get; private set; }
        public string HttpApiV1Id { get; private set; }
        public string HttpApiV2Id { get; private set; }
        public string ReturnRawRequestBodyV2Id { get; private set; }
        public string RestApiUrl { get; private set; }
        public string HttpApiV1Url { get; private set; }
        public string HttpApiV2Url { get; private set; }
        public string ReturnRawRequestBodyHttpApiV2Url { get; private set; }

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
            RestApiId = string.Empty;
            HttpApiV1Id = string.Empty;
            HttpApiV2Id = string.Empty;
            ReturnRawRequestBodyV2Id = string.Empty;
            RestApiUrl = string.Empty;
            HttpApiV1Url = string.Empty;
            HttpApiV2Url = string.Empty;
            ReturnRawRequestBodyHttpApiV2Url = string.Empty;
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
            RestApiId = await CloudFormationHelper.GetOutputValueAsync(StackName, "RestApiId");
            RestApiUrl = await CloudFormationHelper.GetOutputValueAsync(StackName, "RestApiUrl");

            HttpApiV1Id = await CloudFormationHelper.GetOutputValueAsync(StackName, "HttpApiV1Id");
            HttpApiV1Url = await CloudFormationHelper.GetOutputValueAsync(StackName, "HttpApiV1Url");

            HttpApiV2Id = await CloudFormationHelper.GetOutputValueAsync(StackName, "HttpApiV2Id");
            HttpApiV2Url = await CloudFormationHelper.GetOutputValueAsync(StackName, "HttpApiV2Url");

            ReturnRawRequestBodyV2Id = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnRawRequestBodyHttpApiId");
            ReturnRawRequestBodyHttpApiV2Url = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnRawRequestBodyHttpApiUrl");
        }

        private async Task WaitForApisAvailability()
        {
            await ApiGatewayHelper.WaitForApiAvailability(RestApiId, RestApiUrl, false);
            await ApiGatewayHelper.WaitForApiAvailability(HttpApiV1Id, HttpApiV1Url, true);
            await ApiGatewayHelper.WaitForApiAvailability(HttpApiV2Id, HttpApiV2Url, true);
            await ApiGatewayHelper.WaitForApiAvailability(ReturnRawRequestBodyV2Id, ReturnRawRequestBodyHttpApiV2Url, true);
        }

        public async Task DisposeAsync()
        {
            await CloudFormationHelper.DeleteStackAsync(StackName);
        }
    }
}
