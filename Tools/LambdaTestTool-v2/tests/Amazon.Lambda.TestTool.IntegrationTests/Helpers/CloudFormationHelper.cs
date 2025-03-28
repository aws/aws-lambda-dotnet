// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

namespace Amazon.Lambda.TestTool.IntegrationTests.Helpers
{
    public class CloudFormationHelper
    {
        private readonly IAmazonCloudFormation _cloudFormationClient;

        public CloudFormationHelper(IAmazonCloudFormation cloudFormationClient)
        {
            _cloudFormationClient = cloudFormationClient;
        }

        public async Task<string> CreateStackAsync(string stackName, string templateBody)
        {
            var response = await _cloudFormationClient.CreateStackAsync(new CreateStackRequest
            {
                StackName = stackName,
                TemplateBody = templateBody,
                Capabilities = new List<string> { "CAPABILITY_IAM" },
                Tags = new List<Tag>
                {
                    new Tag { Key = "aws-tests", Value = typeof(CloudFormationHelper).FullName },
                    new Tag { Key = "aws-repo", Value = "aws-lambda-dotnet" }
                }
            });
            return response.StackId;
        }

        public async Task<StackStatus> GetStackStatusAsync(string stackName)
        {
            var response = await _cloudFormationClient.DescribeStacksAsync(new DescribeStacksRequest { StackName = stackName });
            return response.Stacks[0].StackStatus;
        }

        public async Task DeleteStackAsync(string stackName)
        {
            await _cloudFormationClient.DeleteStackAsync(new DeleteStackRequest { StackName = stackName });
        }

        public async Task<string> GetOutputValueAsync(string stackName, string outputKey)
        {
            var response = await _cloudFormationClient.DescribeStacksAsync(new DescribeStacksRequest { StackName = stackName });
            return response.Stacks[0].Outputs.FirstOrDefault(o => o.OutputKey == outputKey)?.OutputValue ?? string.Empty;
        }
    }
}
