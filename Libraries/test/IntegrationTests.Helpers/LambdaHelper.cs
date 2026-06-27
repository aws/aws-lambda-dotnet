// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;

namespace IntegrationTests.Helpers
{
    public class LambdaHelper
    {
        // Resource type that SAM AWS::Serverless::Function resources are transformed into in the deployed stack.
        private const string LambdaFunctionResourceType = "AWS::Lambda::Function";

        private readonly IAmazonLambda _lambdaClient;
        private readonly IAmazonCloudFormation _cloudFormationClient;

        public LambdaHelper(IAmazonLambda lambdaClient, IAmazonCloudFormation cloudFormationClient)
        {
            _lambdaClient = lambdaClient;
            _cloudFormationClient = cloudFormationClient;
        }

        /// <summary>
        /// Returns the Lambda functions belonging to a CloudFormation stack by listing the stack's
        /// resources directly. This is O(stack size) and independent of how many functions exist in
        /// the account, unlike scanning every function and reading its tags one at a time, which is
        /// slow and prone to throttling in a shared test account.
        /// </summary>
        public async Task<List<LambdaFunction>> FilterByCloudFormationStackAsync(string stackName)
        {
            var lambdaFunctions = new List<LambdaFunction>();
            var paginator = _cloudFormationClient.Paginators.ListStackResources(
                new ListStackResourcesRequest { StackName = stackName });

            await foreach (var resource in paginator.StackResourceSummaries)
            {
                if (string.Equals(resource.ResourceType, LambdaFunctionResourceType))
                {
                    lambdaFunctions.Add(new LambdaFunction
                    {
                        LogicalId = resource.LogicalResourceId,
                        Name = resource.PhysicalResourceId
                    });
                }
            }

            return lambdaFunctions;
        }

        public async Task<InvokeResponse> InvokeFunctionAsync(string functionName, string? payload = null)
        {
            var request = new InvokeRequest { FunctionName = functionName };
            if (!string.IsNullOrEmpty(payload))
                request.Payload = payload;
            return await _lambdaClient.InvokeAsync(request);
        }

        public async Task<ListEventSourceMappingsResponse> ListEventSourceMappingsAsync(string functionName, string eventSourceArn)
        {
            return await _lambdaClient.ListEventSourceMappingsAsync(new ListEventSourceMappingsRequest
            {
                FunctionName = functionName,
                EventSourceArn = eventSourceArn
            });
        }

        public async Task<GetFunctionUrlConfigResponse> GetFunctionUrlConfigAsync(string functionName)
        {
            return await _lambdaClient.GetFunctionUrlConfigAsync(new GetFunctionUrlConfigRequest
            {
                FunctionName = functionName
            });
        }

        public async Task WaitTillNotPending(List<string> functions)
        {
            foreach (var function in functions)
            {
                while (true)
                {
                    try
                    {
                        var response = await _lambdaClient.GetFunctionConfigurationAsync(new GetFunctionConfigurationRequest { FunctionName = function });
                        if (response.State == State.Pending)
                        {
                            await Task.Delay(1000);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch(TooManyRequestsException)
                    {
                        await Task.Delay(10000);
                    }
                }
            }
        }
    }

    public class LambdaFunction
    {
        public string? LogicalId { get; set; }
        public string? Name { get; set; }
    }
}
