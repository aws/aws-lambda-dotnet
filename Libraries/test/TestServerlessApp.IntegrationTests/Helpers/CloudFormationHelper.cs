using System;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

namespace TestServerlessApp.IntegrationTests.Helpers
{
    public class CloudFormationHelper
    {
        private readonly IAmazonCloudFormation _cloudFormationClient;

        public CloudFormationHelper(IAmazonCloudFormation cloudFormationClient)
        {
            _cloudFormationClient = cloudFormationClient;
        }

        public async Task<StackStatus> GetStackStatus(string stackName)
        {
            var stack = await GetStackAsync(stackName);
            return stack?.StackStatus;
        }

        public async Task<bool> IsDeleted(string stackName)
        {
            var attemptCount = 0;
            const int maxAttempts = 5;

            while (attemptCount < maxAttempts)
            {
                attemptCount += 1;
                if (! await StackExists(stackName))
                    return true;
                await Task.Delay(GetWaitTime(attemptCount));
            }
            return false;
        }

        public async Task DeleteStack(string stackName)
        {
            if (!await StackExists(stackName))
                return;

            await _cloudFormationClient.DeleteStackAsync(new DeleteStackRequest {StackName = stackName});
        }

        public async Task<string> GetOutputValue(string stackName, string outputKey)
        {
            var stack = await GetStackAsync(stackName);
            return stack?.Outputs.FirstOrDefault(x => string.Equals(x.OutputKey, outputKey))?.OutputValue;
        }

        private async Task<Stack> GetStackAsync(string stackName)
        {
            if (!await StackExists(stackName))
                return null;

            var response = await _cloudFormationClient.DescribeStacksAsync(new DescribeStacksRequest {StackName = stackName});
            return response.Stacks.Count == 0 ? null : response.Stacks[0];
        }

        private TimeSpan GetWaitTime(int attemptCount)
        {
            var waitTime = Math.Pow(2, attemptCount) * 5;
            return TimeSpan.FromSeconds(waitTime);
        }

        private async Task<bool> StackExists(string stackName)
        {
            try
            {
                await _cloudFormationClient.DescribeStacksAsync(new DescribeStacksRequest {StackName = stackName});
            }
            catch (AmazonCloudFormationException exception) when (exception.ErrorCode.Equals("ValidationError") && exception.Message.Equals($"Stack with id {stackName} does not exist"))
            {
                return false;
            }

            return true;
        }
    }
}