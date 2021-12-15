using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;

namespace TestServerlessApp.IntegrationTests.Helpers
{
    public class CloudWatchHelper
    {
        private readonly IAmazonCloudWatchLogs _cloudWatchlogsClient;

        public CloudWatchHelper(IAmazonCloudWatchLogs cloudWatchlogsClient)
        {
            _cloudWatchlogsClient = cloudWatchlogsClient;
        }

        public string GetLogGroupName(string lambdaFunctionName) => $"/aws/lambda/{lambdaFunctionName}";

        public async Task<bool> MessageExistsInRecentLogEventsAsync(string message, string logGroupName, string logGroupNamePrefix)
        {
            var attemptCount = 0;
            const int maxAttempts = 5;

            while (attemptCount < maxAttempts)
            {
                attemptCount += 1;
                var recentLogEvents = await GetRecentLogEventsAsync(logGroupName, logGroupNamePrefix);
                if (recentLogEvents.Any(x => x.Message.Contains(message)))
                    return true;
                await Task.Delay(StaticHelpers.GetWaitTime(attemptCount));
            }

            return false;
        }

        private async Task<List<OutputLogEvent>> GetRecentLogEventsAsync(string logGroupName, string logGroupNamePrefix)
        {
            var latestLogStreamName = await GetLatestLogStreamNameAsync(logGroupName, logGroupNamePrefix);
            var logEvents = await GetLogEventsAsync(logGroupName, latestLogStreamName);
            return logEvents;
        }

        private async Task<string> GetLatestLogStreamNameAsync(string logGroupName, string logGroupNamePrefix)
        {
            var attemptCount = 0;
            const int maxAttempts = 5;

            while (attemptCount < maxAttempts)
            {
                attemptCount += 1;
                if (await LogGroupExistsAsync(logGroupName, logGroupNamePrefix))
                    break;
                await Task.Delay(StaticHelpers.GetWaitTime(attemptCount));
            }

            var response =  await _cloudWatchlogsClient.DescribeLogStreamsAsync(
                new DescribeLogStreamsRequest
                {
                    LogGroupName = logGroupName,
                    Descending = true,
                    Limit = 1
                });

            return response.LogStreams.FirstOrDefault()?.LogStreamName;
        }

        private async Task<List<OutputLogEvent>> GetLogEventsAsync(string logGroupName, string logStreamName)
        {
            var response = await _cloudWatchlogsClient.GetLogEventsAsync(
                new GetLogEventsRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = logStreamName,
                    Limit = 10
                });

            return response.Events;
        }

        private async Task<bool> LogGroupExistsAsync(string logGroupName, string logGroupNamePrefix)
        {
            var response = await _cloudWatchlogsClient.DescribeLogGroupsAsync(new DescribeLogGroupsRequest{LogGroupNamePrefix = logGroupNamePrefix});
            return response.LogGroups.Any(x => string.Equals(x.LogGroupName, logGroupName));
        }
    }
}