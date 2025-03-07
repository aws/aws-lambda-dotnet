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

        // ParseAndReturnBody
        public string ParseAndReturnBodyRestApiId { get; private set; }
        public string ParseAndReturnBodyHttpApiV1Id { get; private set; }
        public string ParseAndReturnBodyHttpApiV2Id { get; private set; }
        public string ParseAndReturnBodyRestApiUrl { get; private set; }
        public string ParseAndReturnBodyHttpApiV1Url { get; private set; }
        public string ParseAndReturnBodyHttpApiV2Url { get; private set; }

        // ReturnRawBody
        public string ReturnRawBodyRestApiId { get; private set; }
        public string ReturnRawBodyHttpApiV1Id { get; private set; }
        public string ReturnRawBodyHttpApiV2Id { get; private set; }
        public string ReturnRawBodyRestApiUrl { get; private set; }
        public string ReturnRawBodyHttpApiV1Url { get; private set; }
        public string ReturnRawBodyHttpApiV2Url { get; private set; }

        // ReturnFullEvent
        public string ReturnFullEventRestApiId { get; private set; }
        public string ReturnFullEventHttpApiV1Id { get; private set; }
        public string ReturnFullEventHttpApiV2Id { get; private set; }
        public string ReturnFullEventRestApiUrl { get; private set; }
        public string ReturnFullEventHttpApiV1Url { get; private set; }
        public string ReturnFullEventHttpApiV2Url { get; private set; }

        // ReturnDecodedParseBin
        public string BinaryMediaTypeRestApiId { get; private set; }
        public string BinaryMediaTypeRestApiUrl { get; private set; }

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

            StackName = string.Empty;

            // ParseAndReturnBody
            ParseAndReturnBodyRestApiId = string.Empty;
            ParseAndReturnBodyHttpApiV1Id = string.Empty;
            ParseAndReturnBodyHttpApiV2Id = string.Empty;
            ParseAndReturnBodyRestApiUrl = string.Empty;
            ParseAndReturnBodyHttpApiV1Url = string.Empty;
            ParseAndReturnBodyHttpApiV2Url = string.Empty;

            // ReturnRawBody
            ReturnRawBodyRestApiId = string.Empty;
            ReturnRawBodyHttpApiV1Id = string.Empty;
            ReturnRawBodyHttpApiV2Id = string.Empty;
            ReturnRawBodyRestApiUrl = string.Empty;
            ReturnRawBodyHttpApiV1Url = string.Empty;
            ReturnRawBodyHttpApiV2Url = string.Empty;

            // ReturnFullEvent
            ReturnFullEventRestApiId = string.Empty;
            ReturnFullEventHttpApiV1Id = string.Empty;
            ReturnFullEventHttpApiV2Id = string.Empty;
            ReturnFullEventRestApiUrl = string.Empty;
            ReturnFullEventHttpApiV1Url = string.Empty;
            ReturnFullEventHttpApiV2Url = string.Empty;

            // BinaryMediaTypeRestApiId
            BinaryMediaTypeRestApiId = string.Empty;
            BinaryMediaTypeRestApiUrl = string.Empty;

            // Lambda Function ARNs
            ParseAndReturnBodyLambdaFunctionArn = string.Empty;
            ReturnRawBodyLambdaFunctionArn = string.Empty;
            ReturnFullEventLambdaFunctionArn = string.Empty;
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
            // ParseAndReturnBody
            ParseAndReturnBodyRestApiId = await CloudFormationHelper.GetOutputValueAsync(StackName, "ParseAndReturnBodyRestApiId");
            ParseAndReturnBodyHttpApiV1Id = await CloudFormationHelper.GetOutputValueAsync(StackName, "ParseAndReturnBodyHttpApiV1Id");
            ParseAndReturnBodyHttpApiV2Id = await CloudFormationHelper.GetOutputValueAsync(StackName, "ParseAndReturnBodyHttpApiV2Id");
            ParseAndReturnBodyRestApiUrl = await CloudFormationHelper.GetOutputValueAsync(StackName, "ParseAndReturnBodyRestApiUrl");
            ParseAndReturnBodyHttpApiV1Url = await CloudFormationHelper.GetOutputValueAsync(StackName, "ParseAndReturnBodyHttpApiV1Url");
            ParseAndReturnBodyHttpApiV2Url = await CloudFormationHelper.GetOutputValueAsync(StackName, "ParseAndReturnBodyHttpApiV2Url");

            // ReturnRawBody
            ReturnRawBodyRestApiId = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnRawBodyRestApiId");
            ReturnRawBodyHttpApiV1Id = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnRawBodyHttpApiV1Id");
            ReturnRawBodyHttpApiV2Id = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnRawBodyHttpApiV2Id");
            ReturnRawBodyRestApiUrl = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnRawBodyRestApiUrl");
            ReturnRawBodyHttpApiV1Url = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnRawBodyHttpApiV1Url");
            ReturnRawBodyHttpApiV2Url = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnRawBodyHttpApiV2Url");

            // ReturnFullEvent
            ReturnFullEventRestApiId = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnFullEventRestApiId");
            ReturnFullEventHttpApiV1Id = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnFullEventHttpApiV1Id");
            ReturnFullEventHttpApiV2Id = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnFullEventHttpApiV2Id");
            ReturnFullEventRestApiUrl = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnFullEventRestApiUrl");
            ReturnFullEventHttpApiV1Url = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnFullEventHttpApiV1Url");
            ReturnFullEventHttpApiV2Url = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnFullEventHttpApiV2Url");

            // ReturnDecodedParseBin
            BinaryMediaTypeRestApiId = await CloudFormationHelper.GetOutputValueAsync(StackName, "BinaryMediaTypeRestApiId");
            BinaryMediaTypeRestApiUrl = await CloudFormationHelper.GetOutputValueAsync(StackName, "BinaryMediaTypeRestApiUrl");

            // Lambda Function ARNs
            ParseAndReturnBodyLambdaFunctionArn = await CloudFormationHelper.GetOutputValueAsync(StackName, "ParseAndReturnBodyLambdaFunctionArn");
            ReturnRawBodyLambdaFunctionArn = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnRawBodyLambdaFunctionArn");
            ReturnFullEventLambdaFunctionArn = await CloudFormationHelper.GetOutputValueAsync(StackName, "ReturnFullEventLambdaFunctionArn");
        }

        private async Task WaitForApisAvailability()
        {
            // ParseAndReturnBody
            await ApiGatewayHelper.WaitForApiAvailability(ParseAndReturnBodyRestApiId, ParseAndReturnBodyRestApiUrl, false);
            await ApiGatewayHelper.WaitForApiAvailability(ParseAndReturnBodyHttpApiV1Id, ParseAndReturnBodyHttpApiV1Url, true);
            await ApiGatewayHelper.WaitForApiAvailability(ParseAndReturnBodyHttpApiV2Id, ParseAndReturnBodyHttpApiV2Url, true);

            // ReturnRawBody
            await ApiGatewayHelper.WaitForApiAvailability(ReturnRawBodyRestApiId, ReturnRawBodyRestApiUrl, false);
            await ApiGatewayHelper.WaitForApiAvailability(ReturnRawBodyHttpApiV1Id, ReturnRawBodyHttpApiV1Url, true);
            await ApiGatewayHelper.WaitForApiAvailability(ReturnRawBodyHttpApiV2Id, ReturnRawBodyHttpApiV2Url, true);

            // ReturnFullEvent
            await ApiGatewayHelper.WaitForApiAvailability(ReturnFullEventRestApiId, ReturnFullEventRestApiUrl, false);
            await ApiGatewayHelper.WaitForApiAvailability(ReturnFullEventHttpApiV1Id, ReturnFullEventHttpApiV1Url, true);
            await ApiGatewayHelper.WaitForApiAvailability(ReturnFullEventHttpApiV2Id, ReturnFullEventHttpApiV2Url, true);

            await ApiGatewayHelper.WaitForApiAvailability(BinaryMediaTypeRestApiId, BinaryMediaTypeRestApiUrl, false);

        }

        public async Task DisposeAsync()
        {
            await CloudFormationHelper.DeleteStackAsync(StackName);
        }
    }
}
