using System;
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

        public async Task<bool> MessageExistsInRecentLogEvents(string message, string logGroupName, string logGroupNamePrefix)
        {
            var attemptCount = 0;
            const int maxAttempts = 5;

            while (attemptCount < maxAttempts)
            {
                attemptCount += 1;
                var recentLogEvents = await GetRecentLogEvents(logGroupName, logGroupNamePrefix);
                if (recentLogEvents.Any(x => string.Equals(x.Message.Trim(), message)))
                    return true;
                await Task.Delay(GetWaitTime(attemptCount));
            }

            return false;
        }

        private async Task<List<OutputLogEvent>> GetRecentLogEvents(string logGroupName, string logGroupNamePrefix)
        {
            var latestLogStreamName = await GetLatestLogStreamName(logGroupName, logGroupNamePrefix);
            var logEvents = await GetLogEvents(logGroupName, latestLogStreamName);
            return logEvents;
        }

        private async Task<string> GetLatestLogStreamName(string logGroupName, string logGroupNamePrefix)
        {
            var attemptCount = 0;
            const int maxAttempts = 5;

            while (attemptCount < maxAttempts)
            {
                attemptCount += 1;
                if (await LogGroupExists(logGroupName, logGroupNamePrefix))
                    break;
                await Task.Delay(GetWaitTime(attemptCount));
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

        private async Task<List<OutputLogEvent>> GetLogEvents(string logGroupName, string logStreamName)
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

        private async Task<bool> LogGroupExists(string logGroupName, string logGroupNamePrefix)
        {
            var response = await _cloudWatchlogsClient.DescribeLogGroupsAsync(new DescribeLogGroupsRequest{LogGroupNamePrefix = logGroupNamePrefix});
            return response.LogGroups.Any(x => string.Equals(x.LogGroupName, logGroupName));
        }

        private TimeSpan GetWaitTime(int attemptCount)
        {
            var waitTime = Math.Pow(2, attemptCount) * 5;
            return TimeSpan.FromSeconds(waitTime);
        }
    }
}